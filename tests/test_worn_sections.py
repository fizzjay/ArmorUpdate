"""Worn triangle ownership and palette propagation; not a Unity runtime test."""
import base64
import collections
import re
import unittest
from pathlib import Path
from tools.build_worn_groups import build
from tools.skin_metal import parse

ROOT=Path(__file__).resolve().parents[1]
def source(path):return (ROOT/path).read_text(encoding='utf-8-sig')

class WornSectionsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.groups=build()
        _,cls.meshes,cls.pieces=parse((ROOT/'quest/ArmorRestored.armor.pack').read_bytes())

    def test_every_metal_worn_triangle_has_one_bounded_owner(self):
        for piece in self.pieces[8:16]:
            for part in piece['parts']:
                mesh=self.meshes[part['mesh']]
                groups=self.groups[mesh['name']]
                self.assertEqual(sum(len(s)//3 for s in mesh['subs']),len(groups))
                self.assertTrue(all(g==255 or 0<=g<4 for g in groups))

    def test_chest_front_back_and_buckle_are_separate(self):
        front=collections.Counter(self.groups['cuirass_01_LOD0 leather-fit'])
        back=collections.Counter(self.groups['cuirass_02_LOD0 leather-fit'])
        self.assertEqual({0,2},set(front))
        self.assertEqual(8,front[2])  # all 8 triangles of native buckle input
        self.assertEqual({1},set(back))

    def test_glove_backing_never_gets_a_metal_palette(self):
        for side in 'LR':
            self.assertEqual({255},set(self.groups['metal_gauntlet_'+side+'_glove_flair_LOD0']))

    def test_embedded_maps_reproduce_generator(self):
        code=source('shared/ArmorCraftingData.cs')
        maps=dict(re.findall(r'\{"([^"]+)", "([A-Za-z0-9+/=]+)"\}',code))
        self.assertEqual(set(self.groups),set(maps))
        for name,encoded in maps.items():
            runs=base64.b64decode(encoded)
            decoded=[runs[i] for i in range(0,len(runs),2) for _ in range(runs[i+1])]
            self.assertEqual(self.groups[name],decoded)

    def test_both_wire_readers_consume_all_32_full_width_inputs(self):
        for file in ('quest/Wire.cs','pchost/Net.cs'):
            code=source(file)
            self.assertIn('new int[272]',code)
            self.assertIn('group<32',code)
            self.assertIn('stream.SerializeBits(ref input,32)',code)
        self.assertIn('origin.MaterialAt(group)',source('pchost/ServerArmor.cs'))
        self.assertIn('hello.Wire < 6',source('pchost/Net.cs'))
        self.assertIn('hello.Wire > 6',source('pchost/Net.cs'))

    def test_palette_only_changes_redraw_and_original_geometry_is_cloned(self):
        for file in ('quest/Draw.cs','pchost/ClientArmor.cs'):
            self.assertIn('dressed.ShownPalettes[j] != WornIngredientGroups.Key(array,j)',source(file))
            self.assertIn('ArmorCrafting.SplitWorn(',source(file))
        code=source('shared/ArmorCrafting.cs')
        split=code[code.index('internal static Mesh SplitWorn'):code.index('private static void RepairDarksteel')]
        self.assertIn('Object.Instantiate(original)',split)
        self.assertNotIn('.vertices=',split)
        self.assertNotIn('.boneWeights=',split)
        self.assertIn('group==255 ? originals',split)
        self.assertIn('wornSplits.Clear()',code)
