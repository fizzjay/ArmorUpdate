"""Source contracts only: native game references/runtime are not in this repo."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def source(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


class EquipmentModeContracts(unittest.TestCase):
    def test_both_projects_include_shared_mode(self):
        for platform in ('quest', 'pchost'):
            self.assertIn('..\\shared\\EquipmentMode.cs', source(platform + '/ArmorRestored.csproj'))

    def test_native_room_ownership_and_transition_guards(self):
        code = source('shared/EquipmentMode.cs')
        for token in ('EquipmentState', 'state.CustomizationRoom', 'room.Player.Pointer != player.Pointer',
                      'object.ReferenceEquals(room.Player, player)', 'room.Dummy.Root.gameObject.activeInHierarchy'):
            self.assertIn(token, code)
        self.assertLess(code.index('dummy = true;'), code.index('var room = state.CustomizationRoom;'))
        self.assertNotIn('new GameObject', code)

    def test_release_is_bounded_free_undocked_and_context_scoped(self):
        code = source('shared/EquipmentMode.cs')
        for token in ('Time.unscaledTime + 1f', 'EquipmentMode.Same(rig, entry.Rig)',
                      'item.Entity.IsBeingDestroyed', 'item.Dock != null',
                      'item.IsInteractedWith || item.Interactors.Count != 0',
                      'pending.Remove(item.Entity.Identifier)', 'pending.Remove(id);\n                attach(item);'):
            self.assertIn(token, code)

    def test_both_clients_route_dummy_rendering_and_show_helmet(self):
        for path in ('quest/Draw.cs', 'pchost/ClientArmor.cs'):
            code = source(path)
            for token in ('EquipmentMode.Resolve(transform, out dummy)', 'Dummy = dummy',
                          '!dressed.Dummy && j == 0 && !Settings.OwnHelmet', 'isLocal && transform == null'):
                self.assertIn(token, code)

    def test_clients_wait_for_full_release_and_recheck_empty_dummy_slots(self):
        for path in ('quest/Hands.cs', 'pchost/ClientArmor.cs'):
            code = source(path)
            for token in ('Releases.Released(', 'Releases.Holding(pickup)', 'Releases.Tick(',
                          'Releases.Clear()', 'modeDummy != dressed.Dummy', 'worn[slot] >= 0',
                          'me.Dummy ? 0.24f', 'was.Interactors.Count != 0', 'ArmorStand.NearSocket(was)'):
                self.assertIn(token, code)

    def test_quest_toggle_only_reveals_native_equipment_action(self):
        code = source('quest/EquipmentMenu.cs')
        for token in ('PropertyGetter(typeof(QuickAccessMenuAction), "IsIgnored")',
                      'if (!Gate.IsOpen', '__instance.TryCast<EquipmentModeQuickAccess>()',
                      'menu.Controller.PlayerController.Pointer != local.Pointer',
                      'clone.InitializeForPlayer(local.NetworkPlayer, false)',
                      'Object.Instantiate(native)', 'action.Pointer != entry.Action.Pointer'):
            self.assertIn(token, code)
        self.assertNotIn('typeof(QuickAccessMenuBubble)', code)
        self.assertNotIn('PlayingToEquipmentTransition.Trigger', code)
        self.assertNotIn('PropertyGetter(typeof(QuickAccessMenuAction), "IsValid")', code)
        self.assertIn('EquipmentMenu.Install();', source('quest/Main.cs'))
        self.assertIn('EquipmentMenu.Tick();', source('quest/Main.cs'))
        self.assertIn('Compile Include="EquipmentMenu.cs"', source('quest/ArmorRestored.csproj'))

    def test_direct_body_equipment_controls_are_disabled_on_both_clients(self):
        for path in ('quest/Hands.cs', 'pchost/ClientArmor.cs'):
            code = source(path)
            self.assertIn('!modeDummy || !dressed.Dummy', code)
            self.assertIn('if (me == null || !me.Dummy || !dummy || !EquipmentMode.Same(live, me.Rig)) return;', code)
            self.assertIn('Closed[0] = ', code)

    def test_quest_menu_uses_unambiguous_harmony_type(self):
        self.assertIn('new HarmonyLib.Harmony(', source('quest/EquipmentMenu.cs'))
