"""Policy arithmetic and source integration, not native C#/physics validation."""
import math
import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def source(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


class ArmorStatsContracts(unittest.TestCase):
    def test_corrected_material_order_and_proposed_strengths(self):
        entries = re.findall(r'Entry\("([a-z]+)", ([\d.]+)f, ([\d.]+)f, (\d+)\)', source('shared/ArmorStats.cs'))
        self.assertEqual([e[0] for e in entries], ['copper', 'iron', 'rediron', 'gold', 'electrum', 'viridium', 'silver', 'palladium', 'mythril', 'valyan', 'darksteel'])
        strengths = [float(e[2]) for e in entries]
        self.assertEqual(strengths, sorted(set(strengths)))
        for baseline in (.5, .4, .9):
            multipliers = [math.pow(baseline, strength) for strength in strengths]
            self.assertTrue(all(a > b for a, b in zip(multipliers, multipliers[1:])))
            self.assertTrue(all(0 < x < 1 for x in multipliers))
        self.assertEqual([e[0] for e in entries if int(e[3]) == 0], ['darksteel'])

    def test_eight_piece_weight_splits_original_hip_share(self):
        shares = [1.5, 3, 1.25, 1.25, 1, 1, 1, 1]
        for total in (6, 14, 9):
            self.assertAlmostEqual(total, sum(total * share / 11 for share in shares))
        code = source('shared/ArmorStats.cs')
        self.assertIn('total * share / 11f * multiplier', code)
        self.assertIn('"weightMultiplier"', code)
        self.assertIn('using configurable fallback weights (not native values)', code)
        self.assertIn('Mathf.Clamp', code)

    def test_weight_hooks_refresh_mass_and_add_consumed_worn_items_once(self):
        code = source('shared/ArmorStats.cs')
        for token in ('PropertyGetter(typeof(Pickup), "PhysicalWeight")', '"GetInteractablesWeight"',
                      'body.mass = mass', 'pickup.OnWeightChanged()',
                      'i < 8', '__result += Weight(worn[i], materials[i])',
                      'if (!Enabled || !Mod.IsServer) return', '"OnWeightChanged"'):
            self.assertIn(token, code)
        self.assertIn('ArmorStats.RefreshWearer(p)', source('pchost/ServerArmor.cs'))

    def test_protection_uses_existing_native_pipeline_and_saved_materials(self):
        body = source('pchost/Body.cs')
        self.assertIn('Catalog.MaterialDefinition(array[i], material)', body)
        self.assertIn('materials[slots[i]]', body)
        server = source('pchost/ServerArmor.cs')
        self.assertIn('Body.Apply(w, ServerArmor.WornBy(player2), MaterialsFor(Players.Name(player2)))', server)
        self.assertIn('Body.Apply(ServerArmor.WiringOf(p), ServerArmor.WornBy(p), MaterialsFor(Players.Name(p)))', server)
        catalog = source('pchost/Catalog.cs')
        for token in ('if (piece / 8 != 1 && piece / 8 != 3) return original', 'if (strength == 1f) return original',
                      'Mathf.Pow', 'Object.Instantiate(original)', '(int)type == 0 || (int)type == 1 || (int)type == 2'):
            self.assertIn(token, catalog)
        self.assertNotIn('ReceiveDamage', source('shared/ArmorStats.cs'))

    def test_projects_startup_and_ticks(self):
        for platform in ('pchost', 'quest'):
            self.assertIn('..\\shared\\ArmorStats.cs', source(platform + '/ArmorRestored.csproj'))
            main = source(platform + '/Main.cs')
            self.assertIn('ArmorStats.Install()', main)
            self.assertIn('ArmorStats.Tick()', main)
        code = source('shared/ArmorStats.cs')
        self.assertIn('!Finite(weight) || !Finite(strength)', code)
        self.assertIn('metal.Name == "darksteel" ? 0 : rarity', code)
        self.assertIn('loot policy not yet wired', code)
