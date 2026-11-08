"""Source-contract checks only: native cosmetic packing needs game validation."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def source(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


class CosmeticCoverageTests(unittest.TestCase):
    def test_native_slot_ranges_in_eight_slot_order(self):
        expected = [201, 11, 81, 91, 121, 131, 51, 51]
        for platform in ('pchost', 'quest'):
            code = source(platform + '/CosmeticCoverage.cs')
            body = code.split('internal static bool CoversSlot')[1].split('private static bool Between')[0]
            terms = re.split(r'\(mask & (\d+)U\)', body)[1:]
            def covered(mask, slot):
                for bits, clause in zip(terms[::2], terms[1::2]):
                    ranges = [(int(a), int(b)) for a, b in re.findall(r'Between\(slot, (\d+), (\d+)\)', clause)]
                    singles = [int(a) for a in re.findall(r'slot == (\d+)', clause)]
                    if mask & int(bits) and (any(a <= slot <= b for a, b in ranges) or slot in singles):
                        return True
                return False
            for index, slot in enumerate(expected):
                self.assertTrue(covered(1 << index, slot), (platform, index, slot))
                self.assertFalse(covered(0, slot))
            self.assertFalse(covered(1 << 2, 91))  # left/right shoulders independent
            self.assertFalse(covered(1 << 4, 131))  # left/right arms independent
            self.assertFalse(covered(255, 900))
            self.assertFalse(covered(255, 200))
            self.assertTrue(covered(128, 501))  # right tasset is not dropped as in 7-slot mask

    def test_only_safe_native_compact_overlays_are_used(self):
        for platform in ('pchost', 'quest'):
            code = source(platform + '/CosmeticCoverage.cs')
            for fragment in ('part.IsTextureSharingForced', 'SkinMeshPart', 'PlayerCosmeticDefinition',
                             'part.ImpededLods', 'compact.Length < lods.Length',
                             'if (compact[i] == null) return false;', 'entry.DisablerCount > 0',
                             'input.Arguments == overlay.Value', 'IsImpeded = true',
                             'previous.Primary == original.Primary'):
                self.assertIn(fragment, code)
            self.assertNotIn('renderer.enabled', code)
            self.assertNotRegex(code, r'entry\.Arguments\s*=(?!=)')
            self.assertNotRegex(code, r'DisablerCount\s*=(?!=)')

    def test_clients_register_and_release_coverage(self):
        for platform in ('pchost', 'quest'):
            tree = ET.parse(ROOT / platform / 'ArmorRestored.csproj')
            ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
            files = [x.attrib['Include'] for x in tree.findall('.//m:Compile', ns)]
            self.assertEqual(files.count('CosmeticCoverage.cs'), 1)
        for path in ('pchost/ClientArmor.cs', 'quest/Draw.cs'):
            code = source(path)
            self.assertIn('dressed.Coverage.Update(dressed.Root, dressed.Shown);', code)
            self.assertGreaterEqual(code.count('Coverage.Clear();'), 3)
        self.assertIn('new CosmeticCoverage()', source('quest/Dressed.cs'))
        quest = source('quest/CosmeticCoverage.cs')
        self.assertIn('Il2CppReferenceArray<ICustomizationHandler>', quest)
        self.assertIn('Native<TwoColors>(entry.Arguments)', quest)
        self.assertIn('PropertyInfo property', quest)  # generated IL2CPP properties
