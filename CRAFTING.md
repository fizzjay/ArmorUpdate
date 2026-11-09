# Armor crafting / deconstruction port

## Agreed scope

- Leather and Metal: native workbench recipes and material-preserving deconstruction.
- Wyrm: **kill loot only; no crafting recipe**. Do not infer a Wyrm recipe from
  the reference DLL's Wyrm assets. The new server `WyrmArmorLoot` hook targets
  `Wyrm.Killed(HealthObject)`, checks an alive-to-dead transition and the four
  native Wyrm prefab hashes from the reference. It retains the reference's 5%
  drop chance, selecting uniformly among this mod's eight slots. Ordinary and
  crystal Wyrms drop this mod's existing Wyrm set (there is no separate crystal
  set here). Requires `loot = on` and `wyrm_loot = on`; existing explicit `off`
  settings are respected. Global-table registration now includes only Leather
  and Metal. Existing saved/admin-spawned Wyrm pieces are not deleted.
- Preserve current worn armor meshes, rigging, placement, textures, dummy-only
  equipment, and saved local visibility choices.
- Preserve eight independent slots, including left/right tassets.

## Reference verified

User-supplied `testothermod/PlayerArmor.CraftingHouse.dll`:

- DLL SHA-256: `13FE26BB526EC1C25F0A874DCC47DC32027B91DB4BDC0FC04FE7A206D44C306C`.
- Embedded `PlayerArmor.Model`: 8,883,090-byte UnityFS asset bundle,
  header Unity 2020.3.23f1.
- Model SHA-256: `119EDC0C48D29C7DBA66F5EA3CC7537E17601BF32184E4EDDEACE6716E897B4A`.
- Embedded `PlayerArmor.JigPlans`: schema 1, 35 entries; its model digest matches.
- All 14 repository Leather/Metal glyphs match the embedded glyphs byte-for-byte.
- Contains crafting, recipe reference/version handling, material provenance,
  saved craft records, prefab registration, and disassembly methods.
- References MelonLoader 0.7 and SyncLib. This is not a drop-in replacement for
  either of our current clients; do not install it alongside ArmorRestored as
  the integration strategy.

Reproduce without executing the DLL:

```sh
python -m venv .venv
.venv/bin/pip install dnfile
.venv/bin/python tools/inspect_crafting_reference.py
# Optional scratch extraction outside runtime resources:
.venv/bin/python tools/inspect_crafting_reference.py --extract /home/user/crafting-reference
```

The inspector checks model/archive identity, schema, unique plan names,
parallel array lengths, ingredient-count bounds, and local glyph equality.
It does not validate mesh semantic labels or game API compatibility.

## Still required before enabling crafting

1. Adapt authored construction geometry into platform-neutral resources without
   modifying the accepted worn-armor pack. Verify UV-channel semantic metadata.
2. Adapt combined tasset construction plans to separate left/right results and
   costs; do not duplicate a combined recipe onto both slots.
3. Port native recipe registration/pages, construction hierarchy, material
   validation/transfer, and server-authoritative completion into this mod's
   prefab registry. Check current PC and Quest API signatures.
4. Add saved ingredient provenance and stable recipe versions. Validate native
   serialization strategy on IL2CPP; reject unsupported mixed mod versions.
5. Port deconstruction transaction/rollback and cleanup, including saved reload,
   partially filled projects, duplicate requests, and inventory failure cases.
6. Runtime-test the new Wyrm kill hook. No Wyrm recipe must enter registration,
   recipe books, random pages, or saved recipe history.
7. Compile both clients with matching game references and test crafting,
   deconstruction, save/load, and multiplayer on PC and Quest.

## Implemented construction/persistence foundation

- `tools/build_crafting.py` verifies the model/archive pair and exports the 14
  Leather/Metal pickup plans into a 762,658-byte platform-neutral resource.
  Every triangle is assigned using authored UV4 role/group tags; mixed tags,
  unknown markers, non-finite geometry, absent groups, and bad hierarchies fail
  export. Original submeshes and normal/UV data are retained. Neither worn
  armor pack is rewritten. All 14 actual source meshes passed these checks.
- `crafting/construction-manifest.json` records the source/model and output
  digest, input prefab hashes, and per-ingredient vertex counts. Two combined
  tasset plans are explicitly reference-only, NOT registered as split recipes.
- `shared/ArmorCraftingData.cs` is included by both projects, and both embed the
  same construction resource. The bounded reader validates allocation budgets,
  finite values, indices, rotations, ingredients, and acyclic hierarchies. It
  refuses Wyrm/unknown styles; `Find` never exposes the combined tasset plans.
- `ArmorCraftOrigin` provides a bounded, versioned, checksummed binary codec for
  result identity, a full 32-byte immutable recipe digest, and up to 32 original
  ingredient prefab/material pairs. Constructor arrays are copied. This is not
  yet attached to a native saved component; codec availability does not mean
  crafting provenance is being saved in game. SHA-256 detects corruption; it is
  not a signature and does not replace server-side validation.

Rebuild (development dependencies only; not mod runtime dependencies):

```sh
.venv/bin/pip install -r tools/crafting-requirements.txt
.venv/bin/python tools/build_crafting.py
```

**Status: construction export and provenance codec implemented; native
crafting/deconstruction is NOT registered or enabled yet.** The workbench
adapter, native saved component, and transaction rollback still need porting.
Wyrm kill-only behavior has a server implementation but has NOT been compiled
against the game or runtime-tested. Python tests validate the export, binary
layout, and source contracts, not native crafting/loot behavior.
