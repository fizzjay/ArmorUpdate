# Native armor crafting and dismantling — 0.5.0 / wire 4

## Implementation status

The workbench integration is now implemented and wired into both prefab
registries. This is **source implementation, not a compiled or in-game verified
release**. No C# compiler or complete Unity/loader reference set is available in
this workspace. The supplied Root DLLs were used to inspect the actual APIs;
`crafting/api-audit.json` records a passing API-name audit (20 PC types, 11 Quest
types). This audit and Python tests do not prove C# compilation or game behavior.

Use a backed-up test world for the first build. In particular, check native save
serialization, Quest interaction, collider cooking, nail access, and multiplayer
before deploying to a live server.

## Player workflow

- **Leather and Metal:** eight separate native recipes each, including independent
  left/right tassets. Native physical ingredients, workbench ghosts, and hammerable
  nails are used; this is not an inventory-consumption command.
- **Wyrm:** kill loot only. No crafting recipe, book entry, or recipe page is
  registered. Wyrm dismantling is not defined, so this implementation does not
  turn it into generic Leather/Metal ingredients.
- Use the existing **native crafting workbench** (prefab 28396). There is no new
  custom workshop building or folded-jig animation. The donor's folded-jig system
  is not installed alongside this mod.
- Recipes are appended to native crafting-book collection 57634/provider 2,
  subject to its 255-entry limit. This makes them available to native collection
  page generation; it does not force every existing book to unlock all pages.
- For immediate testing, run **`armor recipes <player>`** on the server. It spawns
  all 16 physical pages near that player. Page prefab/item numbers are 60032–60047.
- Put a recipe page on the workbench, add the displayed ingredients, hammer the
  native nails, then grab the completed armor. All primary ingredients must have
  the same physical material. Fasteners may differ and retain their own provenance.
- To dismantle Leather/Metal, remove the paper and clear the bench, then release
  the armor into the bench's native tracked interaction area. The server restores
  a filled native project and detaches one outer ingredient. Remove the remaining
  ingredients using native workbench dismantling. The empty project is cleared.
- An item being held, docked, on a stand, outside the tracked area, or offered to
  a busy bench is not consumed. Worn armor must first be removed using the dummy.

`armor list` now includes crafting registration status. A missing asset/template,
ID collision, invalid save backend, or full book collection disables recipe
registration and reports the reason instead of publishing a partial recipe set.

## Recipe costs

Each tasset costs two primary pieces and one closure. Other pieces preserve the
reference's counts: Metal helmet three plates plus one fastener; Leather helmet
two rolls plus one closure; chest/shoulders two primary pieces plus one closure;
Metal bracers two plates plus one fastener; Leather bracers one roll plus two
closures. Input prefab hashes are taken from the reference and checked against
native workbench templates: plates 50988, leather 47760, metal fastener 62594,
leather closure 63204. The exported manifest lists every input.

## Materials and saved provenance

`ArmorCraftingServer` validates every completed input before spawning an output.
The Root assembly confirms that the chosen SpawnHelper callback runs after
initialization and **before Chunk assignment**, so the native material and saved
origin are applied before initial replication. A failed spawn leaves the project
available. A claimed project cannot spawn a second result. Cleanup failure after
commit detaches the consumed project from station saves.

`ArmorCraftRecord` is a **server-only native savable MonoBehaviour**, registered
with SavingSystem and its chunk version table. No managed saving interface is
injected into Quest. Its primitive string payload contains the bounded,
checksummed origin codec: result identity, immutable construction/result digest,
and original ingredient prefab/material pairs. Invalid records stay invalid and
are never silently treated as naturally dropped armor.

While equipped, the same origin travels with the slot in `worn.txt`:
`piece@material#base64-origin`. Existing `piece` and `piece@material` entries still
load. Origin, material, and piece are saved together, not in an entity-ID sidecar.
Replacing, removing, and dropping worn armor restores its origin before replication.
Malformed worn origins remain non-refundable rather than gaining default inputs.

Naturally dropped/admin armor has no spent-input record. Dismantling uses the
fixed recipe with native input defaults and the armor's actual primary metal;
it does not invent rare fastener materials. Existing world saves without this
new component can still take this legacy path. Back up both native saves and
`UserData/ArmorRestored` together; crash-atomic transactions across these two save
systems are not claimed.

## Compatibility and lifetime

All PC/server/Quest participants need **0.5.0 / wire 4 with identical construction
data**. Hello messages now carry the construction fingerprint. The server rejects
mismatched protocol/data even when the old join policy was log/off, because
unknown native recipe/page IDs are not safe on older clients. Wire 3 is not
compatible. Existing armor-state message 13 and eight slots are preserved.

Recipes occupy 60000–60015 in the native recipe registry. Glyphs occupy 52830–52845
in their separate registry. The original glyph range collided with native Glyph_Pierce;
the hello signature now includes the corrected glyph layout revision. These fixed-v1 IDs must never be reused for changed
recipes; future balance/layout versions need new IDs plus retained legacy plans.
There is no live recipe editing or custom native-reference serialization patch.
Client unregister removes only owned pages/values/book entries and restores
previous item glyphs. Existing worn meshes, pickup placement, material shaders,
dummy equipment, saved eye toggles, and armor-stand geometry are not rewritten.

Wyrm kill drops retain the reference's 5% chance and select one of eight slots.
Requires `loot = on` and `wyrm_loot = on`; explicit existing `off` is respected.
Ordinary and crystal Wyrm variants drop this mod's existing Wyrm set. No Wyrm is
inserted into the global chest/loot table.

## Reproducible inspection/export

```sh
python -m venv .venv
.venv/bin/pip install -r tools/crafting-requirements.txt
.venv/bin/python tools/inspect_crafting_reference.py
.venv/bin/python tools/check_crafting_api.py
.venv/bin/python tools/build_crafting.py
PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s tests -q
```

Reference DLL SHA-256:
`13FE26BB526EC1C25F0A874DCC47DC32027B91DB4BDC0FC04FE7A206D44C306C`.
Its embedded model SHA-256 matches the 35-entry donor jig archive:
`119EDC0C48D29C7DBA66F5EA3CC7537E17601BF32184E4EDDEACE6716E897B4A`.
All 14 supplied glyph PNGs match the reference byte-for-byte.

The exporter partitions compact pickup meshes by authored UV4 role/group and
rejects crossed tags or absent groups. Tasset primary panels are assigned to
individual sides; closure triangles are clipped at x=0 with interpolated UVs and
normals. Donor nails belong to folded-jig coordinates and must not be copied onto
compact meshes. New nails are seated on exposed surface triangles, checking a
12 cm approach against the ingredient and other pieces at that construction
level. This geometric check is not a game hammer/collider test.

Both projects embed the same `construction.bin` and `glyphs.bin`. Glyphs are
predecoded RGBA, avoiding an ImageConversion dependency. Rebuilding either
resource never modifies either existing `armor.pack`. The supplied game DLLs are
kept in ignored reference folders, not redistributed as new loose dependencies.

## Required game checks

- Build both projects with their existing local dependencies; no framework
  migration was made in this change.
- Confirm `armor list` reports 16 registered native recipes on PC and Quest.
- Craft every slot, including both tassets; inspect nail access and mesh colliders.
- Test mixed-primary rejection, two different fastener materials, and material
  transfer before replication on both clients.
- Save/reload completed pickups, stand-docked armor, worn armor, partially built
  projects, and partially dismantled projects; verify exact input materials.
- Test simultaneous grabs/releases, repeated completion, failed output spawn,
  busy/docked/held inputs, corrupt origins, mismatched clients, and disconnects.
- Confirm old native equipment/visibility/stand behavior and Wyrm kill-only drops.

### Registration hardening after runtime reports

The PC/Quest `FindGlyph` null-reference report exposed direct access to the lazy
native glyph dictionary before its public loader had run. Registration now calls
native `All` getters before reading or publishing registry entries, checks that
maps were populated, and never substitutes empty dictionaries. Failures retain
the stage and full exception after rollback instead of resetting to "not
registered". Template scanning skips incomplete native entries and prefers nail
settings where present, with a sorted-source fallback. Paper/result validation,
late server provenance-component attachment, and publication identity checks
are also performed. These changes have source/syntax/API-name checks, but still
require a PC/Quest restart and runtime crafting test; native loader behavior is
not simulated by the Python suite.

### Quest paper assets outside the prefab map

Runtime logs confirm that Quest has no #37066 map entry and no mapped physical
page passing validation at this join stage. The next fallback reads the native
`Book.paperPrefab` reference from loaded books, verified as a NetworkPrefab in
both supplied Root assemblies. This does not read `Item.Prefab` or register an
arbitrary original hash. The chosen native asset is cloned under the existing
fixed armor-page IDs. If no loaded book supplies a valid paper asset, registration
still stops safely; this fallback has not yet been confirmed in-game.
