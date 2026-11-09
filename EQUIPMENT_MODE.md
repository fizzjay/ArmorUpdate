# Native equipment-room adaptation (runtime verification required)

This adapts the supplied original `ArmorDummyInteraction` flow to ArmorRestored's
**eight** slots and existing server-authoritative equipment table. It does not
copy the original seven-slot `ArmorHarness` or its string/nonce protocol. It does
not create a new room, replace native cosmetics, or change fitted armor geometry.

## Implemented

- Resolve the local player's native `EquipmentState.CustomizationRoom` and use
  its active, owned `Dummy.Root` as the local display/interaction skeleton.
- Suspend targeting during invalid room transitions instead of falling back to
  equipping onto the body. Clear held/pending interaction state when rigs change.
- Display the server's equipped armor and material table on the dummy, including
  the helmet even when the first-person own-helmet setting is off. Remote players
  continue using their real character rigs. On exit the local display returns to
  the real character and the normal helmet setting.
- On hand release, allow up to one second for native pickup interaction state to
  become fully free. Cancel on re-grab, docking, destruction, expiry, disconnect,
  or a different target rig. Submit each pending item once; do not repeatedly send
  the existing non-idempotent equip request.
- Require an empty dummy slot and release within 0.24 m of its existing anchor.
  Existing close-grip/hold-to-remove controls target the displayed dummy skeleton.
  Keep native stand sockets higher priority than wearing armor.
- Keep normal body reach settings, persistence, material handling and wire-3 join
  refusal unchanged. The server still validates item identity and its existing
  2 m player-head proximity limit. This change does not authorize arbitrary distant
  dummy equips or add an unchecked client-supplied position.

## Files for the user's existing classic projects

Both projects: add `shared/EquipmentMode.cs` as a compiled linked/shared source
file (not an EmbeddedResource). Only Quest defines `ARMOR_QUEST`, as before.
The repository projects already include it.

- PC: update `pchost/ClientArmor.cs`.
- Quest: update `quest/Draw.cs`, `quest/Dressed.cs`, and `quest/Hands.cs`.

No pack/resource rebuild or framework migration is needed. Rebuild/install both
clients using matching game references. The server backend is unchanged.

## Acceptance checklist

The proprietary assemblies are absent here: Python source checks are **not** a
C# compilation or proof that the mode works. In particular, verify the native
`Player.Current`, `EquipmentState`, and room/dummy APIs against each platform.

1. Enter the game's existing equipment/customization mode. All currently equipped
   pieces should move to the preview skeleton, and its helmet should be visible.
2. Release each of the eight categories near its empty matching slot. The item
   should disappear only when accepted by the server, with the preview updated
   from the resulting table. Other slots must remain unchanged.
3. Release outside the capture radius, at an occupied dummy slot, while still
   held with the other hand, or near a stand socket: no unintended equip.
4. Re-grab during release, transfer hands, close/reopen the room, or disconnect
   during release: no stale or duplicated submissions.
5. Remove each equipped category using the existing allowed-hand, close-grip
   hold. Confirm the returned real pickup retains its physical material.
6. Exit equipment mode: the fitted body armor returns; first-person helmet rules
   return to their previous setting. Check another client sees the same equipment,
   then restart/rejoin and verify saved equipment/materials.
7. If the server reports an item is over 2 m away, capture that message and the
   room layout. Do not solve it by broadly disabling server proximity validation;
   a separate server-verifiable equipment-mode request would be needed.

The original guide/reticle presentation, native harness docking of actual worn
entities, and cosmetic-coverage repair are **not** implemented by this adaptation.
The invisible networked armor stand remains a separate unresolved issue.

## Quest native wheel toggle

`quest/EquipmentMenu.cs` exposes only `EquipmentModeQuickAccess` through a narrow
postfix on `QuickAccessMenuAction.IsIgnored`, while the ArmorRestored server gate
is open. Other action visibility and developer roles are not modified. Native
`IsValid`, downed restrictions, transitions, `Run`, `Open` and `Close` remain
untouched; a visible but disabled toggle is not forced to execute.

If the local owned controllers' regular wheel arrays omit the equipment action,
the helper clones an already-loaded native EquipmentModeQuickAccess asset and
appends it once to each local wheel. Existing entries and other mods' additions
are preserved. No replacement ScriptableObject or custom transition is invented
when the native asset is missing. Injected entries are removed on gate close;
assets are retained until their controller is destroyed so an open bubble does
not hold a destroyed action. Reopen the wheel after insertion.

For your classic Quest project, add **EquipmentMenu.cs** as a compiled source file
and copy the updated **Main.cs**, in addition to the equipment adaptation files
above. The repository Quest project already includes it. No PC menu patch is
needed. Look for `equipment menu:` logs for hook/insertion/asset-missing status.
Compile against your generated Quest assemblies first: the supplied menu sources
were PC Mono, not proof of identical Quest APIs or available room assets.

Runtime test: join a compatible server, reopen each local wheel and select the
native Equipment Mode bubble. Verify native entry/exit, dummy creation, armor
preview and all eight slots. Verify a downed player cannot activate it, other
wheel actions are unchanged, reconnect does not duplicate entries, and no injected
equipment toggle remains on an unrelated server. Existing QuestMods patches may
independently reveal actions; this helper does not undo another mod's changes.
