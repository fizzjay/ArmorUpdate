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
        self.assertIn('right * (side * lateral) + toward * 0.20f', code)
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

    def test_native_bubble_visuals_not_uninitialized_pooled_behaviours(self):
        code = read('shared/ArmorVisibility.cs')
        for token in ('QuickAccessMenuBubble', '"bubbleRenderer"', '"activeBubbleMaterialAndroid"',
                      '"activeBubbleMaterial"', 'Add<MeshFilter>(shell).sharedMesh = bubbleMesh',
                      'Add<MeshRenderer>(shell).sharedMaterial = bubbleMaterial', 'holder.layer = bubbleLayer',
                      'child.layer = bubbleLayer', 'created 8, mesh='):
            self.assertIn(token, code)
        self.assertNotIn('Object.Instantiate', code)
        self.assertNotIn('Add<QuickAccessMenuBubble>', code)
        self.assertNotIn('Camera.main', code)
        self.assertNotIn('holder.transform.SetParent(rig', code)

    def test_slot_offsets_freeze_view_frame_and_never_recompute_body_bounds(self):
        code = read('shared/ArmorVisibility.cs')
        self.assertIn('local.Head', code)
        self.assertIn('slotFront = rig.InverseTransformDirection(front)', code)
        self.assertIn('slotRight = rig.InverseTransformDirection', code)
        self.assertIn('rig.TransformDirection(slotRight)', code)
        self.assertNotIn('bounds.Encapsulate', code)
        self.assertNotIn('halfWidth', code)
        self.assertIn('0.09f / Mathf.Max', code)
        self.assertIn('waiting for native quick-access bubble', code)
        placement = code[code.index('Vector3 right = rig.TransformDirection'):code.index('private static Vector3[] Ring')]
        self.assertNotIn('view.position - rig.position', placement)
        self.assertIn('Vector3 point = anchor(slot)', placement)

    def test_styled_eye_has_dark_rim_iris_and_glint(self):
        code = read('shared/ArmorVisibility.cs')
        for token in ('"dark outer ring"', '"iris"', '"eye glint"', 'Object.Destroy(rim)',
                      'new Vector3[49]', 'rim.color = new Color(0.035f'):
            self.assertIn(token, code)
