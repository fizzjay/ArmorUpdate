"""Local visibility/UI source contracts, not native rendering/input validation."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


class ArmorVisibilityContracts(unittest.TestCase):
    def test_both_projects_compile_shared_visibility(self):
        for platform in ('quest', 'pchost'):
            self.assertIn('..\\shared\\ArmorVisibility.cs', read(platform + '/ArmorRestored.csproj'))

    def test_visibility_is_local_rendering_only(self):
        for path in ('quest/Draw.cs', 'pchost/ClientArmor.cs'):
            self.assertIn('if (flag && ArmorVisibility.Hidden(j)) num = -1;', read(path))
        code = read('shared/ArmorVisibility.cs')
        for forbidden in ('SendEquip', 'SendUnequip', 'ServerArmor', 'Net.', 'Wire.'):
            self.assertNotIn(forbidden, code)
        self.assertIn('hidden ^= 1 << slot;', code)
        self.assertIn('value & 255', code)

    def test_visibility_is_saved_separately_from_settings(self):
        code = read('shared/ArmorVisibility.cs')
        self.assertIn('Path.Combine(Mod.DataDir, "visibility.txt")', code)
        self.assertIn('File.ReadAllText(PathName)', code)
        self.assertIn('File.WriteAllText(PathName + ".tmp", hidden.ToString())', code)
        self.assertNotIn('settings.txt', code)

    def test_buttons_use_independent_eye_and_slash_visuals(self):
        code = read('shared/ArmorVisibility.cs')
        self.assertIn('new Transform[8]', code)
        self.assertIn('Stroke(button.transform, "eye"', code)
        self.assertIn('Stroke(button.transform, "pupil"', code)
        self.assertIn('slashes[slot].SetActive(Hidden(slot))', code)
        self.assertIn('side * 0.27f', code)
        self.assertIn('Object.Destroy(holder)', code)
        self.assertIn('Object.Destroy(ink)', code)

    def test_input_is_free_hand_edge_triggered_and_consumed_before_removal(self):
        code = read('shared/ArmorVisibility.cs')
        self.assertIn('bool edge = down && !pressed[hand]', code)
        self.assertIn('if (holder == null || !free) return false;', code)
        self.assertIn('pressed[0] = pressed[1] = true;', code)
        for path in ('quest/Hands.cs', 'pchost/ClientArmor.cs'):
            code = read(path)
            self.assertIn('ArmorVisibility.Show(dressed.Rig', code)
            self.assertIn('ArmorVisibility.Hand(', code)
            self.assertIn('ArmorVisibility.ClearButtons();', code)
