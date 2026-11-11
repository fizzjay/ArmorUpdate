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
        self.assertIn('Body.Apply(w, ServerArmor.WornBy(player2), MaterialsFor(Players.Name(player2)), ConditionsFor(Players.Name(player2)))', server)
        self.assertIn('Body.Apply(ServerArmor.WiringOf(p), ServerArmor.WornBy(p), MaterialsFor(Players.Name(p)), ConditionsFor(Players.Name(p)))', server)
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
        self.assertIn('metal loot Copper/Iron 50:50', code)

class ReleaseStabilizationContracts(unittest.TestCase):
    def test_weight_totals_and_split_hips(self):
        shares=[1.5,3,1.25,1.25,1,1,1,1]
        for total in (6,14,9,12,8,5):
            self.assertAlmostEqual(total,sum(total*v/11 for v in shares))
        text=source('shared/ArmorStats.cs')
        self.assertIn('set == 3 ? 12f : set == 4 ? 8f : set == 5 ? 5f',text)

    def test_loot_is_armor_only_copper_iron_and_does_not_change_global_distributions(self):
        text=source('shared/ArmorStats.cs')
        self.assertIn('typeof(PhysicalMaterialPart), "Generate"',text)
        self.assertIn('Catalog.PieceOf(__instance.Entity.Prefab.Hash)/8!=1',text)
        self.assertIn('UnityEngine.Random.Range(0,2)==0 ? lootCopper : lootIron',text)
        self.assertIn('if(lootCopper==null || lootIron==null)',text)
        self.assertIn('material == 0U ? SpawnData.Default : SpawnData.DontGenerate',source('pchost/ServerArmor.cs'))

    def test_rarity_progression_while_wood_and_wyrm_remain_excluded(self):
        text=source('pchost/Catalog.cs')
        self.assertIn('LootRarity = "2. Uncommon"',text)
        self.assertIn('Id="mushroom", Title="Mushroom", LootRarity="3. Rare"',text)
        self.assertIn('LootRarity = "4. Epic"',text)
        self.assertIn('Id="hebios", Title="Hebios", LootRarity="5. Legendary"',text)
        self.assertIn('Id="wood", Title="Wood", LootRarity=null',text)

    def test_bubbles_disabled_without_erasing_saved_preferences(self):
        text=source('shared/ArmorVisibility.cs')
        self.assertIn('BubblesEnabled = false',text)
        self.assertIn('if (!BubblesEnabled) return false;',text)
        self.assertIn('if (!BubblesEnabled) { ClearButtons(); return; }',text)
        self.assertNotIn('File.Delete(PathName);',text)

    def test_stand_survives_one_missing_family_but_requires_every_slot(self):
        text=source('shared/ArmorStand.cs')
        self.assertIn('for (int slot = 0; slot < 8; slot++)',text)
        self.assertIn('if (!available) throw',text)
        self.assertIn('if (pieces[i] != null && items[i] != null) config.IncludedItems.Add(items[i])',text)
        # Model every family refusing one item: another valid family must keep the slot available.
        available=[True]*48;available[45]=False
        self.assertTrue(all(any(available[i] for i in range(slot,48,8)) for slot in range(8)))
        for i in range(5,48,8):available[i]=False
        self.assertFalse(any(available[i] for i in range(5,48,8)))

    def test_leather_and_wood_keep_authored_maps_and_native_material_channels(self):
        for platform in ('pchost','quest'):
            material=source(platform+'/ArmorMaterials.cs')
            self.assertIn('piece / 8 != 0 && piece / 8 != 1',material)
            self.assertIn('piece / 8 == 0 ? 33384U : 1U',material)
            surface=source(platform+'/WornMaterials.cs')
            self.assertIn('piece / 8 == 0) return ApplyLeather',surface)
            self.assertIn('piece / 8 == 4) return ApplyWood',surface)
            self.assertIn('original.mainTexture.name!="gotera_oak_LOD0_d"',surface)
        stats=source('shared/ArmorStats.cs')
        self.assertIn('RefreshPickupSurfaces(pickup,piece,materialHash)',stats)
        self.assertIn('Original=originals',stats)

    def test_recipe_debug_is_confirmed_and_scoped_to_armor(self):
        text=source('pchost/ArmorCommands.cs')
        for token in ('Command("recipeunlock"','Command("recipereset"','confirmation != "CONFIRM"',
                      'provider==2 && ArmorCrafting.Craftable(index)', 'ArmorPage(pages[i])',
                      'book.IsOpen || book.IsInserting', 'spawned.Count!=0',
                      'GlobalPagesManager.Instance', 'SyncKnownArmorBooks()'):
            self.assertIn(token,text)
        self.assertIn('ArmorCommands.SyncKnownArmorBooks()',source('shared/ArmorCrafting.cs'))
        self.assertNotIn('addedPages.Clear',text)
        self.assertNotIn('pages.Clear()',text)

    def test_native_durability_is_registered_on_both_platforms_and_preserves_broken_items(self):
        for platform,file in [('pchost','Prefabs.cs'),('quest','Registry.cs')]:
            self.assertIn('ArmorStats.ConfigureDurability(piece, gameObject)',source(platform+'/'+file))
        text=source('shared/ArmorStats.cs')
        for token in ('"isChanceBasedDurability",false','"durabilityModule",module',
                      '"canDockWhenDamaged",true','nameof(KeepBrokenArmor)','__result=true;return false;',
                      '"integrity",Condition(condition)','"syncStage")!=null'):
            self.assertIn(token,text)

    def test_condition_save_carry_and_broken_protection_guard(self):
        text=source('pchost/ServerArmor.cs')
        for token in ("Split('~')", "Append('~')", 'float condition = 1f',
                      'float incomingCondition=ArmorStats.ReadCondition(networkEntity)',
                      'ConditionsFor(Players.Name(p))[num2]=incomingCondition',
                      'conditions[slot]-absorbed/ArmorStats.MaximumDurability',
                      'ConditionsFor(Players.Name(p))[i]) == null',
                      'ArmorStats.RestoreCondition(networkEntity,condition)', 'RepairWorn(Player player)'):
            self.assertIn(token,text)
        self.assertIn('conditions[slots[i]] > 0f',source('pchost/Body.cs'))
        for condition in (0,.1,.5,1):
            for damage in (0,1,100,10000):
                after=max(0,min(1,condition-damage/100))
                self.assertLessEqual(after,condition)
                self.assertGreaterEqual(after,0)
