# Native armor crafting and dismantling — 0.5.0 / wire 5

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

- **Leather, Metal and Wood:** eight separate native recipes each, including independent
  left/right tassets. Native physical ingredients, workbench ghosts, and hammerable
  nails are used; this is not an inventory-consumption command.
- **Hebios and Mushroom:** loot-only (Epic and Rare global loot respectively,
  subject to the existing loot setting). Neither set registers recipes or pages.
- **Wood:** crafting-only; never added to global loot.
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
  all 24 physical pages near that player. Pages are 60032–60047 (Leather/Metal)
  and 62032–62039 (Wood); Wyrm, Hebios and Mushroom page indices remain unused.
- Put a recipe page on the workbench, add the displayed ingredients, hammer the
  native nails, then grab the completed armor. Primary ingredients may use different physical materials. Every input retains
  its own material provenance.
- To dismantle Leather/Metal/Wood, remove the paper and clear the bench, then release
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

All PC/server/Quest participants need **0.5.0 / wire 5 with identical construction
data**. Hello messages now carry the construction fingerprint. The server rejects
mismatched protocol/data even when the old join policy was log/off, because
unknown native recipe/page IDs are not safe on older clients. Wire 3 is not
compatible. Existing armor-state message 13 and eight slots are preserved.

Leather/Metal recipes occupy 60000–60015; Wood recipes occupy 60032–60039.
The former Hebios recipes (60024–60031) are no longer published.
Glyphs occupy 52830–52845 and 52855–52878
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
The original 14 glyph PNGs match the reference byte-for-byte; seven supplied
Hebios glyphs are also embedded (21 total).

The exporter partitions compact pickup meshes by authored UV4 role/group and
rejects crossed tags or absent groups. Tasset primary panels are assigned to
individual sides; closure triangles are clipped at x=0 with interpolated UVs and
normals. Donor nails belong to folded-jig coordinates and must not be copied onto
compact meshes. New nails are seated on exposed surface triangles, checking a
12 cm approach against the ingredient and other pieces at that construction
level. This geometric check is not a game hammer/collider test.

Both projects embed the same `construction.bin` and `glyphs.bin`. Glyphs are
predecoded RGBA, avoiding an ImageConversion dependency. The original Leather/Metal exporter does not change accepted worn records;
its final Hebios import step appends to both `armor.pack` resources. The supplied game DLLs are
kept in ignored reference folders, not redistributed as new loose dependencies.

## Required game checks

- Build both projects with their existing local dependencies; no framework
  migration was made in this change.
- Confirm `armor list` reports 24 registered native recipes on PC and Quest.
- Craft every slot, including both tassets; inspect nail access and mesh colliders.
- Test mixed-primary acceptance, two different fastener materials, and material
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

### Explicit Quest resource discovery

The book-reference fallback was reported insufficient on Quest. The resolver now
explicitly requests NetworkPrefab assets from `Network Prefabs` (the resource
folder read by PC native PrepareSpawnSetups), then inspects loaded prefab assets
outside the map. Scene instances are excluded from that asset fallback. It does
not clear or rebuild the native map or assume that an empty search means paper
exists. Failure includes folder/prefab/book counts and up to six loaded native
paper-page names. This patch cannot supply an asset missing from the APK, and
its resource-loading fallback has not been verified in-game.

### Held-input and nail-access patch

- Server auto-places a held, undocked valid ingredient when its center comes within
  8 cm of the native selected target. It ends native hand interactions, rechecks
  the active project/ingredient, refreshes the native selection, then uses the
  native insertion/destruction callback. Ordinary recipes are excluded.
- Quest and PC now apply the same primary-material consistency preview rule.
  Consecutive metal plates must have the same physical material; mismatched
  plates should no longer show a misleading green preview.
- Required nail transforms are derived from unchanged construction meshes using
  the complete assembly as blockers, outward-facing normals and five approach
  rays spanning 3 cm. Worn armor placement is unchanged. Run
  `.venv/bin/python tools/check_crafting_nail_access.py` to audit all 33 nails.
- Compatibility signature has an `interaction2` suffix: update both client and
  server together. Existing construction binary and ingredient provenance remain
  unchanged. Do not mix this build with the previous recipe descriptors.
- Runtime checks still required: two matching plates consecutively; mismatched
  plate rejection; held auto-take with both hands; metal chest fastener hammer
  access; normal non-armor crafting; completion/save/reload/disassembly. Local
  source tests and geometry checks are not a game runtime or compiled build.

### Crafting diagnostic build `interaction2-diag1`

Diagnostics are on automatically on both platforms; no configuration change is
needed. Look for `armor craft diag` in both logs. Startup prints the patch ID,
side, compatibility signature and hook installation. Each required nail prints
its recipe/piece/source/level, original position, selected position and outward
hammer direction (four decimal places).

While an armor recipe is active, native station tracking prints its tracked
pickup count every five seconds. Acceptance logs identify native rejection
(enabled/filled/source), missing metal material, conflicting existing material,
or acceptance. Server auto-placement logs the Track callback, held/docked state,
cached target, target distance, attempt, post-release abort or native insertion
result. Native drop/release callbacks are logged separately. Repeated per-item
acceptance and waiting messages are throttled to three seconds; diagnostic
throttle storage is capped at 256 keys.

For a failure report, include startup through one attempt to insert the first
and second plates, then one attempt at the chest nail, from BOTH Quest and PC.
A station heartbeat without a server `held Track callback` is useful evidence
that the auto-place hook is not being reached. No behavior fix is claimed by this
diagnostic build; it instruments the previous patch's decision points.

### Creative crafting correction (`creative3-diag1`, supersedes interaction2)

- Mixed primary metals are allowed in previews AND final completion. Native
  source, level, filled-slot and known-material validation remain. Per-input
  materials are retained in the provenance record for native dismantling. The
  first primary input determines the finished item's single native material
  channel/stats; this is not a new per-plate worn-mesh paint system.
- Auto-take uses the native green-target radius, not an additional 8 cm test.
  It finishes native hand interactions and leaves insertion/destruction to the
  game's ComponentReleased end-of-frame coroutine. The previous synchronous
  Released call raced that coroutine and hand synchronization; it is removed.
  Native SetMaterial already calls RequestSyncPiece; no blind extra sync is
  introduced. Native Pop logging remains for any further placement disappearance.
- Confirmed from PC IL: GenericCraftingNail.UpdatePosition extends the unfinished
  nail mesh along Vector3.back. Nail forward now points INTO the surface so the
  head extends outward. A nail can keep the same surface point but now has the
  opposite orientation. Whole-assembly approach-ray validation still applies.
- Darksteel 55232 native forging/non-forging materials populate all six render
  channels (androidMaterialChannels on Quest). Its native list visibility is
  enabled, smelting recipe 60118 and material are added idempotently to upgrade
  5674, and the native ingot is command-spawnable when its asset is found. No
  MateriaLib dependency, custom material ID, or natural loot/distribution entry is
  added. Missing native assets produce an explicit diagnostic and do not block
  armor recipe registration.
- Update both sides: compatibility signature now ends in `creative3`. Test on a
  fresh project: mixed plates consecutively, auto-take at a green preview, chest
  nail hammering, completion, save/reload and material-preserving dismantling.
  Check `Darksteel 55232 visible` or the explicit missing-assets diagnostic.
  Static/API/geometry tests cannot establish in-game success or a compiled DLL.

### Worn per-ingredient metals (`worn-sections1`, wire 5)

The server now transmits a bounded 32-entry material palette for each worn slot,
derived from its validated saved crafting origin. Both clients redraw on palette
changes even when the armor ID and primary metal stay the same. Existing saved
crafted armor already contains these inputs; it does not need to be recrafted.
Loot/legacy armor with no crafting origin retains its single native material.
Stats still use the primary material; multi-material combat balancing is not
changed by this visual feature.

Worn metal triangles are assigned to construction ingredients using exact atlas
triangle/vertex matches, with explicit component ownership for reused atlas islands
and differing LOD topology. `tools/build_worn_groups.py` reproducibly generates the
compact maps in the existing shared source, so no additional project item or
embedded resource is needed. The chest front, back and 8-triangle buckle group
remain independently paintable. Helmets, shoulders, gauntlet plates/closures and
split hips are covered; the glove backing has an explicit unpainted sentinel.
Original positions, normals, UVs, bind poses and skin weights remain unchanged;
cloned meshes only regroup triangle indices into material sections. Tinting still
uses original armor maps/shaders. Cached section meshes are cleaned up with recipe
assets. Base meshes remain CPU-readable to support this regrouping.

Update PC/server AND Quest together: wire 5 rejects older table layouts rather
than interpreting them as palette data. The crafting signature ends with
`worn-sections1`. Runtime checklist: gold chest front + Darksteel second plate + a
third-metal buckle on the dummy and player, other player visibility, same-primary
secondary-metal-only swaps, save/rejoin, dismantle refunds, and neutral glove
backing. Pickup rendering remains native single-channel; this patch adds worn
section rendering, not a replacement pickup material system.

### Paired-side table clearance (`benchup2`)

The previous surface-ray check did not include the bench approach direction. Nail
selection now requires the exposed head direction to have at least 0.25 upward
projection in the oriented jig frame. Full-assembly blockers and the five hammer
clearance rays still apply. Three assemblies (metal left glove and both metal hip
halves) turn their COMPLETE construction by a quarter turn; other jigs retain
their orientation. Every input mesh, normal and nail pose receives the same jig
transform. The lowest construction point is raised to 6 cm above local jig y=0.
This affects construction only, never the accepted worn fit or original pack.
Run `tools/check_crafting_nail_access.py` for all 33 nails. The compatibility
signature now ends with `worn-sections1-benchup2`; update both sides together.
Actual table collider/hammer reach still needs confirmation in-game.

### Hebios integration history (now loot-only; see current policy below)

`tools/import_hebios.py` now imports the seven supplied ASCII FBXs into both runtime
packs, producing eight equipment slots. `crafting/hebios-runtime-manifest.json`
records triangle counts and native ingredient costs. All 24,154 triangles are
retained. The combined hips are partitioned by triangle centroid across x=0;
central belt/pennant geometry belongs to only one side, never duplicated.
Each side uses the same existing split-tasset convention: two primary plates
and a separate half-belt closure input (three inputs per side rather than
charging the entire five-input combined recipe twice).

- Results: **52855–52862**, after Wyrm 52846–52853 and stand **52854**.
- Recipes: **60024–60031**; pages: **60056–60063**. Wyrm recipe/page indices
  remain empty. All catalog, equipment, stand, save and wire bounds cover 32 items.
- The six non-hip source arrays, parent graphs, levels and nail flags use the
  supplied native Hebios metadata verbatim, retained in `hebios-jig-plans.json`.
- Centimetres/right-handed FBX coordinates are converted to Unity metres,
  including parent transforms, ZXY local rotations and XYZ pre-rotations.
  Rigid parts attach relative to named `ATTBone_*` helpers. Glove backing retains
  four-influence skin weights; its rest pose is baked from the supplied clusters
  and helper pose, not left in the original full-avatar coordinate space.
- Embedded PNGs are decoded completely, including the hips texture whose base64
  spans multiple quoted chunks. RGBA32 works on PC and through Quest's existing
  CPU texture path; original maps and material colors remain the basis for tint.
- Each mesh records its primary/closure owner. Front/back chest plates, helmet
  crown/cheek/nape, shoulder cap/sode, forearm/hand plates and tasset front/rear
  panels retain independent input material colors. Buckles and lacing follow
  closure inputs; flexible glove backing stays neutral.
- New nails are seated on the imported geometry, not the donor's folded jig.
  Helmet/chest jigs turn as complete assemblies for upper access. All **50**
  required nails across all 24 recipes pass the full-assembly geometric audit.
- Legacy Leather/Metal recipe-origin digests are retained, so adding Hebios does
  not invalidate saved provenance or refuse dismantling older crafted items.
- Existing Leather/Metal/Wyrm mesh and piece records, original textures/materials,
  and the original 16 construction records are byte-identical.

Rebuild with `.venv/bin/python tools/import_hebios.py`; it is idempotent and strips
only its own appended records. Development dependencies are in
`tools/crafting-requirements.txt`. Glyphs are generated from all 21 source PNGs by
`tools/build_crafting.py`. The shared crafting signature changes with this set;
update server and both clients together.

**Validation limit:** this is implemented source and generated runtime data,
not a tested game release. No C# compiler/complete Unity references are available
here. In-game dummy fit, skin deformation, collision/hammer access, save/rejoin,
mixed-material display and complete/disassemble transactions still need testing
on PC and Quest. Existing unrelated cosmetic-coverage/grip/rarity work is not
claimed fixed by this integration.

### Original Hebios appearance, withdrawal grace and leather dyes

- Uncrafted Hebios sends zero appearance hashes instead of coloring the worn set
  with its randomly assigned physical-material channel. Valid saved crafting
  provenance alone populates its per-input worn palette. Physical material still
  exists for native stats/save behavior. Hebios native `PhysicalMaterialPart`
  receives no renderer targets, preventing it from replacing the original pickup
  atlases with the default copper material. Metal armor's existing native
  renderer/material behavior is unchanged.
- Before a filled armor crafting piece is popped, its station receives a **one
  second** withdrawal grace period. Both native ingredient acceptance/insertion
  and the server's held-item auto-release respect that period. The initial
  dismantling refund gets the same protection before spawning. The grace applies
  to that station, not unrelated benches or native non-armor projects. Normal
  auto-placement resumes after the second; there is no permanent drop requirement.
- Leather now has triangle-to-input ownership maps, with the original geometry
  untouched. For leather inputs (47760 or 63204), worn soft-hide surfaces use
  the native `Township Tale/RampLerp3` three-color dye palette. The authored atlas
  is read on the GPU and tone-mapped using the undyed leather (33384) reference.
  Dye colors are baked into a cached atlas, preserving source alpha, UVs,
  normal/metal maps and the existing armor shader. Hardware and neutral Hebios
  glove backing are not converted into leather. Missing dyes retain the original
  appearance and retry once a second; cached materials/textures are destroyed on
  clear. Metal inputs continue using the accepted iron-relative coloring.
- No new source file or csproj compile entry is required. Update the modified
  shared files and PC/Quest files together. The crafting signature now ends in
  `surfaces2`, preventing older rendering code from joining a mismatched server.

These are source/data checks, not game validation. Verify uncrafted Hebios held,
stand-docked and worn; then craft mixed metal/leather inputs, equip through the
native dummy, save/rejoin, and compare dyes. Pull a piece off the workbench and
move it away during the one-second grace; confirm ordinary insertion still works
afterward. The independent worn palette is authoritative; this patch does not
add a new per-input network palette for loose completed pickup renderers.

## Current port: Wood crafting and Mushroom loot

This policy supersedes the historical Hebios crafting notes above:

| Set | Acquisition | Native book recipes |
| --- | --- | --- |
| Leather | Existing crafting and existing loot unchanged | 8 |
| Metal | Existing crafting and existing loot unchanged | 8 |
| Wyrm | Existing kill-only drops | None |
| Hebios | Loot-only, Epic global rarity | None |
| Wood | Crafting-only; no global loot entries | 8 |
| Mushroom | Loot-only, Rare global rarity | None |

Both new sets are imported from the uploaded FBXs into the existing runtime
packs (48 armor items total). Wood retains all **9,714** source triangles and
Mushroom all **28,298**. Each has eight equipment slots, with hips partitioned
between the two existing slots without duplicate triangles. Existing equipment,
stand, save and packet paths use the expanded catalog; no new runtime `.cs` file
or `.csproj` entry is required. Updates are consolidated into existing importer,
material, crafting and catalog files instead of introducing a file per feature.

- Wood result IDs: **52863–52870**, recipes **60032–60039**, pages **62032–62039**.
- Mushroom result IDs: **52871–52875, 62076, 52877–52878**. The stand remains **52854**.
- `armor recipes <player>` creates all 24 Leather/Metal/Wood pages. All 24
  recipes are published and appended to native collection **57634**, provider 2.
  This registers them for the book; it does not force-unlock every saved book.
- Native Woodland graphs/costs/levels/nail flags are retained in the existing
  combined `hebios-jig-plans.json`. Wood sources are **43836** (binding) and
  **47118** (wood). The combined hips become one binding plus two wood inputs
  on each side, following the existing split-tasset convention.
- Wood worn sections retain separate input ownership. Native wood textures,
  tiling, offsets and color replace only authored bark/heartwood surfaces;
  moss-hide and bindings retain their authored materials. Generated clones are
  cached, retried on missing assets and released on clear. Native physical
  material/provenance is set before crafted output replication and saved for
  dismantling, equipment and rejoin. Default/admin-spawned Wood remains authored
  until valid crafting provenance is present. New set protection values are
  conservative mod defaults, not claims about official game balance; the donor's
  standalone wood-settings API is not introduced as a separate dependency.
- Mushroom keeps its supplied textures and named helper attachments. One embedded
  texture is a Unity YAML RGBA32 asset, not a PNG; its raw pixel payload is decoded
  explicitly. Generated texture data is deduplicated across all FBXs, keeping
  each client pack around **42.4 MiB** instead of roughly 185 MiB.
- All **35** supplied glyphs share the existing glyph binary. Hebios and Mushroom
  receive item glyphs separately from recipe registration, without adding either
  set to the book. Loot-only sets are also refused by the dismantling path.
- Legacy Hebios construction data remains readable only for older saved material
  provenance/appearance; its recipes/pages are not registered. Remove obsolete
  Hebios recipe papers from test benches before updating. Leather/Metal saved
  recipe identities and their original 16 construction records remain unchanged.
- The one-second withdrawal grace and leather dye behavior remain in place.

Validation: **168 Python tests**, PC/Quest C# syntax parsing, byte-identical
regeneration of all generated resources, and full-assembly access for all **44
active crafting nails** (61 including legacy Hebios data) pass. These are not C#
compilation or Unity game tests. Verify book-page generation, Wood insertion and
completion/dismantling, mixed wood species on worn pieces, dummy fitting, stand
slots, loot generation and save/rejoin on both platforms before live deployment.
Update server and both clients together; the shared signature now identifies
Wood recipes and loot-only Hebios/Mushroom.

### Deployment correction: stale packs and native garlic collisions

A Quest log reporting `armor.pack has 32 pieces, expected 48` means an old
binary was embedded in the rebuilt DLL. Replace the local pack from
`quest/ArmorRestored.armor.pack` (PC: `pchost/armor.pack`), not just its build action.
Both current files are 44,493,907 bytes and have SHA256
`cccdcf42bc28fe9067aeaac5073e803ee750510e0ce69a6f9e0702bc7a5f6f1f`.
Use logical name `ArmorRestored.armor.pack`, remove local bin/obj build outputs,
and rebuild/redeploy. External file replacement alone cannot update an embedded
resource in an already-built DLL.

Native registry collisions observed in game: 52876 is Garlic Full Burnt and
60064 is Garlic Half Ripe. Mushroom right gauntlet now uses **62076**, and Wood
page items use **62032–62039**. Leather/Metal IDs and pages are unchanged. Old
52876 must not be interpreted as armor by either catalog. All page publication,
lookup and cleanup paths share the new mapping. These replacement IDs avoid the
reported collisions but are not certified free on every game/mod combination;
native collision checks remain enabled. Deploy updated PC/server and Quest
code together, then check `armor list` for refusals before use. Do not overwrite
native garlic entries or migrate garlic saves into armor.

## Release stabilization pass (in progress, not release-certified)

Confirmed scope: Wood remains crafting-only. Debug recipe commands affect only
this mod's 24 active Leather/Metal/Wood recipes, never vanilla/other-mod recipes.

Implemented in existing source files (no new runtime source/project entries):

- Loot rarity targets: Leather `2. Uncommon`, Mushroom `3. Rare`, Metal `4. Epic`,
  Hebios `5. Legendary`. Wood/Wyrm remain excluded from global loot. Missing native
  rarity lists are reported by `armor list`; these names/relative rates must be
  checked in the running server. Existing already-generated loot is not changed.
- Server-only native `PhysicalMaterialPart.Generate` interception rolls Copper or
  Iron equally for newly generated Metal armor only. Other game material
  distributions, saved items, and crafting inputs are untouched. Both native
  materials must resolve; otherwise a diagnostic reports native fallback.
  Explicit-material return spawns use DontGenerate to avoid rerolling saved gear.
- Leather now has a native saved/synced material channel. Neither Leather nor Wood
  passes renderer ownership to native material replacement: their authored maps
  remain under the existing cached surface system. Pickup polling uses original
  renderer materials rather than repeatedly cloning already-tinted variants.
  Worn Wood no longer suppresses a valid command-selected wood species. Binding,
  hardware and neutral hide exclusions remain. Mixed crafted loose-pickup section
  palettes are not added by this pass.
- Visibility eye bubbles are disabled, including stale local hidden-slot effects;
  the preference file is preserved for a possible future UI restoration.
- Stand registration requires a valid item for every slot, not every family.
  Dock bounds and allowlists include only successfully registered items, so an
  unrelated refused item no longer blocks an otherwise usable stand. No placement
  offsets or accepted armor geometry are changed. This does not establish that
  every native stand docking/spawning path works in-game.
- Native durability modules are configured on both platform templates with
  deterministic, non-chance-based settings. Broken armor remains a repairable
  item rather than being destroyed. Default per-set durability totals are
  Leather 600, Metal 1400, Wyrm 900, Hebios 1600, Wood 800, Mushroom 500, distributed
  by existing slot shares. Native loose items use the game's durability saving.
- The server carries worn condition in a backward-readable `~condition` suffix
  in worn.txt (legacy entries default to full condition), captures incoming
  condition before consuming equipment and restores it on returned items.
  Successful damaging impacts wear the struck armor by estimated absorbed damage;
  depleted pieces stop providing armor protection but retain identity, mass and
  appearance. Zero-health-change hits currently do not wear worn armor. Condition
  is saved on each damage event; high-load server profiling and native material
  multiplier/repair-shop integration remain follow-up work.
- Weight defaults now total Leather 6, Mushroom 5, Wood 8, Metal 14, Hebios 12,
  Wyrm 9, retaining split hips. These are mod policy values, not official balance.

### Debug/admin commands

- `armor recipeunlock`: persistently unlock all 24 armor recipes in the native
  server-wide GlobalPagesManager; replay learned armor pages into loaded native
  books. Registration also replays already-known armor pages when ready.
- `armor recipereset CONFIRM`: remove armor unlock keys and armor entries from
  loaded, closed crafting books only. Close all books and wait for animations
  first; back up the world. Reconnect clients afterward and save the world
  normally. Loose paper and unloaded saved books are **not** deleted, and may
  retain recipes. This is not a full-world recipe purge.
- `armor repair <player>`: admin repair of that player's currently worn armor.

### Still required before release

Actual C# compilation and PC/Quest playtests are unavailable here. Source syntax
and Python contract checks do not prove successful native component registration,
network synchronization, book saving, durability saving or visuals. Deploy all
platforms together and back up existing saves before testing this new behavior.
The configurable per-input Wood strength/averaging API is **not yet ported**.
The torso fabric fitting fallback is now implemented as described below. Existing
authored compact-LOD cosmetic fitting remains preferred; there is no blanket
hide/shrink workaround. Native surface-grip adaptation
also remains unimplemented. This pass must not be described as release-ready.


### Cosmetic fabric fitting fallback (PC and Quest)

Consolidated into the two existing CosmeticCoverage files. When chest armor is
shown, eligible two-color torso clothing without complete authored compact LODs
is fitted during native MeshPacker.Pack. Skin, head parts, held accessories,
texture-sharing-forced parts and natively impeded/disabled clothing are excluded.
Existing complete compact LODs continue to use native impeded arguments first.

The supplied reference envelope is adapted directly: torso-only z range
0.995–1.355, feathered edges, 8 mm clearance and at most 65 mm radial compression.
The 11-by-17 torso-radius table is retained. Fitting clones readable meshes and
native CosmeticMeshPart assets, preserving skinning, UVs, textures, colors and
other mesh channels. It substitutes a shallow copy of only this packing call's
part map, never modifies saved cosmetics, never scales armor and never edits a
shared source mesh. Removing armor, changing clothing or clearing coverage
unmarks the per-handler inputs and requests native repacking with original parts.
Quest uses native dictionary/interface/array conversions and pointer-keyed input
ownership rather than implementing a managed native cosmetic interface.

The cache is bounded to 96 cosmetic source assets. Cached clones deliberately
remain alive for the process lifetime: native packing can be asynchronous, so
LRU eviction/destruction could free a mesh still being read. At capacity,
unreadable meshes or an unsupported native API, original cosmetics are retained;
errors are diagnosed instead of hiding clothing. Temporary clones are destroyed
if construction fails. This is a torso fallback, not a guarantee that every
hat, sleeve, skirt or third-party cosmetic fits every armor piece. Test chest
clipping, garment changes, remove/re-equip, dummy views, remote players and
save/rejoin on both platforms. No in-game validation or C# build is claimed.

### Quest join-load mitigation

Quest now caps decoded armor maps and leather tint readbacks at 512 pixels on
both axes, budgets worn builds to one slot per frame across players/dummies,
and scans scene pickups every two seconds instead of twice per second. Empty
slots still remove immediately. Crafting registration is not skipped. Fabric
fallback waits eight seconds after finding a new customization manager and
installs its packing hook only when an eligible input exists. Detailed worn-map
and nail diagnostics require debug mode; lifecycle/errors remain logged.

These reduce texture memory/readback and avatar build spikes; they do not prove
that a reported native headset crash is fixed. Registration is still synchronous
and renderer counts are unchanged. The provided managed log contained no fatal
stack or OOM report; Android logcat/tombstone is needed to identify that cause.

Quest worn rendering and armor pickup-stat discovery now also wait for an active
local character/rig, followed by a ten-second settling window. The window resets
if that rig disappears or changes, and on disconnect. This is an avatar-readiness
heuristic, not proof that the loading screen has gone away. No armor renderers or
worn tint ticks are created by Draw during that window. Native prefab/recipe
registration still runs before join approval; postponing it until after admission
would break the existing handshake and risk unresolved network prefabs.
