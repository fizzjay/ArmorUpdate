"""Stand asset validation and source contracts, not Unity runtime tests."""
import hashlib
import math
from pathlib import Path
import struct
import unittest
import xml.etree.ElementTree as ET
import zlib

from tools.build_stand import build, obj_mesh, png_rgba

ROOT = Path(__file__).resolve().parents[1]
NS = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}


def source(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


def png_chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)


def tiny_png(raw):
    return (b'\x89PNG\r\n\x1a\n' +
            png_chunk(b'IHDR', struct.pack('>IIBBBBB', 2, 2, 8, 2, 0, 0, 0)) +
            png_chunk(b'IDAT', zlib.compress(raw)) + png_chunk(b'IEND', b''))


class ArmorStandAssets(unittest.TestCase):
    def test_committed_asset_is_reproducible(self):
        self.assertEqual(build(), (ROOT / 'armorstand/stand.bin').read_bytes())

    def test_complete_resource_valid_mesh_and_atlas(self):
        data = build()
        magic, version, vertices, indices = struct.unpack_from('<4s3i', data)
        self.assertEqual((b'ASTD', 1), (magic, version))
        self.assertGreater(vertices, 100)
        self.assertLessEqual(vertices, 65535)
        self.assertEqual(0, indices % 3)
        mesh = list(struct.iter_unpack('<8f', data[16:16 + vertices * 32]))
        for vertex in mesh:
            self.assertTrue(all(math.isfinite(x) for x in vertex))
            self.assertAlmostEqual(1, sum(x*x for x in vertex[3:6]), places=5)
            self.assertTrue(all(-0.001 <= x <= 1.001 for x in vertex[6:]))
        self.assertAlmostEqual(0, min(v[1] for v in mesh), places=4)
        self.assertAlmostEqual(1.467, max(v[1] for v in mesh), places=4)
        offset = 16 + vertices * 32
        for index, in struct.iter_unpack('<i', data[offset:offset + indices * 4]):
            self.assertTrue(0 <= index < vertices)
        offset += indices * 4
        width, height, size = struct.unpack_from('<3i', data, offset)
        self.assertEqual((512, 512, 512*512*4), (width, height, size))
        self.assertEqual(offset + 12 + size, len(data))
        self.assertEqual(set(data[offset + 15::4]), {255})  # opaque atlas

    def test_obj_generates_flat_normals_and_triangulates(self):
        text = ('v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n'
                'vt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\n'
                'f -4/-4 -3/-3 -2/-2 -1/-1\n')
        vertices, indices = obj_mesh(text)
        self.assertEqual(4, len(vertices))
        self.assertEqual([0, 1, 2, 0, 2, 3], indices)
        self.assertTrue(all(v[3:6] == (0, 0, 1) for v in vertices))
        with self.assertRaises(ValueError):
            obj_mesh(text.replace('f -4/-4 -3/-3 -2/-2 -1/-1', 'f 0/1 1/1 2/2'))

    def test_png_filters_and_bottom_up_rgba(self):
        # First row red/green, second row blue/white. Encode all five filters.
        rows = [bytes((255, 0, 0, 0, 255, 0)), bytes((0, 0, 255, 255, 255, 255))]
        expected = bytes((0, 0, 255, 255, 255, 255, 255, 255, 255, 0, 0, 255, 0, 255, 0, 255))
        for mode in range(5):
            previous = bytes(6)
            encoded = bytearray()
            for row in rows:
                encoded.append(mode)
                for i, value in enumerate(row):
                    a, b, c = row[i-3] if i >= 3 else 0, previous[i], previous[i-3] if i >= 3 else 0
                    if mode == 4:
                        p = a+b-c
                        prediction = min((a, b, c), key=lambda x: abs(p-x))
                    else:
                        prediction = (0, a, b, (a+b)//2)[mode]
                    encoded.append((value-prediction) & 255)
                previous = row
            self.assertEqual((2, 2, expected), png_rgba(tiny_png(encoded)))
        with self.assertRaises(ValueError):
            png_rgba(tiny_png(bytes((5,)) + bytes(6) + bytes(7)))

    def test_existing_armor_packs_still_match(self):
        self.assertEqual(hashlib.sha256((ROOT / 'pchost/armor.pack').read_bytes()).digest(),
                         hashlib.sha256((ROOT / 'quest/ArmorRestored.armor.pack').read_bytes()).digest())


class ArmorStandContracts(unittest.TestCase):
    def test_both_projects_embed_same_stand_and_compile_same_code(self):
        for platform in ('pchost', 'quest'):
            tree = ET.parse(ROOT / platform / 'ArmorRestored.csproj')
            files = [x.attrib['Include'] for x in tree.findall('.//m:Compile', NS)]
            self.assertEqual(1, files.count('..\\shared\\ArmorStand.cs'))
            resources = {x.find('m:LogicalName', NS).text: x.attrib['Include']
                         for x in tree.findall('.//m:EmbeddedResource', NS)}
            self.assertEqual('..\\armorstand\\stand.bin', resources['ArmorRestored.stand'])
            self.assertTrue((ROOT / platform / resources['ArmorRestored.stand'].replace('\\', '/')).exists())
        self.assertIn('$(DefineConstants);ARMOR_QUEST', source('quest/ArmorRestored.csproj'))

    def test_eight_native_docks_and_exact_item_inclusion(self):
        code = source('shared/ArmorStand.cs')
        self.assertIn('Number = 52854U', code)
        self.assertIn('slot < 8', code)
        self.assertIn('style * 8 + slot', code)
        self.assertIn('config.IncludedItems.Add(items[style * 8 + slot])', code)
        self.assertIn('Set(entity, "hash", slot + 1)', code)
        self.assertIn('prefab.Embedded.Add(entity)', code)
        self.assertIn('Bind(entity, new MonoBehaviour[] { dock })', code)
        self.assertIn('Set(prefab, "SaveMode", 0)', code)
        self.assertIn('dock.Channels = (InteractionChannel)1;', code)
        self.assertNotIn('dock.Channels = 1;', code)
        self.assertIn('"tasset_l", "tasset_r"', code)
        self.assertNotIn('SpawnHelper.Spawn', code)  # never replaces deposited armor
        self.assertNotIn('SyncLib', code)
        self.assertNotIn('AssetBundle', code)
        self.assertNotIn('SetColor("_Color", tint)', code)
        self.assertIn('staging.activeInHierarchy', code)
        self.assertIn('Same(current, expected)', code)  # collision-safe unregister

    def test_registration_and_cleanup_use_existing_lifecycle(self):
        self.assertIn('ArmorStand.Register(Prefabs._holder, Prefabs.ByPiece, Prefabs.Items)', source('pchost/Prefabs.cs'))
        self.assertIn('ArmorStand.Register(Registry._holder, Registry.Prefabs, Registry.Items)', source('quest/Registry.cs'))
        for path in ('pchost/Prefabs.cs', 'quest/Registry.cs'):
            code = source(path)
            self.assertIn('ArmorStand.Unregister()', code)
            self.assertIn('list.Add("armorstand")', code)

    def test_body_equipping_does_not_consume_stand_pickups(self):
        for path in ('pchost/ClientArmor.cs', 'quest/Hands.cs'):
            self.assertIn('if (ArmorStand.NearSocket(was)) return;', source(path))
        code = source('pchost/ServerArmor.cs')
        self.assertLess(code.index('ArmorStand.Contains('), code.index('ArmorMaterials.Read(networkEntity)'))
        self.assertIn('GetComponent<global::Pickup>()', code)
        self.assertNotIn('Alta.Inventory.Pickup', code)
        self.assertIn('[Command("stand",', source('pchost/ArmorCommands.cs'))

    def test_stand_checks_new_id_without_throwing_getprefab(self):
        code = source('shared/ArmorStand.cs')
        self.assertNotIn('PrefabManager.GetPrefab(Number)', code)
        self.assertIn('if (FindPrefab() != null || ItemNumberOccupied())', code)
        self.assertIn('Same(FindPrefab(), prefab)', code)
        self.assertIn('map.TryGetValue(Number, out prefab) ? prefab : null', code)

    def test_stand_raw_atlas_does_not_require_missing_mip_data(self):
        code = source('shared/ArmorStand.cs')
        self.assertIn('new Texture2D(width, height, TextureFormat.RGBA32, false, false)', code)
        self.assertIn('texture.Apply(false, true)', code)
        self.assertNotIn('new Texture2D(width, height, TextureFormat.RGBA32, true, false)', code)

    def test_stand_placement_accepts_solid_floors_outside_ground_layer(self):
        code = source('pchost/ArmorCommands.cs')
        self.assertIn('Physics.RaycastAll', code)
        self.assertIn('Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore', code)
        self.assertNotIn('1 << layer', code)
        self.assertIn('a.distance.CompareTo(b.distance)', code)
        self.assertIn('hit.point.ToString("F2")', code)

    def test_spawn_diagnostics_distinguish_template_from_inactive_instances(self):
        code = source('shared/ArmorStand.cs')
        self.assertIn('Resources.FindObjectsOfTypeAll(Il2CppType.Of<NetworkEntity>())', code)
        self.assertIn('Same(entity, Get<NetworkEntity>(Prefab.gameObject))', code)
        self.assertIn('Time.unscaledTime + 5f', code)
        self.assertIn('if (report != lastProbe)', code)
        self.assertIn('ArmorStand.TickDiagnostics();', source('quest/Main.cs'))
        self.assertIn('[Command("standinfo",', source('pchost/ArmorCommands.cs'))

    def test_stand_root_links_to_prefab_before_activation_and_publication(self):
        code = source('shared/ArmorStand.cs')
        link = code.index('Set(entity, "prefab", prefab)')
        self.assertLess(code.index('Set(prefab, "entity", entity)'), link)
        self.assertLess(link, code.index('Bind(entity, new MonoBehaviour[0])'))
        self.assertLess(link, code.index('root.SetActive(true)'))
        self.assertLess(link, code.index('PrefabManager.prefabMap.Add(Number, prefab)'))
        self.assertIn('if (!Same(entity.Prefab, prefab))', code)
        self.assertNotIn('Set(entity, "hash", (int)Number)', code)

    def test_standinfo_reports_broken_prefab_links_instead_of_rejecting(self):
        code = source('pchost/ArmorCommands.cs')
        self.assertNotIn('return "Entity is not an armor stand"', code)
        self.assertIn('linkedPrefab=', code)
        self.assertIn('attachedPrefab=', code)
        self.assertIn('field.GetValue(entity)', code)

    def test_new_prefab_requires_updated_clients(self):
        self.assertIn('hello.Wire < 4', source('pchost/Net.cs'))
        self.assertIn('hello.Wire > 4', source('pchost/Net.cs'))
        self.assertIn('wire != 4', source('pchost/Gate.cs'))
        self.assertIn('_heardWire != 4', source('quest/Gate.cs'))
        for platform in ('pchost', 'quest'):
            self.assertIn('"0.5.0"', source(platform + '/Mod.cs'))


if __name__ == '__main__':
    unittest.main()
