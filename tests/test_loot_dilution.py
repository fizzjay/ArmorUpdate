"""Check armor ticket dilution arithmetic and idempotent pool source."""
from pathlib import Path
import math
import unittest
ROOT=Path(__file__).resolve().parents[1]
class LootDilution(unittest.TestCase):
    def test_probability_reduction_for_uniform_native_entry_tickets(self):
        for ordinary in range(1,100):
            for armor in (1,8,16):
                for reduction in (2,4):
                    factor=reduction+math.ceil((reduction-1)*armor/ordinary)
                    before=armor/(ordinary+armor)
                    after=armor/(factor*ordinary+armor)
                    self.assertLessEqual(after,before/reduction+1e-12)
    def test_baseline_is_retained_and_only_nonarmor_is_diluted(self):
        text=(ROOT/'pchost/Loot.cs').read_text(encoding='utf-8-sig')
        self.assertIn('originalPools.TryGetValue(rarityList,out baseline)',text)
        self.assertIn('!Prefabs.IsArmor(candidate.Item.Hash)',text)
        self.assertIn('Catalog.Sets[j].Id=="hebios" ? 4 : 2',text)
        self.assertIn('list3.AddRange(ordinary)',text)
    def test_resolved_tints_are_written_once_and_reset_on_clear(self):
        for platform in ('quest','pchost'):
            text=(ROOT/platform/'WornMaterials.cs').read_text()
            self.assertIn('!Painted.Add(material.GetInstanceID())',text)
            self.assertIn('Painted.Clear();',text)
