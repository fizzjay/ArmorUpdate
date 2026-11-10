# Material-dependent armor stats — first implementation

## Scope and status

This implements material-dependent **physical protection**, armor pickup weight,
rigidbody mass refresh and server worn-weight integration. It is **not yet tested
against proprietary game references**. Source contracts do not prove C# compilation,
Quest physics, native stat recalculation or combat behavior.

**Material rarity/natural generation is NOT wired yet.** The loot-weight column
below records the proposed policy only. Existing native Metal Material Distribution
and loot tables are unchanged, so this implementation does **not yet guarantee
Darksteel exclusion** or generation of every listed metal. Completing that part
requires the actual Distribution/material-generation implementation. Do not apply
a blanket post-spawn reroll: it would also corrupt restored/admin-spawned armor.

Hebios is not in this checkout. Existing sets are Leather, Metal and Wyrm.

## Proposed configurable defaults (not official game stats)

| Metal, worst to best | Fallback weight multiplier | Protection strength | Planned natural roll weight |
|---|---:|---:|---:|
| Copper | 0.85 | 0.8 | 100 |
| Iron | 1.00 | 1.0 | 80 |
| Red Iron | 1.20 | 1.1 | 60 |
| Gold | 3.00 | 1.2 | 40 |
| Electrum | 1.60 | 1.3 | 30 |
| Viridium | 1.75 | 1.4 | 22 |
| Silver | 1.50 | 1.5 | 16 |
| Palladium | 1.25 | 1.6 | 10 |
| Mythril | 0.65 | 1.7 | 6 |
| Valyan | 1.10 | 1.8 | 3 |
| Darksteel | 1.40 | 1.9 | 0 (reserved admin-only policy) |

These weight defaults are provisional mod balance, **not the superseded user
weight table** and not confirmed native densities. Strength follows the corrected
order; better armor is not necessarily heavier. Missing/unknown materials use
iron balance with a diagnostic; the item's material hash is never changed.

On first startup `UserData/ArmorRestored/material-stats.txt` is generated. Edit and
restart to tune values. Each line is `name=weightMultiplier,protectionStrength,
plannedNaturalWeight`. Invalid/non-finite/nonpositive values disable the stats
feature with an initialization warning, leaving the existing mod running. Darksteel's
planned loot weight is forced to zero. Copy matching config to server and clients;
this patch does not synchronize customized configuration over the wire.

`use_native_weight=true` attempts the native PhysicalMaterial's numeric
`weightMultiplier` member. **Its availability/semantics still require inspection
of the matching game assemblies.** If absent/invalid, a warning explicitly states
that configured fallback values are used. It never guesses density units or changes
native materials/distributions. Set false for deterministic configured weights.

## Weight

Full baseline sets: Metal 14, Leather 6, Wyrm 9, adapted from the supplied original
weight policy. Slot shares total 11: head 1.5, chest 3, each shoulder 1.25, each
forearm 1, each tasset 1 (splitting the original single hips slot's share of 2).
Each piece weighs `setTotal * share / 11 * multiplier`, clamped to 0.01..100.
Only metal uses a material weight multiplier.

A narrow native Pickup.PhysicalWeight postfix affects registered armor IDs only.
Every 0.5 s active armor rigidbody mass is refreshed; changed mass calls native
Pickup.OnWeightChanged. Worn items are consumed by this mod, so the server adds
the table's eight piece weights once through PlayerWeight.GetInteractablesWeight.
Equip/remove/strip/admin changes and first wiring request native OnWeightChanged.
No fake inventory items, custom movement penalties or extra network behaviours.

## Protection

The server already binds ArmorDefinition to native anatomical equipment regions.
It now selects a material-dependent clone for metal pieces using the saved material
hash. The native damage pipeline still applies protection; **there is no second
ReceiveDamage patch**, which would risk applying armor twice.

Pierce/slash/blunt damage multipliers become `baselineMultiplier ^ strength`.
Iron strength 1 preserves prior balance exactly. Increasing strength strictly
lowers physical damage multipliers without rank plateaus from a hard reduction
cap. Caustic/explosive multipliers and conductivity retain their existing set
behavior. Leather and Wyrm protection are unchanged. Original definitions and
native material assets are not modified; clones are cached by strength.

The existing seven-zone anatomy mapping is preserved: both tassets still share
one hip zone, preferring the left tasset when equipped, otherwise the right.
This does not introduce separate left/right hip hit detectors or durability.
Visibility eyes remain cosmetic; hiding an item does not remove its stats.

## Build and verify

Add `shared/ArmorStats.cs` as compiled source to both existing classic projects
(ARMOR_QUEST only for Quest). Repository includes are updated. Do not migrate
frameworks or change packs. Copy:

- PC/server: Main.cs, Catalog.cs, Body.cs, ServerArmor.cs, ArmorCommands.cs.
- Quest: Main.cs.

Use matching assemblies to compile. No protocol layout change: version/wire stay
0.4.0/3. Keep the compatibility refusal enabled.

Runtime checklist:
1. Check `armor material stats:` startup status and any explicit disabled/fallback
   warning. Confirm vanilla pickups retain their native weights.
2. Spawn/equip native-material-aware metal pickups (iron and other metals).
   Run `armor stats <player>` for computed per-piece weight and physical damage
   multipliers. This command is diagnostic, not proof of damage/physics behavior.
3. Verify increasing protection on the same anatomical physical hit; check
   elemental/conductivity and Leather/Wyrm behavior remain unchanged.
4. Compare pickup mass/weight, carried weight, equipped weight and removal. Each
   worn item must count exactly once. Check late join and restart using saved hashes.
5. Hide equipped armor using the eyes: weight/protection must stay unchanged.
6. Rebuild/copy the same config to PC and Quest; test native weight updates on both.
7. Natural material rarity and Darksteel exclusion remain a **separate unfinished
   acceptance test**, not something this patch claims to solve.
