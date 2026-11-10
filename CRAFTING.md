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

- **Leather, Metal and Hebios:** eight separate native recipes each, including independent
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
  all 24 physical pages near that player. Pages are 60032–60047 (Leather/Metal)
  and 60056–60063 (Hebios); Wyrm entries remain unused.
- Put a recipe page on the workbench, add the displayed ingredients, hammer the
  native nails, then grab the completed armor. Primary ingredients may use different physical materials. Every input retains
  its own material provenance.
- To dismantle Leather/Metal/Hebios, remove the paper and clear the bench, then release
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

Leather/Metal recipes occupy 60000–60015; Hebios recipes occupy 60024–60031.
Glyphs occupy 52830–52845 and 52855–52862
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

### Hebios integration

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
