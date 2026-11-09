using System;
using System.Collections.Generic;
using UnityEngine;
#if ARMOR_QUEST
using Il2Cpp;
using Il2CppAlta.PlayerStates;
using Il2CppAlta.Networking.Scripts.Player;
namespace ArmorRestoredQuest
#else
using Alta.PlayerStates;
using Alta.Networking.Scripts.Player;
namespace ArmorRestored
#endif
{
    // The native room owns its dummy. Never create a second customization room.
    internal static class EquipmentMode
    {
        internal static Transform Resolve(Transform bodyRig, out bool dummy)
        {
            dummy = false;
            var player = Player.Current;
            if (player == null || player.StateManager == null) return bodyRig;
#if ARMOR_QUEST
            var current = player.StateManager.CurrentState;
            EquipmentState state = current == null ? null : current.TryCast<EquipmentState>();
#else
            EquipmentState state = player.StateManager.CurrentState as EquipmentState;
#endif
            if (state == null) return bodyRig;
            // During room transitions, do not silently target the real body.
            dummy = true;
            var room = state.CustomizationRoom;
            if (room == null || room.Player == null || !room.gameObject.activeInHierarchy
                || room.Dummy == null || room.Dummy.Root == null
                || !room.Dummy.Root.gameObject.activeInHierarchy) return null;
#if ARMOR_QUEST
            if (room.Player.Pointer != player.Pointer) return null;
#else
            if (!object.ReferenceEquals(room.Player, player)) return null;
#endif
            return room.Dummy.Root;
        }

        internal static bool Same(Transform a, Transform b)
        {
#if ARMOR_QUEST
            return a != null && b != null && a.Pointer == b.Pointer;
#else
            return a != null && a == b;
#endif
        }
    }

    // Poll the native release state for up to one second. Hand polling can see
    // the hand let go before Pickup.Interactors has finished updating.
    internal sealed class EquipmentReleases
    {
        private sealed class Pending
        {
            internal Pickup Item;
            internal Transform Rig;
            internal float Until;
        }
        private readonly Dictionary<uint, Pending> pending = new Dictionary<uint, Pending>();
        internal void Clear() { pending.Clear(); }
        internal void Holding(Pickup item)
        {
            if (item != null && item.Entity != null) pending.Remove(item.Entity.Identifier);
        }
        internal void Released(Pickup item, Transform rig)
        {
            if (item == null || item.Entity == null || item.Entity.Identifier == 0U || rig == null) return;
            pending[item.Entity.Identifier] = new Pending { Item = item, Rig = rig, Until = Time.unscaledTime + 1f };
        }
        internal void Tick(Transform rig, Action<Pickup> attach)
        {
            foreach (uint id in new List<uint>(pending.Keys))
            {
                Pending entry = pending[id];
                Pickup item = entry.Item;
                if (!EquipmentMode.Same(rig, entry.Rig) || item == null || item.Entity == null
                    || item.Entity.IsBeingDestroyed || item.Dock != null || Time.unscaledTime > entry.Until)
                { pending.Remove(id); continue; }
                if (item.IsInteractedWith || item.Interactors.Count != 0) continue;
                // Remove before callback: no duplicate submit for two-handed releases.
                pending.Remove(id);
                attach(item);
            }
        }
    }
}
