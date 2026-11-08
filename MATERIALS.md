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
managed game fields while Quest has generated IL2CPP properties. Native Unity
materials are reused without modification. For color-only assets, cached copies
of the armor-pack materials preserve the original textures and normal maps.
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
