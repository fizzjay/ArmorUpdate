# Armor stand — PC/server and Quest

The supplied `armor_stand.obj` and `Stand_Atlas.png` are embedded as a separate,
platform-neutral resource. **Neither existing armor `.pack` file is modified.**
No Unity editor, Windows/Android asset bundles, SyncLib, or other armor mod is
required by this implementation.

## Build and use

1. Build **both projects** with the matching game's reference assemblies.
   `stand.bin` is already generated and included by both `.csproj` files; it is
   embedded automatically. Do not copy the OBJ/PNG/bin into the game's folders.
2. Install the rebuilt **0.4.0** mod on the PC server, PC clients and Quest clients.
   Wire protocol **3** rejects older clients, which lack the stand prefab.
   Keep the default server `join = refuse` compatibility check enabled.
3. In the **server console**, check `armor list`. It should include
   `52854  ATT_ArmorStand  ready`. Startup should also report
   `armor stand registered: ATT_ArmorStand number=52854 docks=8`.
4. Stand in front of a clear, level patch of ground, then run:

   ```text
   armor stand <player>
   ```

   For example, `armor stand scary`. The command places a fixed stand 1.5 metres
   in front of that player, facing them, with its base on the ground. It refuses
   missing/steep ground. It does not check for every possible overlapping object;
   choose a clear area.
5. Carry an armor pickup to its corresponding socket and release it. Grab the
   actual armor pickup to remove it. The eight sockets are helmet, chest, left
   and right shoulder, left and right gauntlet, left and right tasset. Each socket
   accepts its matching leather, metal and Wyrm items only, without stacking.

This is a **stationary spawnable scene object**, not a bag-sized pickup, crafting
recipe, ninth player slot, or quick-wheel equipment dummy. Use the dedicated
`armor stand` command; native generic spawn-command name lookup is not promised.
Existing worn-armor state and body fitting are unchanged.

## How it works

- `shared/ArmorStand.cs` is compiled into both clients, with a Quest conditional
  for native API/array access. Docks have the same eight embedded entity hashes
  (1–8) and item filters on both platforms.
- Native `PickupDock` components hold the **original armor entities**, not cloned
  visual proxies. Material data is not replaced or rewritten by stand code.
- Docks use one channel, stacking disabled, no physical joints, trigger bounds
  derived from the three styles' existing pickup colliders. Initial mount poses
  follow the supplied model; their visual alignment still needs a runtime check.
- Releasing armor inside a stand socket takes priority over body-proximity equip.
  The server refuses equip requests for armor already docked on a stand.
- The root uses native persistent save mode (`SaveMode = 0`) and the docks are
  native embedded network entities. **Save/reload and material preservation must
  still be verified in game.** No separate stand-save file is introduced.
- Number 52854 is outside the 24 existing armor IDs. Registration checks for
  collisions, publishes only after building the whole prefab, and reports a
  stand failure in the existing client/server registration handshake. Cleanup
  removes only this mod's registered stand entry and its resources.
- Dedicated servers build collision/docking only, not meshes or textures.

## Updating the model or atlas later

Keep the OBJ upright, in metres, with the base at y=0 (current model height:
1.467 m). OBJ faces must include UVs; normals are read if present, otherwise flat
face normals are generated. The PNG must be non-interlaced 8-bit RGB or RGBA.

From the repository root, run:

```sh
python tools/build_stand.py
python -m unittest discover -s tests -v
```

The converter needs only Python's standard library. It triangulates OBJ faces,
preserves positions and UVs, and decodes the atlas to Unity's bottom-up RGBA32.
Both projects embed the same resulting `armorstand/stand.bin`. If the stand's
shape changes substantially, update `Mounts` and the base/post collision in
`shared/ArmorStand.cs`; do not change the player armor meshes to fit the stand.

## Runtime verification checklist

The repository has no matching proprietary game assemblies or Unity runtime.
Source/asset tests and syntax parsing do **not** establish in-game compatibility.
Before using this on an important world, back it up and verify:

- PC server and both PC/Quest clients register the stand with eight docks;
  all clients see its textured model, including both Quest eyes.
- Spawn a stand; place/remove each of the eight slots for all three armor styles.
  Incorrect slots, ordinary items and a second item in an occupied socket must
  be rejected. Inspect shoulder, gauntlet and tasset display alignment.
- Deposit a non-default colored metal item; remove it on another client. Its
  entity/material should survive. Test while standing close to your own body
  so the body-equip fallback cannot steal a deposit.
- Simultaneous grab/deposit by two players must not duplicate or lose armor.
- Leave/rejoin, unload/reload the area and restart the server with a full stand.
  Check stand placement, all eight contents and metal material colors persist.
- Disconnect to a non-ArmorRestored server, then return: registration/cleanup
  should not leave stale stand prefabs or report a number collision.

Existing cosmetic coverage remains a separate runtime-unverified issue; this
addition does not claim to fix it.

### Quest registration follow-up

A Quest runtime test reached stand registration but failed at the initial
collision check: `PrefabManager.GetPrefab(52854)` throws KeyNotFoundException for
an unregistered ID, rather than returning null. Stand lookup now uses the native
map's `TryGetValue` on Quest (and the corresponding PC dictionary) both before
registration and when verifying publication. Rebuild with the updated shared
`ArmorStand.cs` on both platforms. Further docking/persistence testing is still
required; this fixes the observed lookup failure, not a verified full stand run.

### Invisible spawned stand diagnostics

A user runtime test confirmed terrain placement on MasterTerrain, stand registration
on both ends, and wire-3 join acceptance, but the stand remained invisible. This
is not yet resolved. The Quest read-only `stand probe:` log scans every five
seconds (including inactive objects), excludes the registered template, and logs
only changed instance summaries. It reports entity IDs, positions, activation,
parent/scale, mesh renderer state, bounds, vertex count, shader, and main-camera
layer inclusion. `armor standinfo <entityId>` reports server state without spawning
more objects. Update shared/ArmorStand.cs and quest/Main.cs for the client probe,
and pchost/ArmorCommands.cs for standinfo. Capture both outputs after spawning and
waiting at least five seconds; registration alone is not replication evidence.

### Missing root prefab link correction

A subsequent server probe found an active stand clone with attached prefab 52854
and eight embedded entities, but `NetworkEntity.prefab` / `entity.Prefab` was null.
The builder now explicitly assigns the root entity's prefab reference as well as
the prefab's entity reference, before activation or publication, and checks it
using platform-appropriate identity. The root local entity hash stays zero; dock
hashes remain 1..8. Update shared/ArmorStand.cs in **both** builds, restart both
ends, and spawn a fresh stand. Confirm standinfo now reports linkedPrefab 52854,
then check Quest instance probes and visibility. This corrects the observed setup
defect; successful replication, rendering, and docking still need runtime proof.
