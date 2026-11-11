# Quest Armor Port

Wearable armor for **A Township Tale on Quest**, with 48 pieces across six sets, restored armor crafting, material customization, durability, and equipment support.

> Requires a modded Quest installation and a server running the matching ArmorRestored server mod. This is not a standalone client-only armor unlock.

## Armor sets

| Set | How to obtain it |
| --- | --- |
| Leather | Crafting or chest loot |
| Mushroom | Chest loot |
| Wood | Crafting only |
| Metal | Crafting or chest loot |
| Hebios | Rare chest loot; hardest chest armor set to find |
| Wyrm | Wyrm kill drops only |

Each set includes a helmet, chest piece, two shoulders, two gauntlets, and two tassets.

## Features

- **24 crafting recipes/pages** covering Leather, Metal, and Wood.
- Mixed-material customization on supported crafted components, including separate plates and buckles.
- Material-based armor protection and weight, plus durability.
- Native Equipment Mode support and local per-slot armor visibility controls.
- Eight-slot armor stands.
- Dark Steel support beyond armor—not restricted to armor recipes.

Loot-only and kill-only sets do **not** have crafting recipes. Not every piece supports the same material-customization channels.

## Installation

1. Use a compatible modded Quest game/MelonLoader setup.
2. Download the **Quest client** from the Download Board or your release's Quest download.
3. Install it in your game's MelonLoader `Mods` folder, following your Quest mod manager's instructions.
4. Remove older copies of the armor client DLL. Do not install the PC/server DLL on Quest.
5. Join a server running the matching server version.

Armor stays inactive on servers that do not advertise ArmorRestored. Client/server compatibility is checked during joining.

**Server owners:** install the matching [PC/server mod](../pchost/README.md). Quest clients cannot provide the server-side crafting, loot, or protection logic themselves.

## Quest performance and known limitations

- Armor textures and leather tint readbacks are capped at 512 pixels on each axis.
- Worn armor waits for an active local avatar, followed by a ten-second settling window, then builds one slot per frame.
- Required prefab/recipe registration still happens before admission and can cause a noticeable join pause.
- Experimental fabric flattening is **disabled on Quest** for stability investigation. Native compact cosmetic coverage remains enabled; some outfits may clip through armor.
- Individual armor pieces can still use many renderers. These mitigations are not a guarantee against crashes on every setup.

If the game closes, report what you were doing and include the latest MelonLoader log. Android logcat is especially useful when the managed log contains no crash stack. **Redact login tokens and other private information before sharing logs.**

## Loot balance

The latest pool changes target at least **2× rarer** Leather, Metal, and Mushroom selections, and **4× rarer Hebios** selections under equal entry-ticket selection. Actual chest frequency depends on native filtering and multiple loot rolls.

Wyrm armor has a **2.5% chance per supported Wyrm kill**, awarding one random slot. Wood remains crafting-only. Existing generated chest contents are not removed.

## Commands

Armor commands run on the **server**, not in a Quest-only client console. Ask a server administrator for assistance. See the [server command list](../pchost/README.md#server-commands).

## Credits

- **@ven_man** — Original PC armor mod and its content.
- **@doctornighterror** — Mod foundation used for this port.
- **@crazycircl** — Permission to port Dark Steel.
- **Shadow / fizzjay** — Quest port and integration.

Thank you to everyone who helped, tested, and waited patiently. Bug reports and balancing feedback are welcome!

[Project on GitHub](https://github.com/fizzjay/ArmorUpdate)
