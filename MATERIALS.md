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

`WornMaterials` resolves a Unity material or color/tint from the game's
`PhysicalMaterial` asset. Field/property reflection is used because PC has
managed game fields while Quest has generated IL2CPP properties. Only the color
is borrowed from a native Unity material (`_BaseColor` or `_Color`); its shader
and textures never replace the armor atlas. Cached tinted copies of the original
armor-pack materials preserve the original textures and normal maps. Tint alpha
is kept opaque so physical-material color alpha cannot hide worn armor.
Those copies are destroyed when disconnecting/clearing armor.

**The visual-member lookup is not verified against game DLLs in this checkout.**
It currently recognizes material/renderMaterial/unityMaterial/visualMaterial
and color/colour/tint/materialColor/materialColour/mainColor/baseColor/displayColor.
If the installed game exposes visual data differently, it logs
`No visual material/color exposed for physical material ...` once per hash and
keeps the pack appearance rather than breaking armor. That lookup will need
adapting to the actual game API if the warning occurs; in-game verification is
required before claiming every material renders correctly.

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

The visual resolver now supports nested visual settings (bounded depth, cycle
protection), typed Color/Color32 members with nonstandard names, additional base
tint shader properties, and native `TryCast<Material>` for IL2CPP object proxies.
Individual reflection getter failures no longer stop the remaining candidates.
Only the discovered color is copied to the armor material, never native textures.
If discovery still fails, the log includes physical-material member names/types
so the installed game's actual API can be identified without another blind
name-list guess. This is a candidate lookup fix, not verified against game DLLs.

The user's supplied EquipmentSlot/EquipmentPartPickup classes are mod-side slot
and release bookkeeping, not sufficient evidence of a native game API. Replacing
body-proximity equip with mannequin/customization equipment interaction needs
confirmation of the intended UI and its slot-creation/controller integration.
No equip, removal, persistence or placement behavior is changed by this lookup
patch.
