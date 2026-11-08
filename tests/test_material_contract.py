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
        self.assertEqual(pc, quest)
        self.assertIn('piece / 8 != 1', pc)  # Do not recolor leather or wyrm.
        self.assertIn('UnityEngine.Object.Destroy(material)', pc)

    def test_protocol_version_and_distinct_message(self):
        pc = source('pchost/Net.cs')
        quest = source('quest/Wire.cs')
        self.assertIn('int num = 2;', pc)
        self.assertIn('int num2 = 2;', quest)
        self.assertIn('wire != 2', source('pchost/Gate.cs'))
        self.assertIn('_heardWire != 2', source('quest/Gate.cs'))
        self.assertIn('byte b = 13;', pc)
        self.assertIn('if (b == 13)', pc)
        self.assertIn('num2 == 13U && Gate.IsOpen', quest)

    def test_material_hashes_are_full_width_and_interleaved(self):
        pc = source('pchost/Net.cs')
        quest = source('quest/Wire.cs')
        self.assertIn('unchecked((uint)keyValuePair.Value[8 + i])', pc)
        for text in (pc, quest):
            self.assertIn('int[] array = new int[16]', text)
            self.assertIn('s.SerializeBits(ref material, 32)', text)
            self.assertIn('array[8 + i] = unchecked((int)material)', text)
        broadcast = source('pchost/ServerArmor.cs')
        self.assertIn('Array.Copy(ServerArmor.WornBy(player2), row, 8)', broadcast)
        self.assertIn('row[8 + slot] = unchecked((int)materials[slot])', broadcast)

    def test_material_only_swaps_and_render_paths(self):
        for platform, renderer in (('pchost', 'ClientArmor.cs'), ('quest', 'Draw.cs')):
            text = source(platform + '/' + renderer)
            self.assertIn('dressed.ShownMaterials[j] != material', text)
            self.assertIn('dressed.ShownMaterials[j] = material', text)
            self.assertIn('WornMaterials.Clear()', text)
            self.assertIn('WornMaterials.Apply(piece, material', text)
        pc = source('pchost/ClientArmor.cs')
        self.assertEqual(2, pc.count('WornMaterials.Apply(piece, material'))
        quest = source('quest/Draw.cs')
        self.assertIn('meshRenderer.sharedMaterial = sharedMaterial', quest)
        self.assertIn('skinnedMeshRenderer.sharedMaterial = sharedMaterial', quest)
        self.assertIn('for (int i = 0; i < 8; i++)', quest)

    def test_persistence_retains_original_format_compatibility(self):
        text = source('pchost/ServerArmor.cs')
        self.assertIn("Split('@')", text)
        self.assertIn('ServerArmor.PieceById(saved[0])', text)
        self.assertIn('uint.TryParse(saved[1], out material)', text)
        self.assertIn('ArmorMaterials.Restore(template, previous)', text)


class QuestBuildContract(unittest.TestCase):
    def test_embedded_pack_names_match_loaders(self):
        ns = {'ms': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        for platform in ('pchost', 'quest'):
            tree = ET.parse(ROOT / platform / 'ArmorRestored.csproj')
            resources = tree.findall('.//ms:EmbeddedResource', ns)
            self.assertEqual(1, len(resources))
            self.assertEqual('ArmorRestored.armor.pack', resources[0].find('ms:LogicalName', ns).text)

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
        self.assertEqual(4, text.count('(Stream.StringEncoding)1'))
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

    def test_pc_architecture_matches_x64_loader(self):
        ns = {'ms': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        tree = ET.parse(ROOT / 'pchost/ArmorRestored.csproj')
        targets = tree.findall('.//ms:PlatformTarget', ns)
        self.assertEqual(2, len(targets))
        self.assertTrue(all(x.text == 'x64' for x in targets))


if __name__ == '__main__':
    unittest.main()
