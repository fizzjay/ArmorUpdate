import hashlib
import io
import json
from pathlib import Path
import struct
import unittest
import xml.etree.ElementTree as ET

from tools.build_crafting import split_mesh, validate_plan

ROOT = Path(__file__).resolve().parents[1]


class MeshPartitionTests(unittest.TestCase):
    def mesh(self):
        return dict(vertices=[(0., 0., 0.), (1., 0., 0.), (0., 1., 0.)],
                    normals=[(0., 0., 1.)] * 3, uv=[(0., 0.)] * 3,
                    semantics=[(1., 0., 2., 47.)] * 3,
                    submeshes=[[(0, 1, 2)]], sources=[50988])

    def test_partition_preserves_geometry_and_submesh(self):
        args = self.mesh()
        parts = split_mesh(**args)
        self.assertEqual(parts[0]['vertices'], args['vertices'])
        self.assertEqual(parts[0]['submeshes'], [[[0, 1, 2]]])

    def test_mixed_semantics_rejected(self):
        args = self.mesh()
        args['semantics'][2] = (2., 0., 2., 47.)
        args['sources'].append(62594)
        with self.assertRaisesRegex(ValueError, 'crosses'):
            split_mesh(**args)

    def test_missing_semantics_rejected(self):
        args = self.mesh()
        args['semantics'] = []
        with self.assertRaises(ValueError):
            split_mesh(**args)

    def test_nonfinite_geometry_rejected(self):
        args = self.mesh()
        args['vertices'][0] = (float('nan'), 0., 0.)
        with self.assertRaisesRegex(ValueError, 'Non-finite'):
            split_mesh(**args)

    def test_unknown_semantic_marker_rejected(self):
        args = self.mesh()
        args['semantics'] = [(1., 0., 1., 47.)] * 3
        with self.assertRaisesRegex(ValueError, 'marker'):
            split_mesh(**args)

    def test_plan_cycle_rejected_before_export(self):
        with self.assertRaisesRegex(ValueError, 'Cyclic'):
            validate_plan(dict(sources=[50988, 62594], parents=[1, 0], levels=[0, 1]))


class GeneratedConstructionTests(unittest.TestCase):
    def test_binary_matches_manifest_and_topology(self):
        data = (ROOT / 'crafting/construction.bin').read_bytes()
        manifest = json.loads((ROOT / 'crafting/construction-manifest.json').read_text())
        self.assertEqual(hashlib.sha256(data).hexdigest().upper(), manifest['pack_sha256'])
        stream = io.BytesIO(data)
        def read(fmt):
            return struct.unpack('<' + fmt, stream.read(struct.calcsize('<' + fmt)))
        def text():
            return stream.read(read('i')[0]).decode('utf8')
        self.assertEqual(stream.read(4), b'ACRF')
        self.assertEqual(read('i')[0], 1)
        self.assertEqual(stream.read(32).hex().upper(), manifest['model_sha256'])
        self.assertEqual(read('i')[0], 16)
        ids = []
        for recipe in manifest['recipes']:
            name, glyph = text(), text()
            ids.append(name)
            self.assertEqual(name, recipe['id'])
            self.assertTrue((ROOT / 'RecipeGlyphs' / glyph).exists())
            self.assertEqual(read('i')[0], len(recipe['sources']))
            parents = []
            for index, source in enumerate(recipe['sources']):
                actual, parent, level, nail = read('Iii?')
                self.assertEqual(actual, source)
                parents.append(parent)
                self.assertGreaterEqual(level, 0)
                pose = read('7fi')
                self.assertAlmostEqual(sum(v*v for v in pose[3:7]), 1, places=3)
                vertices = read('i')[0]
                self.assertEqual(vertices, recipe['vertex_counts'][index])
                attributes = [read('8f') for _ in range(vertices)]
                if nail:
                    for axis in range(3):
                        self.assertGreaterEqual(pose[axis], min(v[axis] for v in attributes)-1e-5)
                        self.assertLessEqual(pose[axis], max(v[axis] for v in attributes)+1e-5)
                total = 0
                for _ in range(read('i')[0]):
                    size = read('i')[0]
                    self.assertEqual(size % 3, 0)
                    indices = read(str(size) + 'i')
                    self.assertTrue(all(0 <= i < vertices for i in indices))
                    total += size
                self.assertGreater(total, 0)
            for start in range(len(parents)):
                seen = set()
                while start != -1:
                    self.assertTrue(0 <= start < len(parents))
                    self.assertNotIn(start, seen)
                    seen.add(start)
                    start = parents[start]
        self.assertEqual(stream.tell(), len(data))
        self.assertEqual(len(set(ids)), 16)
        self.assertEqual(set(ids), {style+'.'+slot for style in ('leather','metal')
                                   for slot in ('helmet','chest','shoulder_l','shoulder_r','gauntlet_l','gauntlet_r','tasset_l','tasset_r')})
        self.assertEqual(sum(not r['reference_only'] for r in manifest['recipes']), 16)

    def test_both_projects_embed_the_same_construction_resource(self):
        ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        for project in ('pchost', 'quest'):
            root = ET.parse(ROOT / project / 'ArmorRestored.csproj').getroot()
            entries = root.findall('.//m:EmbeddedResource', ns)
            matches = [e for e in entries if e.find('m:LogicalName', ns).text == 'ArmorRestored.construction.bin']
            self.assertEqual(len(matches), 1)
            self.assertEqual(matches[0].attrib['Include'], '..\\crafting\\construction.bin')
            self.assertTrue(any(e.attrib['Include'] == '..\\shared\\ArmorCraftingData.cs'
                                for e in root.findall('.//m:Compile', ns)))


class WyrmKillLootContract(unittest.TestCase):
    def test_wyrm_is_not_inserted_into_global_tables(self):
        source = (ROOT / 'pchost/Loot.cs').read_text(encoding='utf-8-sig')
        self.assertIn('j < 2', source)
        self.assertNotIn('Settings.WyrmLoot', source)

    def test_native_death_hook_checks_transition_and_uses_eight_slots(self):
        source = (ROOT / 'pchost/WyrmArmorLoot.cs').read_text()
        self.assertIn('method.Name == "Killed"', source)
        self.assertIn('ParameterType.Name == "HealthObject"', source)
        self.assertIn('random.Next(8)', source)
        self.assertIn('random.Next(20)', source)
        self.assertIn('if (!(bool)alive.GetValue(__instance, null)) return;', source)
        self.assertIn('if ((bool)alive.GetValue(__instance, null)) return;', source)
        self.assertIn('victim.Chunk', source)
        self.assertIn('SpawnData.DontGenerate', source)
        self.assertIn('!Mod.IsServer', source)

    def test_death_hook_is_installed_only_on_server(self):
        source = (ROOT / 'pchost/Main.cs').read_text(encoding='utf-8-sig')
        self.assertIn('if (Mod.IsServer)', source)
        self.assertIn('WyrmArmorLoot.Install()', source)
        project = (ROOT / 'pchost/ArmorRestored.csproj').read_text(encoding='utf-8-sig')
        self.assertIn('Compile Include="WyrmArmorLoot.cs"', project)
