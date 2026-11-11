# ArmorRestored — PC & Server

The PC/server side of the **Quest Armor Port** for A Township Tale. Provides the authoritative armor registration, crafting, loot, protection, durability, stands, and administrative commands needed by compatible clients.

**48 armor pieces · 6 sets · 24 crafting recipes**

> This README describes this repository's updated PC/server build, not the original upstream PC mod. Servers supporting this Quest port should use the matching server build. Do not run both armor implementations together.

## Armor progression

| Set | Acquisition | Chest rarity tier |
| --- | --- | --- |
| Leather | Crafting and chest loot | Uncommon |
| Mushroom | Chest loot only | Rare |
| Wood | Crafting only | None |
| Metal | Crafting and chest loot | Epic |
| Hebios | Chest loot only | Legendary |
| Wyrm | Wyrm kills only | None |

Every set has eight slots: helmet, chest, left/right shoulders, left/right gauntlets, and left/right tassets.

### Crafting and materials

- Leather, Metal, and Wood provide **24 native recipes/pages**.
- Supported crafted components can use mixed materials; plates and buckles retain separate material appearances.
- Metal chest loot uses **Copper or Iron, 50:50**. Other metals are obtained through crafting or commands rather than natural armor material rolls.
- Hebios retains its authored appearance by default.
- Armor has material-based protection and weight, plus durability. Administrative repair is available.
- Dark Steel is not naturally added to armor loot and is not limited to armor use.

## Installation

1. Back up the world and existing mod configuration.
2. Install the matching **PC/server armor DLL** from the release into the game's MelonLoader `Mods` folder.
3. Remove older/duplicate armor DLLs. Do not load the original armor mod alongside this replacement.
4. Restart the server and check the log for armor and recipe registration.
5. Quest players must install the matching [Quest client](../quest/README.md), available through the Download Board.

The same PC project includes PC-client rendering paths, but PC-client behavior should be tested with your installation. Do not install the Quest DLL on PC.

Configuration is stored under `UserData/ArmorRestored`. Back up configuration before making changes. Server settings control loot and join compatibility checks.

## Loot balance

The latest global pool changes increase existing non-armor entry tickets while retaining all eight armor slots and the native rarity tiers:

- **Leather, Metal, Mushroom:** target at least 2× rarer selections.
- **Hebios:** target at least 4× rarer selections.
- **Wyrm:** 2.5% per supported Wyrm kill; one random armor slot.
- **Wood:** no chest loot.

Pool targets assume equal entry-ticket selection. Category/dock filtering, prosperity, and multiple chest rolls affect observed rates; these are not fixed per-chest percentages. Existing generated loot is not deleted. Test newly generated/refilled loot after restarting.

## Server commands

Run these through the server console with the appropriate permissions. Replace `<player>` with the player's name or identifier.

### Equipment and spawning

```text
armor list
armor give <player> <set> <slot|all>
armor wear <player> <set> <slot|all>
armor strip <player>
armor repair <player>
armor stand <player>
armor standinfo <entityId>
```

| Command | Purpose |
| --- | --- |
| `list` | List pieces, IDs, registration, and loot information |
| `give` | Spawn armor in front of a player |
| `wear` | Directly equip armor; administrative/testing shortcut |
| `strip` | Remove worn armor and drop it in front of the player |
| `repair` | Fully repair worn armor |
| `stand` | Place an eight-slot stand on suitable ground |
| `standinfo` | Inspect a spawned stand |

**Sets:** `leather`, `metal`, `wyrm`, `hebios`, `wood`, `mushroom`

**Slots:** `helmet`, `chest`, `shoulder_l`, `shoulder_r`, `gauntlet_l`, `gauntlet_r`, `tasset_l`, `tasset_r`, or `all`

```text
armor give scary metal all
armor give scary hebios chest
armor repair scary
armor stand scary
```

### Recipes

```text
armor recipes <player>
armor recipeunlock
armor recipereset CONFIRM
```

- `recipes`: spawn the 24 Leather/Metal/Wood recipe pages near the player.
- `recipeunlock`: unlock only those armor recipes **server-wide**, with native persistence.
- `recipereset CONFIRM`: remove armor recipe unlocks and armor entries from loaded, closed books.

**Reset warning:** back up the world, close crafting books first, and reconnect clients afterward. Unloaded saved books and loose recipe pages are not deleted. Vanilla and other mods' recipe unlocks are outside the command's intended scope.

### Diagnostics

```text
armor status <player>
armor stats <player>
armor debug <on|off>
armor loot <rollsPerDock>
armor testhit <player> <zone> <damageType> <damage>
```

- `status`: worn armor and protected hit zones.
- `stats`: worn weight and material protection.
- `debug`: detailed hit logging.
- `loot`: sample native loot rolls across loaded docks; start with a small number to avoid a long synchronous test.
- `testhit`: apply a hit through native damage code, report damage, then restore health. Use a test player; native hit side effects may still occur.

**Zones:** `head`, `chest`, `hips`, `leftshoulder`, `rightshoulder`, `leftarm`, `rightarm`

**Damage types:** `pierce`, `slash`, `blunt`, `caustic`, `explosive`

```text
armor testhit scary chest slash 10
```

## Known limitations and reporting issues

- Cosmetic coverage depends on the outfit. Experimental fabric flattening is currently disabled on Quest; clipping can occur.
- Quest join registration remains synchronous, and worn armor can have high renderer counts.
- Source/data tests are not a substitute for compiling against matching game references or testing on PC and Quest.
- Additional material-stat customization and runtime compatibility may require further balancing/testing.

Include the server/client versions, reproduction steps, and relevant logs in bug reports. For unexplained Quest process exits, Android logcat can distinguish native faults from low-memory termination. **Redact authentication tokens before posting logs.**

See [CRAFTING.md](../CRAFTING.md) for detailed implementation notes and limitations.

## Credits

- **@ven_man** — Original PC armor mod and content.
- **@doctornighterror** — Foundation used for the port.
- **@crazycircl** — Permission to port Dark Steel.
- **Shadow / fizzjay** — Quest port and integration.

[Project on GitHub](https://github.com/fizzjay/ArmorUpdate)
