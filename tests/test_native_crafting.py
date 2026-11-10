"""Asset/geometry tests and integration source contracts, not game simulation."""
import io
import json
import hashlib
import zipfile
import struct
import unittest
from pathlib import Path

from tools.crafting_geometry import area, half_mesh, nail_pose, ray_distance

ROOT = Path(__file__).resolve().parents[1]

def source(name):
    return (ROOT/name).read_text(encoding='utf-8-sig')


class GeometryTests(unittest.TestCase):
    def part(self):
        return dict(vertices=[(-1.,0.,0.),(1.,0.,0.),(0.,1.,0.)],
                    normals=[(0.,0.,1.)]*3,uv=[(0.,0.),(1.,0.),(.5,1.)],submeshes=[[(0,1,2)]])

    def total_area(self, part):
        return sum(area([part['vertices'][i] for i in t]) for s in part['submeshes'] for t in s)

    def test_clipped_halves_conserve_surface_area(self):
        part=self.part()
        left,right=half_mesh(part,1),half_mesh(part,-1)
        self.assertAlmostEqual(self.total_area(part),self.total_area(left)+self.total_area(right),places=8)
        self.assertTrue(all(v[0]>=0 for v in left['vertices']))
        self.assertTrue(all(v[0]<=0 for v in right['vertices']))
        for half in (left,right):
            for v,uv in zip(half['vertices'],half['uv']):
                self.assertAlmostEqual(uv[0],(v[0]+1)/2)
                self.assertAlmostEqual(uv[1],v[1])

    def test_plane_coincident_triangle_is_not_duplicated(self):
        part=self.part();part['vertices']=[(0.,0.,0.),(0.,1.,0.),(0.,0.,1.)]
        with self.assertRaisesRegex(ValueError,'Empty'):
            half_mesh(part,-1)
        self.assertAlmostEqual(self.total_area(half_mesh(part,1)),.5)

    def test_generated_nail_is_on_surface_and_unit_rotation(self):
        pos,rotation=nail_pose(self.part())
        self.assertAlmostEqual(pos['z'],0)
        self.assertAlmostEqual(pos['x'],0)
        self.assertAlmostEqual(pos['y'],1/3)
        self.assertAlmostEqual(sum(x*x for x in rotation.values()),1)

    def test_blocked_nail_surface_is_rejected(self):
        blocker=self.part();blocker['vertices']=[(x,y,.05) for x,y,z in blocker['vertices']]
        with self.assertRaisesRegex(ValueError,'No clear'):
            nail_pose(self.part(),[blocker])

    def test_ray_intersection_distance(self):
        triangle=[(-1.,-1.,.05),(1.,-1.,.05),(0.,1.,.05)]
        self.assertAlmostEqual(ray_distance((0.,0.,0.),(0.,0.,1.),triangle),.05)
        self.assertIsNone(ray_distance((0.,0.,0.),(0.,0.,-1.),triangle))


class NativeIntegrationContracts(unittest.TestCase):
    def test_native_recipes_pages_and_book_are_wired(self):
        code=source('shared/ArmorCrafting.cs')
        for text in ('New<GenericCraftingRecipe>()','new GenericCraftingPieceDefinition()',
                     'New<Glyph>()','new NailSettings()','collection.PageBases','collection.Size + 16 > 255',
                     'FindPrefab(37066U)','CreatePage(2)','PublishRecipe(recipe)',
                     'PublishGlyph(glyph)','BuildPage(i, holder, paper)', 'GenericCraftingSettings.GenericNailDefaultDefinition'):
            self.assertIn(text,code)
        self.assertIn('ArmorCrafting.Register(',source('pchost/Prefabs.cs'))
        self.assertIn('ArmorCrafting.Register(',source('quest/Registry.cs'))
        self.assertIn('ArmorCrafting.Unregister();',source('pchost/Prefabs.cs'))
        self.assertIn('ArmorCrafting.Unregister();',source('quest/Registry.cs'))

    def test_server_completion_captures_materials_before_commit(self):
        code=source('pchost/ArmorCraftingServer.cs')
        start=code.index('private static bool SpawnResult(')
        end=code.index('private static void OnReleased',start)
        method=code[start:end]
        self.assertLess(method.index('Capture(project,index'),method.index('SpawnHelper.Spawn'))
        self.assertLess(method.index('record.Store(origin)'),method.index('claimed.Add(project'))
        self.assertLess(method.index('ArmorMaterials.Restore(entity,material)'),method.index('claimed.Add(project'))
        self.assertIn('refused without consuming project',method)
        self.assertIn('currentProject",null',method)
        self.assertIn('claimed.TryGetValue(project',method)

    def test_dismantle_requires_empty_tracked_native_bench_and_rollback(self):
        code=source('pchost/ArmorCraftingServer.cs')
        for text in ('Project(station) != null','dock.QuantityStored != 0','tracked.Contains(armor.Rigidbody)',
                     'armor.IsDocked','armor.IsInteractedWith','NativeBench(station)',
                     'origin.SourceAt(i) != definitions[i].source.Prefab.Hash',
                     'Equal(origin.RecipeDigest,Digest(index))','piece.ApplySaveData(',
                     'component.SetupAsRefuse(materials[outer])','detached.Empty()',
                     'armor.Entity.SceneDestroy()','loose.SceneDestroy()',
                     'project.Clear(false)','"isSyncingAllowed",true','syncFull','ChangedPieces'):
            self.assertIn(text,code)
        self.assertLess(code.index('Could not detach outer ingredient'),code.index('armor.Entity.SceneDestroy()'))

    def test_save_topic_is_server_only_and_invalid_records_never_become_loot(self):
        record=source('pchost/ArmorCraftRecord.cs')
        self.assertIn('[SavingInfo("ArmorRestored.CraftOrigin.v1", false)]',record)
        self.assertIn('[SaveStructure(typeof(ArmorCraftRecord), 1)]',record)
        self.assertIn('ArmorCraftOrigin.Decode',record)
        self.assertIn('payload ?? "N"',record)
        self.assertIn('payload = ""',record)
        self.assertNotIn('ArmorCraftRecord.cs',source('quest/ArmorRestored.csproj'))
        self.assertIn('ArmorCraftRecord.cs',source('pchost/ArmorRestored.csproj'))
        self.assertIn('ArmorCraftingServer.Tick();',source('pchost/Main.cs'))

    def test_equipped_origins_are_in_the_same_worn_save(self):
        code=source('pchost/ServerArmor.cs')
        for text in ("Split('#')", "Append('#').Append(origin)", 'OriginsFor(array2[0])[num]',
                     'record.Read()', 'record.Store(savedOrigin)', 'origins[num2] = incomingOrigin',
                     'OriginsFor(Players.Name(p))[slot] = null', 'OriginsFor(Players.Name(p))[piece % 8] = null'):
            self.assertIn(text,code)
        self.assertIn('origins[num2]',code[code.index('NetworkEntity returned'):])

    def test_matching_wire_and_data_are_required(self):
        pc=source('pchost/Net.cs');quest=source('quest/Wire.cs')
        self.assertIn('int num = 4;',pc)
        self.assertIn('int num2 = 4;',quest)
        self.assertIn('hello.Signature != ArmorCrafting.Signature',pc)
        self.assertIn('if (wire >= 4)',pc)
        self.assertIn('string signature = ArmorCrafting.Signature;',pc)
        self.assertIn('string signature = ArmorCrafting.Signature;',quest)
        self.assertIn('Settings.Join != "refuse") || ArmorCrafting.Ready',pc)

    def test_supplied_api_audit_is_passing_and_matches_archives(self):
        reports=json.loads(source('crafting/api-audit.json'))
        self.assertEqual(len(reports),2)
        for report,archive in zip(reports,('root.zip','rootil2cpp.zip')):
            self.assertEqual(report['missing'],[])
            self.assertGreaterEqual(len(report['checked_types']),11)
            path=ROOT/archive
            if path.exists():
                with zipfile.ZipFile(path) as zipped:
                    name=next(n for n in zipped.namelist() if n.endswith('/'+report['assembly']))
                    self.assertEqual(hashlib.sha256(zipped.read(name)).hexdigest(),report['sha256'])

    def test_generated_glyph_pack_is_complete(self):
        stream=io.BytesIO((ROOT/'crafting/glyphs.bin').read_bytes())
        self.assertEqual(stream.read(4),b'AGLY')
        self.assertEqual(struct.unpack('<ii',stream.read(8)),(1,14))
        names=[]
        for _ in range(14):
            size=struct.unpack('<i',stream.read(4))[0]
            names.append(stream.read(size).decode())
            self.assertEqual(struct.unpack('<ii',stream.read(8)),(256,256))
            self.assertEqual(len(stream.read(256*256*4)),256*256*4)
        self.assertEqual(len(set(names)),14)
        self.assertEqual(stream.read(),b'')


class QuestCraftingBuildRegressionTests(unittest.TestCase):
    def test_harmony_type_is_qualified(self):
        for path in ('shared/ArmorCrafting.cs', 'pchost/ArmorCraftingServer.cs', 'pchost/WyrmArmorLoot.cs'):
            text = source(path)
            self.assertNotIn('new Harmony(', text)
            self.assertIn('new HarmonyLib.Harmony(', text)

    def test_quest_recipe_registry_uses_native_snapshot(self):
        text = source('shared/ArmorCrafting.cs')
        self.assertIn('NativeEnumerable.Items<GenericCraftingRecipe>(HashedGeneralValue<GenericCraftingRecipe>.All)', text)
        self.assertIn('#else\n                foreach (GenericCraftingRecipe recipe in HashedGeneralValue<GenericCraftingRecipe>.All)', text)


class CraftingCollisionRegressionTests(unittest.TestCase):
    def test_glyph_revision_is_fixed_and_in_handshake(self):
        text = source('shared/ArmorCrafting.cs')
        self.assertIn('GlyphStart = 52830U', text)
        self.assertIn(':glyphs-52830-v1', text)
        self.assertNotIn('PageStart+32U', text)
        self.assertNotIn('PageStart + 32U', text)

    def test_preflight_uses_nonthrowing_lookups(self):
        text = source('shared/ArmorCrafting.cs')
        self.assertIn('var existingPage = FindPrefab(pageHash)', text)
        self.assertIn('PrefabManager.prefabMap.TryGetValue(hash,out value)', text)
        for kind in ('Item','GenericCraftingRecipe','Glyph'):
            self.assertIn('HashedGeneralValue<'+kind+'>.items.TryGetValue(hash,out value)', text)


class CraftingRegistrationDiagnosticsTests(unittest.TestCase):
    def test_registration_preserves_stage_and_stack(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('"disabled at " + stage', text)
        self.assertIn('failure + "\\n" + error', text)
        self.assertIn('building slot ', text)

    def test_missing_native_nails_are_checked(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertNotIn('Get(template.nailSettings, field)', text)
        self.assertIn('if (nativeNails == null)', text)
        self.assertIn('Get(nativeNails, field)', text)


class CraftingLazyRegistryTests(unittest.TestCase):
    def test_native_registries_are_initialized_before_preflight(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertLess(text.index('InitializeRegistries();'),text.index('var existingGlyph ='))
        self.assertIn('HashedGeneralValue<Glyph>.All == null',text)
        self.assertIn('Native registry remains unavailable:',text)
        self.assertNotIn('items = new ',text)

    def test_rollback_preserves_registration_error(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('finally { Ready = false; Status = failure; }',text)

    def test_templates_are_checked_and_nail_fallback_is_sorted(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('if (recipe == null || recipe.Pieces == null) continue;',text)
        self.assertIn('sourceHashes.Sort()',text)
        self.assertIn('previous.nailSettings == null && piece.nailSettings != null',text)

    def test_page_and_result_preflight_checks(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('armor.Length < 16 || items.Length < 16',text)
        self.assertIn('var paper = ResolvePaper()',text)
        self.assertIn('Cloned paper is missing native prefab/page component',text)


class NativePaperResolutionTests(unittest.TestCase):
    def test_paper_is_resolved_from_validated_native_templates(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('ChildComponent<PageInstance>(prefab.gameObject)',text)
        self.assertNotIn('originalItem.Prefab',text)
        self.assertIn('prefabValues.CopyTo(nativePrefabs,0)',text)
        self.assertIn('!IsPaper(prefab)',text)
        self.assertIn('Object.Instantiate(PaperItem(paper))',text)

    def test_failed_client_is_not_reported_as_data_mismatch(self):
        text=source('pchost/Net.cs')
        self.assertLess(text.index('string.IsNullOrEmpty(hello.Signature)'),text.index('hello.Signature != ArmorCrafting.Signature'))


class QuestPaperPrefabGetterRegressionTests(unittest.TestCase):
    def test_paper_discovery_never_reads_throwing_item_prefab(self):
        text=source('shared/ArmorCrafting.cs')
        body=text.split('private static NetworkPrefab ResolvePaper()',1)[1].split('private static void BuildPage',1)[0]
        self.assertNotIn('item.Prefab',body)
        self.assertNotIn('originalItem.Prefab',body)
        self.assertIn('PrefabManager.prefabMap.Values',body)
        self.assertIn('!IsPaper(prefab)',body)
        self.assertIn('prefab.Hash < selected.Hash',body)


class QuestPrefabValueCollectionBuildTests(unittest.TestCase):
    def test_value_collection_is_copied_without_interface_conversion(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertNotIn('NativeEnumerable.Items<NetworkPrefab>(PrefabManager.prefabMap.Values)',text)
        self.assertIn('new Il2CppReferenceArray<NetworkPrefab>(prefabValues.Count)',text)
        self.assertIn('prefabValues.CopyTo(nativePrefabs,0)',text)


class BookPaperReferenceRegressionTests(unittest.TestCase):
    def test_authored_book_reference_used_when_paper_map_entry_absent(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('Resources.FindObjectsOfTypeAll(Il2CppType.Of<Book>())',text)
        self.assertIn('Get(book,"paperPrefab") as NetworkPrefab',text)
        self.assertIn('if (!IsPaper(paper)) continue;',text)
        self.assertNotIn('new GameObject("Paper',text)


class QuestPaperLoadingTests(unittest.TestCase):
    def test_native_resource_folder_is_explicitly_loaded(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('Resources.LoadAll("Network Prefabs",Il2CppType.Of<NetworkPrefab>())',text)
        self.assertIn('Resources.FindObjectsOfTypeAll(Il2CppType.Of<NetworkPrefab>())',text)
        self.assertIn('candidate.gameObject.scene.IsValid()',text)

    def test_missing_assets_produce_bounded_counts(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertIn('loadedPaperPages=',text)
        self.assertIn('details.Count >= 6',text)
        self.assertIn('loadedPrefabCount',text)
        self.assertIn('loadedBookCount',text)


class HeldIngredientRegressionTests(unittest.TestCase):
    def test_native_auto_take_is_server_only_and_revalidates(self):
        content = source('pchost/ArmorCraftingServer.cs')
        self.assertIn('!Mod.IsServer', content)
        self.assertIn('pickup.FinishInteraction(hand)', content)
        self.assertNotIn('((IGenericCraftingComponent)__instance).Track(__0)', content)
        self.assertNotIn('((IGenericCraftingComponent)__instance).Released(__0)', content)
        self.assertIn('inserting.Contains(__instance)', content)

    def test_mixed_materials_are_allowed(self):
        content = source('shared/ArmorCrafting.cs')
        self.assertIn('crafting-material-preview', content)
        self.assertIn('private static void AcceptMaterial', content)
        self.assertNotIn('other.Material.Hash) != incoming', content)
        self.assertNotIn('Armor primary ingredients must use one material', source('pchost/ArmorCraftingServer.cs'))

    def test_nails_check_complete_assembly_and_hammer_width(self):
        content = source('shared/ArmorCrafting.cs')
        self.assertIn('AccessibleNail(plan,index)', content)
        self.assertIn('j<plan.Ingredients.Length', content)
        self.assertIn('tangent*.015f', content)
        self.assertIn('RayHits(', content)

class CraftingDiagnosticTests(unittest.TestCase):
    def test_patch_identity_and_bounded_throttling(self):
        code = source('shared/ArmorCrafting.cs')
        self.assertIn('creative3-diag1', code)
        self.assertIn('diagnosticTimes.Count>=256', code)
        self.assertIn('now-previous<interval', code)
        self.assertIn('registered signature=', code)

    def test_rejection_and_nail_evidence(self):
        code = source('shared/ArmorCrafting.cs')
        for marker in ('native reject ', 'incomingMaterial=',
                       'enabled=', 'filled=', 'original=', 'selected=', 'outward=',
                       'tracked=', 'TrackDiagnostic'):
            self.assertIn(marker, code)

    def test_auto_take_path_evidence(self):
        code = source('pchost/ArmorCraftingServer.cs')
        for marker in ('server auto-place Track hook', 'held Track callback',
                       'cachedKey=', 'auto-place attempting',
                       'auto-place native hand release queued',
                       'native ComponentReleased'):
            self.assertIn(marker, code)


class QuestDiagnosticCompilationTests(unittest.TestCase):
    def test_logger_uses_pc_server_flag_only_in_pc_branch(self):
        code = source('shared/ArmorCrafting.cs')
        begin = code.index('internal static void CraftLog(')
        end = code.index('private static void TrackDiagnostic', begin)
        logger = code[begin:end]
        self.assertIn('#if ARMOR_QUEST', logger)
        quest, pc = logger.split('#if ARMOR_QUEST',1)[1].split('#else',1)
        self.assertNotIn('Mod.IsServer', quest)
        self.assertIn('Mod.IsServer', pc)
        self.assertIn('quest-client', quest)


class CreativeCraftingFixTests(unittest.TestCase):
    def test_native_head_extends_outward_not_into_armor(self):
        code=source('shared/ArmorCrafting.cs')
        self.assertIn('FromToRotation(Vector3.forward,-candidate.Normal)',code)
        self.assertIn('*Vector3.back).ToString',code)

    def test_green_target_is_not_blocked_by_extra_radius(self):
        code=source('pchost/ArmorCraftingServer.cs')
        self.assertNotIn('if (distance > .08f)',code)
        self.assertIn('pickup.FinishInteraction(hand)',code)
        self.assertNotIn('((IGenericCraftingComponent)__instance).Released',code)

    def test_darksteel_channels_and_recipe_without_loot_distribution(self):
        code=source('shared/ArmorCrafting.cs')
        start=code.index('private static void RepairDarksteel()')
        repair=code[start:code.index('internal const string CraftingPatch',start)]
        for token in ('55232U','60118U','5674U','androidMaterialChannels',
                      'Darksteel Alloy NonForging','canBeSpawnedThroughCommand',
                      'existing.Hash==recipe.Hash','isShowingInList'):
            self.assertIn(token,repair)
        self.assertNotIn('Distribution',repair)
        self.assertNotIn('MateriaLib',repair)

    def test_mixed_material_origin_records_each_input(self):
        code=source('pchost/ArmorCraftingServer.cs')
        self.assertIn('materials[i] = MaterialHash(piece.Material)',code)
        self.assertIn('if (!found) primaryMaterial = materials[i]',code)
        self.assertNotIn('primaryMaterial != materials[i]',code)
