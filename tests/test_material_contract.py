"""Source-contract checks (no proprietary game DLLs required).

Run: python -m unittest discover -s tests -v
These do not replace compilation or Unity/multiplayer tests.
"""
import pathlib
import unittest
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]


def source(name):
    return (ROOT / name).read_text(encoding='utf-8-sig')


class MaterialContract(unittest.TestCase):
    def test_projects_include_material_helpers(self):
        ns = {'ms': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        for platform in ('pchost', 'quest'):
            tree = ET.parse(ROOT / platform / 'ArmorRestored.csproj')
            files = [x.attrib['Include'] for x in tree.findall('.//ms:Compile', ns)]
            for helper in ('ArmorMaterials.cs', 'WornMaterials.cs'):
                self.assertEqual(1, files.count(helper))
                self.assertTrue((ROOT / platform / helper).exists())

    def test_visual_helpers_have_identical_behavior(self):
        pc = source('pchost/WornMaterials.cs')
        quest = source('quest/WornMaterials.cs')
        quest = quest.replace('using Il2Cpp;\nusing Il2CppAlta;', 'using Alta;')
        quest = quest.replace('namespace ArmorRestoredQuest', 'namespace ArmorRestored')
        # Only the Quest readback dimensions differ; preserve shared tint math.
        cap = 'int width=source.width,height=source.height;\n                while(width>512 || height>512) {width=Math.Max(1,width/2);height=Math.Max(1,height/2); }'
        cap = cap.replace('; }', ';}')
        self.assertIn(cap, quest)
        quest = quest.replace(cap+'\n                ', '')
        quest = quest.replace('GetTemporary(width,height,', 'GetTemporary(source.width,source.height,')
        quest = quest.replace('new Texture2D(width,height,TextureFormat.RGBA32,true,true)', 'new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,true)')
        quest = quest.replace('new Rect(0,0,width,height)', 'new Rect(0,0,source.width,source.height)')
        self.assertEqual(pc, quest)
        self.assertIn('piece / 8 != 1', pc)  # Do not recolor leather or wyrm.
        self.assertIn('UnityEngine.Object.Destroy(material)', pc)

    def test_visual_lookup_uses_native_cold_material_palette(self):
        for platform in ('pchost', 'quest'):
            helper = source(platform + '/WornMaterials.cs')
            self.assertIn('physical.GetNonAtlasedNonDeformingMaterial(0)', helper)
            self.assertIn('HashedGeneralValue<PhysicalMaterial>.Get(hash)', helper)
            self.assertNotIn('HashedGeneralValue<PhysicalMaterial>.All', helper)
            self.assertIn('physical.Hash != hash', helper)
            self.assertIn('Get("iron", false)', helper)
            self.assertIn('material.HasProperty("_ColorA") && material.HasProperty("_ColorB")', helper)
            self.assertIn('Mathf.Clamp01(sample.g)', helper)
            self.assertIn('value / Math.Max(baseline, 0.01f)', helper)
            self.assertNotIn('FindTint(', helper)
            self.assertNotIn('System.Reflection', helper)

    def test_cold_sample_cleanup_and_late_load_retry(self):
        for platform in ('pchost', 'quest'):
            helper = source(platform + '/WornMaterials.cs')
            self.assertIn('finally', helper)
            self.assertIn('RenderTexture.active = active;', helper)
            self.assertIn('RenderTexture.ReleaseTemporary(temporary)', helper)
            self.assertIn('UnityEngine.Object.Destroy(readable)', helper)
            self.assertIn('Retry[hash] = Time.unscaledTime + 1f;', helper)
            self.assertIn('Paint(material, Colors[entry.Key])', helper)
            self.assertIn('RawColors.Clear();', helper)
        self.assertIn('WornMaterials.Tick();', source('pchost/ClientArmor.cs'))
        self.assertIn('WornMaterials.Tick();', source('quest/Draw.cs'))

    def test_protocol_version_and_distinct_message(self):
        pc = source('pchost/Net.cs')
        quest = source('quest/Wire.cs')
        self.assertIn('int num = 6;', pc)
        self.assertIn('int num2 = 6;', quest)
        self.assertIn('wire != 6', source('pchost/Gate.cs'))
        self.assertIn('_heardWire != 6', source('quest/Gate.cs'))
        self.assertIn('byte kind=14', pc)
        self.assertIn('if(b==14)', pc)
        self.assertIn('num2==14U && Gate.IsOpen', quest)

    def test_material_hashes_are_full_width_and_interleaved(self):
        pc = source('pchost/Net.cs')
        quest = source('quest/Wire.cs')
        self.assertIn('unchecked((uint)row.Value[8+slot])', pc)
        for text in (pc, quest):
            self.assertIn('row=new int[272]', text)
            self.assertIn('stream.SerializeBits(ref material,32)', text)
            self.assertIn('row[8+slot]=unchecked((int)material)', text)
        broadcast = source('pchost/ServerArmor.cs')
        self.assertIn('Array.Copy(ServerArmor.WornBy(player2), row, 8)', broadcast)
        self.assertIn('row[8 + slot] = unchecked((int)materials[slot])', broadcast)

    def test_material_only_swaps_and_render_paths(self):
        for platform, renderer in (('pchost', 'ClientArmor.cs'), ('quest', 'Draw.cs')):
            text = source(platform + '/' + renderer)
            self.assertIn('dressed.ShownMaterials[j] != material', text)
            self.assertIn('dressed.ShownMaterials[j] = material', text)
            self.assertIn('WornMaterials.Clear()', text)
            self.assertIn('ArmorCrafting.SplitWorn(', text)
        pc = source('pchost/ClientArmor.cs')
        self.assertIn('sharedMaterials = sectionMaterials', pc)
        quest = source('quest/Draw.cs')
        self.assertIn('meshRenderer.sharedMaterials = new Il2CppReferenceArray<Material>(sectionMaterials)', quest)
        self.assertIn('skinnedMeshRenderer.sharedMaterials = new Il2CppReferenceArray<Material>(sectionMaterials)', quest)
        self.assertIn('for (int i = 0; i < 8; i++)', quest)

    def test_persistence_retains_original_format_compatibility(self):
        text = source('pchost/ServerArmor.cs')
        self.assertIn("Split('@')", text)
        self.assertIn('ServerArmor.PieceById(saved[0])', text)
        self.assertIn('uint.TryParse(saved[1], out material)', text)
        self.assertIn('ArmorMaterials.Restore(template, previous)', text)


class QuestBuildContract(unittest.TestCase):
    def test_quest_uses_sdk_net8_without_manual_runtime_references(self):
        ns = {'ms': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        tree = ET.parse(ROOT / 'quest/ArmorRestored.csproj')
        self.assertEqual('Microsoft.NET.Sdk', tree.getroot().attrib['Sdk'])
        def prop(name):
            return tree.find('.//ms:' + name, ns).text
        self.assertEqual('net8.0', prop('TargetFramework'))
        self.assertEqual('false', prop('EnableDefaultItems'))
        self.assertEqual('false', prop('GenerateAssemblyInfo'))
        self.assertEqual('true', prop('AllowUnsafeBlocks'))
        self.assertIn('ARMOR_QUEST', prop('DefineConstants'))
        self.assertIsNone(tree.find('.//ms:TargetFrameworkVersion', ns))
        self.assertIsNone(tree.find('.//ms:TargetFrameworkIdentifier', ns))
        references = tree.findall('.//ms:Reference', ns)
        self.assertTrue(all(not x.attrib['Include'].startswith('System.') for x in references))
        for item in references:
            self.assertIn('$(QuestReferenceDir)', item.find('ms:HintPath', ns).text)
            self.assertEqual('false', item.find('ms:Private', ns).text)
        self.assertNotIn('Microsoft.CSharp.targets', source('quest/ArmorRestored.csproj'))

    def test_embedded_pack_names_match_loaders(self):
        ns = {'ms': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        for platform in ('pchost', 'quest'):
            tree = ET.parse(ROOT / platform / 'ArmorRestored.csproj')
            resources = tree.findall('.//ms:EmbeddedResource', ns)
            self.assertEqual({'ArmorRestored.armor.pack', 'ArmorRestored.stand', 'ArmorRestored.construction.bin', 'ArmorRestored.recipe-glyphs'},
                             {r.find('ms:LogicalName', ns).text for r in resources})

    def test_no_decompiler_delegate_syntax(self):
        text = source('quest/Crumb.cs') + source('quest/Wire.cs')
        for token in ('ldftn(', 'class <>O', '<0>__Receive', '<p0>', 'method fn', 'void arg', 'UnmanagedCallersOnly'):
            self.assertNotIn(token, text)
        self.assertIn('private static readonly ExitCallback NativeExitCallback', text)
        self.assertIn('Marshal.GetFunctionPointerForDelegate(NativeExitCallback)', text)

    def test_explicit_unity_enums(self):
        text = source('quest/Pack.cs')
        self.assertIn('TextureFormat.RGBA32, true, kind != 0', text)
        self.assertNotIn('hideFlags = 32', text)
        self.assertEqual(3, text.count('hideFlags = HideFlags.DontUnloadUnusedAsset'))
        self.assertNotIn('> false', text)

    def test_quest_network_api_types(self):
        text = source('quest/Wire.cs')
        self.assertIn('c.SetHandler((MessageType)137, Wire._handler)', text)
        self.assertIn('c.Send(Wire._channel, (MessageType)137, Wire._last)', text)
        self.assertEqual(5, text.count('(Stream.StringEncoding)1'))
        self.assertIn('Wire._last = new Action<Connection, Stream>', text)
        self.assertNotIn('Wire._last = delegate', text)
        self.assertIn('new HarmonyLib.Harmony(', source('quest/Main.cs'))

    def test_dxt_interpolation_byte_casts(self):
        text = source('quest/Dxt.cs')
        self.assertIn('c[8 + i] = (byte)((2 * c[i] + c[4 + i]) / 3)', text)
        self.assertIn('c[12 + i] = (byte)((c[i] + 2 * c[4 + i]) / 3)', text)
        self.assertIn('c[8 + j] = (byte)((c[j] + c[4 + j]) / 2)', text)
        # Weighted averages of byte channels always remain representable as bytes.
        for a in range(256):
            for b in range(256):
                self.assertTrue(0 <= (2 * a + b) // 3 <= 255)
                self.assertTrue(0 <= (a + 2 * b) // 3 <= 255)
                self.assertTrue(0 <= (a + b) // 2 <= 255)

    def test_managed_and_native_collection_boundaries(self):
        for f in ('Registry.cs', 'Hands.cs', 'Draw.cs'):
            text = source('quest/' + f)
            self.assertNotIn('using Il2CppSystem;', text)
            self.assertNotIn('using Il2CppSystem.Collections.Generic;', text)
            self.assertIn('Il2CppSystem.Array.CreateInstance(', text)
            self.assertIn('using Object = UnityEngine.Object;', text)
        text = source('quest/Registry.cs')
        self.assertIn('Il2CppSystem.Collections.Generic.Dictionary<uint, NetworkPrefab>', text)
        self.assertIn('Il2CppSystem.Collections.Generic.Dictionary<uint, Item>', text)
        self.assertIn('var colliders = root.Colliders;', text)


class PcBuildContract(unittest.TestCase):
    def test_explicit_game_enums(self):
        text = source('pchost/Catalog.cs')
        self.assertEqual(8, text.count('Game = (EquipmentSlot)'))
        for value in (1, 2, 3, 5):
            self.assertIn('(ImpactType)' + str(value), text)
        self.assertIn('Message = (MessageType)137', source('pchost/Net.cs'))
        for file in ('Net.cs', 'Gate.cs', 'ServerConnectionSeam.cs'):
            text = source('pchost/' + file)
            self.assertNotIn('.SetHandler(137,', text)
            self.assertNotIn('.Send(null, 137,', text)
            self.assertIn('Net.Message', text)

    def test_unity_object_aliases(self):
        for file in ('Catalog.cs', 'ClientArmor.cs', 'Loot.cs', 'MatDef.cs', 'MeshDef.cs', 'Prefabs.cs', 'TexDef.cs'):
            self.assertIn('using Object = UnityEngine.Object;', source('pchost/' + file))

    def test_tint_preserves_armor_atlas(self):
        for platform in ('pchost', 'quest'):
            helper = source(platform + '/WornMaterials.cs')
            self.assertNotIn('return native;', helper)
            self.assertNotIn('Dictionary<uint, Material> Native', helper)
            self.assertIn('material.GetColor(property)', helper)
            self.assertIn('tinted = new Material(original);', helper)
            self.assertIn('tint.a = 1f;', helper)

    def test_quest_preview_character_is_skipped(self):
        draw = source('quest/Draw.cs')
        self.assertIn('PlayerCharacter character = Draw.CharacterOf(player2);', draw)
        self.assertIn('catch (Il2CppInterop.Runtime.Il2CppException)', draw)
        self.assertIn('Draw.RigOf(character)', draw)
        self.assertNotIn('player2.PlayerCharacter', draw)

    def test_worn_armor_prefers_visible_avatar_bone_bindings(self):
        pc = source('pchost/ClientArmor.cs')
        quest = source('quest/Draw.cs')
        self.assertIn('foreach (Transform bone in body.bones)', pc)
        self.assertIn('dressed.Bones.Bones[bone.name] = bone;', pc)
        self.assertIn('foreach (Transform bone in body.bones)', quest)
        self.assertIn('d.Bones[bone.name] = bone;', quest)
        for text in (pc, quest):
            self.assertIn('body.transform.parent.name != "Baked"', text)
            self.assertIn('bone bindings from baked avatar renderers', text)

    def test_material_roundtrip_and_reachable_removal(self):
        server = source('pchost/ServerArmor.cs')
        self.assertIn('ArmorMaterials.Restore(networkEntity, material);', server)
        self.assertIn('ArmorMaterials.Read(networkEntity) != material', server)
        self.assertIn('could not read this item', server)
        equip = server[server.index('internal static void OnEquip'):server.index('internal static void OnUnequip')]
        self.assertLess(equip.index('ServerArmor.Spawn(num3'), equip.index('networkEntity.SceneDestroy();'))
        pc = source('pchost/ClientArmor.cs')
        quest = source('quest/Hands.cs')
        self.assertIn('else if (flag2 && array != null', pc)
        self.assertIn('if (!flag3) return;', quest)
        for text, end in ((pc, 'private static int PieceOf'), (quest, 'private static Vector3 Anchor')):
            slot = text[text.index('private static Vector3 SlotPoint'):text.index(end)]
            self.assertNotIn('Center(', slot)

    def test_missing_pack_fails_before_prefab_or_join_setup(self):
        pack = source('pchost/Pack.cs')
        self.assertIn('return Pack.Pieces.Count == Catalog.Count;', pack)
        self.assertIn('if (num != Catalog.Count)', pack)
        prefabs = source('pchost/Prefabs.cs')
        self.assertLess(prefabs.index('if (!Pack.Loaded)'), prefabs.index('Prefabs._busy = true;'))
        self.assertIn('if (!isServer || !Pack.Loaded)', source('pchost/ServerSeam.cs'))
        for platform in ('pchost', 'quest'):
            self.assertIn('GetManifestResourceNames()', source(platform + '/Pack.cs'))

    def test_pc_private_material_member_access(self):
        text = source('pchost/ArmorMaterials.cs')
        self.assertIn('BindingFlags.NonPublic', text)
        self.assertIn('type = type.BaseType', text)
        self.assertIn('throw new MissingMemberException', text)
        self.assertNotIn('part.physicalMaterial', text)
        self.assertNotIn('entity.networkEntityBehaviours', text)
        for member in ('physicalMaterial', 'renderers', 'materialChannel', 'impactors', 'listenToPhysicalMaterial', 'durabilityModule', 'entity', 'materialDistribution'):
            self.assertIn('Set(part, "' + member + '",', text)
        self.assertIn('Set(entity, "networkEntityBehaviours",', text)

    def test_pc_remaining_typed_api_calls(self):
        net = source('pchost/Net.cs')
        import re
        self.assertIsNone(re.search(r'SerializeString\(ref \w+, [01]\)', net))
        self.assertIn('connection.Send(null, (MessageType)10,', net)
        self.assertIn('UnityEngine.Rendering.IndexFormat.UInt32', source('pchost/MeshDef.cs'))
        self.assertIn('(TextureFormat)this.Format', source('pchost/TexDef.cs'))
        loot = source('pchost/Loot.cs')
        self.assertIn('num6, out num7)', loot)
        self.assertNotIn('allowedResult == null', loot)

    def test_pc_architecture_matches_x64_loader(self):
        ns = {'ms': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        tree = ET.parse(ROOT / 'pchost/ArmorRestored.csproj')
        targets = tree.findall('.//ms:PlatformTarget', ns)
        self.assertEqual(2, len(targets))
        self.assertTrue(all(x.text == 'x64' for x in targets))

    def test_glove_backing_keeps_original_grey(self):
        for platform in ('pchost', 'quest'):
            text = source(platform + '/WornMaterials.cs')
            for side in ('L', 'R'):
                self.assertIn('metal_gauntlet_' + side + '_glove_flair_LOD0', text)
            self.assertLess(text.index('meshName != null'), text.index('Tinted.TryGetValue'))
            self.assertIn('Apply(piece, hash, result[i], meshName)', text)
        self.assertIn('Pack.Mats(wornPart.Mats),palette,slot', source('pchost/ClientArmor.cs'))
        self.assertIn('new[] {Pack.MaterialOf(partDef.Mat)},palette,slot', source('quest/Draw.cs'))

    def test_coverage_reports_silent_skips(self):
        for platform in ('pchost', 'quest'):
            text = source(platform + '/CosmeticCoverage.cs')
            self.assertIn('Diagnose(current, handlers, mask)', text)
            self.assertIn('diagnostic == lastDiagnostic', text)
            for reason in ('missing-compact-LODs', 'texture-sharing', 'outside-armor-slots', 'not-mesh-part'):
                self.assertIn(reason, text)
            self.assertIn('packedInputs=', text)


if __name__ == '__main__':
    unittest.main()
