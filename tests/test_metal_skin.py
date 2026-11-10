"""Binary-resource regression tests; these exercise the actual pack, not C# text."""
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('skin_metal', ROOT / 'tools/skin_metal.py')
skin = importlib.util.module_from_spec(spec)
spec.loader.exec_module(skin)


def digest(data):
    return hashlib.sha256(data).hexdigest()


class MetalSkinTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = (ROOT / 'pchost/armor.pack').read_bytes()
        cls.prefix, cls.meshes, cls.pieces = skin.parse(cls.data)
        cls.contract = json.loads((ROOT / 'tests/fixtures/metal_skin_contract.json').read_text())

    def test_clients_embed_identical_resources(self):
        self.assertEqual(self.data, (ROOT / 'quest/ArmorRestored.armor.pack').read_bytes())

    def test_original_meshes_textures_and_materials_untouched(self):
        r=skin.Reader(self.prefix); magic=r.take(8);textures=[]
        for _ in range(r.number()):
            start=r.pos;r.text();r.take(16);r.take(r.number());textures.append(self.prefix[start:r.pos])
        materials=[]
        for _ in range(r.number()):
            start=r.pos;r.text();r.take(40);materials.append(self.prefix[start:r.pos])
        nt=self.contract['original_texture_count'];nm=self.contract['original_material_count']
        original=magic+skin.integer(nt)+b''.join(textures[:nt])+skin.integer(nm)+b''.join(materials[:nm])
        self.assertEqual(digest(original), self.contract['prefix'])
        for mesh, expected in zip(self.meshes, self.contract['meshes']):
            self.assertEqual(digest(mesh['raw']), expected)

    def test_leather_wyrm_helmet_and_all_pickups_untouched(self):
        for piece in self.pieces[:24]:
            old = self.contract['pieces'][piece['id']]
            self.assertEqual(digest(piece['prefix']), old['prefix'])
            self.assertEqual(digest(piece['suffix']), old['suffix'])
            if not piece['id'].startswith('metal.') or piece['id'] == 'metal.helmet':
                self.assertEqual([digest(p['raw']) for p in piece['parts']],
                                 [p['raw'] for p in old['parts']])

    def test_sixty_plates_use_leather_weights_and_authored_rest_geometry(self):
        count = changed_influences = 0
        leather = {p['id']: p for p in self.pieces if p['id'].startswith('leather.')}
        for piece in self.pieces:
            if not piece['id'].startswith('metal.') or piece['id'] == 'metal.helmet':
                continue
            slot = piece['id'].split('.')[1]
            reference_piece = leather['leather.' + ('chest' if slot.startswith('tasset_') else slot)]
            candidates = [p for p in reference_piece['parts'] if p['skin']]
            reference = max(candidates, key=lambda p: len(self.meshes[p['mesh']]['v']))
            ref_mesh = self.meshes[reference['mesh']]
            tree = skin.Nearest(ref_mesh['v'])
            for part, old in zip(piece['parts'], self.contract['pieces'][piece['id']]['parts']):
                self.assertTrue(part['skin'])
                if 'local' not in old:  # Already-skinned glove lining is untouched.
                    self.assertEqual(digest(part['raw']), old['raw'])
                    continue
                count += 1
                mesh = self.meshes[part['mesh']]
                original = self.meshes[old['mesh']]
                self.assertEqual(mesh['uv'], original['uv'])
                self.assertEqual(mesh['subs'], original['subs'])
                self.assertEqual(part['bones'], reference['bones'])
                self.assertEqual(mesh['bind'], ref_mesh['bind'])
                self.assertEqual(len(mesh['w']), len(mesh['v']))
                bind = mesh['bind'][part['bones'].index(old['bone'])]
                model = skin.multiply(skin.inverse(bind), old['local'])
                for v, source in zip(mesh['v'], original['v']):
                    expected = skin.point(model, source)
                    self.assertLess(math.dist(v, expected), 2e-6)
                for w in mesh['w']:
                    self.assertAlmostEqual(sum(w[4:]), 1, places=5)
                    self.assertTrue(all(0 <= i < len(part['bones']) for i in w[:4]))
                # Check actual transferred samples, not just array dimensions.
                for index in (0, len(mesh['v']) // 2, len(mesh['v']) - 1):
                    nearest = tree.find(mesh['v'][index])
                    self.assertEqual(mesh['w'][index], ref_mesh['w'][nearest])
                parent = part['bones'].index(old['bone'])
                changed_influences += sum(any(i != parent and weight > 0.001
                    for i, weight in zip(w[:4], w[4:])) for w in mesh['w'])
        self.assertEqual(count, 60)
        # Skinning must actually change deformation, not just wrap a rigid mesh
        # in a mathematically identical single-bone renderer.
        self.assertGreater(changed_influences, 1000)

    def test_converter_is_idempotent(self):
        output, count = skin.convert(self.data)
        self.assertEqual(count, 0)
        self.assertEqual(output, self.data)

    def test_nearest_search_matches_brute_force(self):
        vertices = [(math.sin(i), math.cos(i * 2), i / 13) for i in range(90)]
        tree = skin.Nearest(vertices)
        for v in [(0, 0, 0), (1, -1, 2), (-0.1, 0.2, 4)]:
            expected = min(range(len(vertices)), key=lambda i: math.dist(v, vertices[i]))
            self.assertEqual(tree.find(v), expected)
