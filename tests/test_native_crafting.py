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
                     'PrefabManager.GetPrefab(37066U)','CreatePage(2)','PublishRecipe(recipe)',
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
        self.assertIn('Status + "\\n" + error', text)
        self.assertIn('building slot ', text)

    def test_missing_native_nails_are_checked(self):
        text=source('shared/ArmorCrafting.cs')
        self.assertNotIn('Get(template.nailSettings, field)', text)
        self.assertIn('if (nativeNails == null)', text)
        self.assertIn('Get(nativeNails, field)', text)
