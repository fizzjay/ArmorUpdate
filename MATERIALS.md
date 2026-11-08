# Armor materials

Metal armor prefabs now register a native `PhysicalMaterialPart` on material
channel 0 using `distributions/Metal Material Distribution`, matching the
EnoughIron approach. This enables the game's existing item-material system;
it does not introduce new armor item IDs or a crafting recipe/UI.

Both PC/server and Quest register the same additional network behaviour. Update
both builds together (version 0.3.0, wire protocol 2); old clients do not have
the matching prefab layout or worn-material table format.
Leather and wyrm pieces keep their original materials. The entire metal armor
pickup renderer is material-controlled (including any trim sharing that renderer).

The server records the material hash before consuming an equipped pickup and
restores it when removing, replacing, or stripping armor. `worn.txt` accepts
`piece_id@material_hash`; old entries without a suffix remain valid. Admin wear
commands use the default material. Back up worn.txt before downgrading, since old
versions cannot read the new suffix.

## Worn appearance (PC and Quest)

The server broadcasts a full 32-bit material hash for every worn slot, along
with its piece ID. Both clients apply it to rigid and skinned worn meshes.
A material-only swap rebuilds that slot, even when the piece ID is unchanged.
Late joiners receive the same full snapshot as existing clients.

`WornMaterials` resolves the exact `PhysicalMaterial` hash from the game's
registry and calls `GetNonAtlasedNonDeformingMaterial(0)`, following the palette
implementation supplied by the user. It samples cold albedo on the client,
including packed `_ColorA`/`_ColorB` metal shaders (blend factor is texture green).
An 8x8 linear GPU downsample supports non-readable native textures. The saved
RenderTexture is restored and temporary GPU/CPU textures are released in finally.
Shader colors use the source implementation's linear-color-space conversion.

Sampled RGB is divided by the sampled iron baseline (floor 0.01 per channel,
ratio clamped 0..2). This retains the armor atlas's original iron appearance for
iron and applies the other metal's relative color over its detail. Cached clones
keep the original shader, texture and normal maps, with opaque tint alpha.
Unavailable assets retry once per second and update existing cloned materials,
without rebuilding fitted meshes or requiring another equip. Successful raw
samples/tints are cached until disconnect. Clones are destroyed on clear.

**The native method calls and GPU sampling still need matching-game compilation
and Quest runtime validation.** Source-contract tests are not that validation.
This replaces the earlier speculative field/property visual lookup, which failed
for hash 31468 in the user's logs.

Material-specific protection is not implemented. Protection continues to use
the existing armor-set settings. Material changes do not add crafting recipes.

The changed table uses message kind 13 rather than the old kind 10, preventing
old clients from accidentally parsing material bits as piece IDs. Both sides
advertise and check protocol 2; install matching updates on server, PC and Quest.

## In-game verification needed

This checkout does not contain the proprietary game/Unity/MelonLoader reference
assemblies or a build toolchain. Compilation and multiplayer behavior must be
verified with the matching game installation:

1. Build and install the PC/server and Quest mods with the game references.
2. Confirm all armor pieces register and no material-distribution errors appear.
3. Use the game's material-aware spawn tooling to spawn two metal pieces with
   different channel-0 materials; check their pickup appearance on both clients.
4. Equip each; inspect it on both a PC and a Quest client, including helmet,
   rigid pieces and skinned chest meshes. Check there are no visual-lookup
   warnings. Swap two pieces with the same ID but different materials; the worn
   appearance must change. Remove each; confirm its material survives. Replace armor in
   the same slot and use `armor strip` to check the dropped materials.
5. Restart the server while wearing non-default metal armor, then remove it;
   check the saved material survives and old worn.txt entries still load.
6. Join with a second PC/Quest client after armor is already worn and confirm
   appearance matches. Disconnect/rejoin to check rebuilt visuals.
7. Confirm leather/wyrm armor and ordinary item materials are unaffected.
8. Attempt to connect a protocol-1 client/server and verify the mismatch is
   reported instead of accepting the changed table.

## Automated source-contract checks

Run `python -m unittest discover -s tests -v`. These check project inclusion,
PC/Quest helper parity, protocol checks and serialization wiring, both rendering
paths, material-only refresh, and persistence integration. They are not game
runtime tests or a substitute for a C# build.


## Quest rebuild notes

Build the complete `quest/ArmorRestored.csproj`, not just Registry.cs or a
partial selection of decompiled files. `ArmorMaterials.cs` and `WornMaterials.cs`
are both required and are explicitly included in that project. If using a DLL
editor instead, import both helpers in the `ArmorRestoredQuest` namespace too.

Use a .NET 8-capable build setup and the matching Quest MelonLoader/IL2CPP Unity
references. Do not mix PC's managed Unity/game assemblies into the Quest build.
The repository's framework reference HintPaths may need changing to your local
installation. A missing `ArmorMaterials` error indicates that helper was not
included in the build (or was imported into the wrong namespace).

Quest decompiler cleanup disambiguates managed scratch collections from the
native game registries, qualifies IL2CPP array factories, aliases Unity Object,
and restores typed Unity texture/hide flags. Invalid compiler-generated delegate
names are replaced with ordinary delegates. The native exit callback is rooted
for its lifetime and marshaled with Cdecl rather than using decompiled `ldftn`
or `UnmanagedCallersOnly` syntax. Both project files explicitly embed the pack
under the exact name expected by their loaders.


## PC build warnings and references

The PC project now targets x64 in Debug and Release to match the reported AMD64
MelonLoader reference. This setting is PC-only; it must not be applied to Quest.

Reference only the assemblies the mod needs, plus dependencies required by the
compiler, rather than importing the entire game's Managed directory. The
repository PC project lists the starting set. In particular it does not directly
reference `mcs`, IdentityModel, or Microsoft.Bcl.AsyncInterfaces. Remove an unused
direct `mcs` reference from a custom project: its x86 architecture cannot be fixed
by changing the entire PC mod to x86 when its loader is x64.

A Microsoft.Bcl.AsyncInterfaces version conflict is separate from source compile
errors. A Reference Include claiming version 10 while its HintPath resolves to an
assembly of version 1 is misleading metadata. Remove an unnecessary direct
reference or correct it to the actual DLL identity; that alone may not resolve a
transitive System.Text.Json dependency on version 5. Use a consistent set of
references from the same supported game/loader installation and check which
versions the host actually loads. Do not overwrite game DLLs, copy arbitrary
NuGet versions into the game's Managed directory, or assume a binding redirect
in a mod DLL config can control the Unity host. Set Copy Local=False on external
game/loader references to avoid accidentally packaging them with the mod.


PC native material setup uses reflection for the non-public backing members that
Quest exposes as public IL2CPP properties. It walks base types to find inherited
fields/properties as well. The member names still need to exist in the installed
game build: if registration logs a MissingMemberException, send that full error
and the PhysicalMaterialPart/NetworkEntity declarations from your game DLLs.
Reflection removes the compile-time accessibility problem; it does not prove
that a different game version has the same runtime layout. An unexpected
network-behaviour collection type is rejected instead of discarding behaviours.

The loot diagnostic reports each named AllowedResult rather than comparing the
non-nullable enum to null. GetLoot's output count uses `out`. PC string-encoding,
texture-format and mesh-index-format values use explicit enum types. The
ServerSettings obsolescence warning remains: it is not a compilation error and
switching damage diagnostics to a replacement API needs its actual game signature.

### Worn alignment diagnostics

PC and Quest now prefer the native `Baked` avatar renderers' bone arrays over
name matches found beneath the customization root. This prevents a same-named
dummy/reference joint from taking precedence over the joint used to render the
body. The fallback hierarchy scan is retained for joints not present in a body
renderer. Armor renderers are not used to seed the map.

On first wiring, the client logs `armor rig on ...: N bone bindings from baked
avatar renderers`. If N is zero, or armor remains offset, report that line and
whether the offset changes while bending the wrist or turning the torso. This
change does not recalibrate pack bind poses or prove that the pack's original
rest skeleton matches the current game's rest skeleton; in-game alignment still
needs verification. No manual offsets or gameplay/persistence changes are made.

### Metal plates following the leather skin

The native-bone preference did **not** resolve the user's metal misalignment.
The next candidate correction re-rigs the metal chest, shoulders, gauntlets and
tassets to the existing leather skin, rather than parenting their plates directly
to joints. This is a deformation change, not a claim of verified in-game fit.

`tools/skin_metal.py` converts 60 rigid worn metal parts into separate skinned
mesh records in both embedded packs. It recovers authored model-space rest
vertices using `inverse(leatherBindPose[bone]) * rigidLocal`, then transfers each
vertex's nearest leather vertex's four bone influences and the reference bind
palette. Chest/shoulders/gloves use the corresponding leather slot's largest
skin. Tassets use the leather chest's lower-torso surface, since leather tassets
are themselves rigid. This avoids computing a bind pose from a moving avatar
and allows the plates to follow the same blended joints as the working leather.
Metal can now flex with the body; weight transfer may need artistic refinement
if a seam or plate bends unnaturally. It is not a manual weight-painted rig.

Original mesh records are retained for pickup visuals. Already-skinned glove
linings, all leather/wyrm worn records, textures, materials, pickup references,
and colliders are unchanged. The seven metal helmet parts remain rigid and
unchanged: the leather helmet has no skinned head surface to transfer from.
**This patch must not be described as a confirmed fix for all metal armor.**

Both clients embed identical updated pack bytes. Rebuild Quest/PC client DLLs
with their updated embedded resource and fully restart before testing. Updating
only loose pack files does not change an already-built DLL. No protocol or
server persistence change is needed. First test chest and both gloves while
turning, crouching and curling fingers, then shoulders/tassets; inspect remotely
as well as locally. Helmet fitting is still unresolved if it is displaced.

Tests exercise the binary resources: 60 conversions, unchanged original assets,
valid normalized skin weights, reference bind palettes, rest-space transforms,
real changes to bone influences, matching client packs, nearest-vertex search,
and idempotent conversion. These tests are not Unity rendering validation.

### Confirmed fitting / unresolved Quest tint lookup

The user reports that the re-skinned metal now fits. Do not undo the pack or
adjust working leather to troubleshoot color. Quest logs show the updated pack
(285 meshes), and specifically `No visual material/color exposed for physical
material 31468`. The material hash reaches the worn renderer; this failure is in
visual-data discovery, not evidence that the saved item material was lost.

The initial nested-member reflection candidate has now been superseded by the
user-supplied native palette path described above. Native cold-albedo method
lookup, packed metal color sampling, iron-relative tint and retry-in-place replace
that reflection code on both platforms. No placement or pack data changed.

### Equipment-dummy integration reference (not implemented yet)

The user supplied ArmorServer/ArmorHarness code confirms native
EquipmentState.remoteDummyEffects as the active remote dummy anchor. Their
harness accepts dummy equips only with an active session, live non-spirit owner,
matching released armor, empty slot and finite positions within dummy/hand reach;
its dock transaction rechecks ownership and reach. This is a useful reference,
not a drop-in implementation: their harness persists live docked pickups in seven
slots; this repository persists piece/material hashes in eight slots. Do not
replace its save format or slot mapping by copying those classes wholesale.

The supplied ArmorMaterialAppearance/ArmorMaterialMesh also require semantic UV
metadata and mesh section support absent from our version-1 pack. Our single
material hash per piece needs only the cold palette sampler, not those mesh
rewrites. In particular preserve the new fitted skin weights and bind poses.

The client code constructing EquipmentSlot.Point/Attach and rendering/interacting
with the local quick-wheel dummy remains missing. No dummy equip/removal UI has
been installed in this patch; current body-proximity interaction stays intact.

### Cosmetic coverage (native compact meshes)

PC and Quest now maintain a per-avatar `CosmeticCoverage` overlay alongside worn
rendering. It resolves the native PlayerCharacterCustomizationManager from typed
character/handler members, reads its internalManager/items (fields on Mono,
generated properties supported on Quest), and applies temporary TwoColors with
IsImpeded=true to each active native handler. Native packing uses the cosmetic's
authored ImpededLods, with its original primary/secondary colors preserved.

Coverage uses this repository's shown eight-slot order, **not** the native
EquipmentSlot enum values or the reference mod's seven-slot mask. The two tasset
bits both cover the shared lower-body cosmetic slots; coverage remains while
either is shown. Local hidden helmets do not compact local head cosmetics.
Entries are reevaluated on each dress scan, so cosmetic changes and repacks can
be handled without re-equipping. Removal, rig replacement, avatar cleanup and
session clear release owned overlays. Restoration checks the actual packed
argument identity and does not overwrite an unrelated native/mod change.

Safety boundaries: only PlayerCosmeticDefinition clothing with TwoColors and a
complete authored compact LOD set is supported. SkinMeshPart, forced texture
sharing, disabled entries, and non-clothing accessory slots are excluded. No
whole Baked renderer is disabled, no saved customization argument is mutated,
and no armor geometry, tint, persistence or equip protocol is changed. Cosmetics
without compact LODs remain unchanged: the reference's additional custom fabric
shrink/packing path is **not** implemented. Thus this is not a promise to hide
every possible cosmetic or eliminate all clipping.

Rebuild client DLLs and restart. Validate covered clothing before/after equipping
each slot, both tassets independently, cosmetic changes while wearing armor,
remote avatars, respawn and reconnect. Look for `Cosmetic armor coverage deferred`
if the installed native API differs. Source-contract tests cover registration,
slot ranges, cleanup and safety guards; matching-reference C# compilation and
Unity/Quest runtime validation are still unavailable in this checkout.

Equipment-dummy interaction remains a separate unfinished request. The supplied
ArmorClient now identifies ArmorDummyInteraction as the relevant client class;
EditableSlot handles native CustomizationPickup objects, not these armor pickups.
No native cosmetic slot triggers or saved customization definitions are replaced.

### Compilation follow-up: native enumerable interfaces

PC CosmeticCoverage imports Alta.Character for PlayerCharacter. Quest registry
All and customization Handlers are generated native IEnumerable<T> interfaces,
whose IEnumerator<T> proxy does not expose inherited MoveNext. NativeEnumerable
bridges iteration with a cast to Il2CppSystem.Collections.IEnumerator for MoveNext,
reads typed Current on the generic proxy, and disposes the native enumerator.
ChangeArguments now receives a native List<ICustomizationHandler> cast to native
IEnumerable<ICustomizationHandler>, rather than a native array that does not
implicitly implement the expected interface in these generated references.
This addresses the user's reported compile errors; the matching DLL references
are still not present here, so successful C# compilation is not claimed.

### Quest runtime follow-up: enumeration failures block tint and coverage

The user's Quest log confirms both new paths run, but tint 31468 fails inside
native Dictionary.ValueCollection.Enumerator.MoveNext and cosmetic coverage
fails inside native HashSet.Enumerator.MoveNext with "Collection was modified".
Neither feature is runtime-verified working. Server equip/remove succeeds and
these exceptions are client presentation failures, not evidence of lost hashes.

The generic-to-nongeneric enumerator bridge has been removed. Material lookup on
both clients now uses Get(hash) with an exact physical.Hash check (no All scan).
Quest customization handler reads copy the native ICollection into a native
reference array and then a managed array, with no generated struct enumerator.
Quest cosmetic entry groups use ValueCollection.CopyTo and indexed list reads.
Packing/ChangeArguments happens after snapshot reads, not during a live handler
walk. Copy failures still abort/defer rather than applying a partial snapshot.

Quest manager identity is now compared by native Pointer, not CLR wrapper
reference. New generated wrappers for the same manager must not clear coverage,
drop owned overlays or reset the one-warning guard every half-second. This also
addresses the repeated warnings seen in the user's log. Fitting resources, native
compact-mesh eligibility, shader sampling, persistent hashes and gameplay are
unchanged. Rebuild/restart the Quest client to test; a server-only DLL replacement
cannot fix these client errors. PC clients should rebuild for the keyed lookup.

Source-contract tests prohibit native enumerator use in this bridge and require
snapshot CopyTo, exact hash lookup and native manager identity. These are not
matching-game compilation/runtime tests. Check for successful "worn cold-albedo
tint resolved" and absence of repeated "coverage deferred" errors after testing.


### Follow-up: neutral glove backing / coverage diagnosis

Quest testing after 253fd36 confirms physical-material colors resolve, but cosmetic
coverage still has no visible effect. Coverage is **not verified fixed**. Both
clients now log a deduplicated `cosmetic coverage: mask=... entries=... handlers=...
eligible=... packedInputs=...` summary with skip-reason counts. This distinguishes
slot/filter exclusions, unavailable compact LODs and missing packer inputs without
hiding skin or changing saved cosmetics. A new runtime log is needed to choose a
safe next fix; clothing without authored compact LODs still needs a fabric-packing
implementation.

The two `metal_gauntlet_*_glove_flair_LOD0` backing meshes now bypass physical-metal
tinting. Their original packed `metal_glove_m` material is neutral grey
(R=G=B=0.25735295); plates still receive the selected physical-material color.
Mesh names are passed through both PC material-array and Quest material paths.
No placement, leather, mesh resources, or persistence behavior changed.
