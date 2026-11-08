using System;
using System.Collections.Generic;
using System.Reflection;
using Alta.Customization;
using UnityEngine;

namespace ArmorRestored
{
    // Own only transient native "impeded" arguments, never the player's saved cosmetics.
    internal sealed class CosmeticCoverage
    {
        private PlayerCharacterCustomizationManager manager;
        private readonly Dictionary<CustomizationSlotEntry, Dictionary<ICustomizationHandler, TwoColors>> owned =
            new Dictionary<CustomizationSlotEntry, Dictionary<ICustomizationHandler, TwoColors>>();
        private bool warned;
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal void Update(Transform root, int[] shown)
        {
            try
            {
                PlayerCharacterCustomizationManager next = root == null ? null : ManagerOf(root);
                if (manager != next) { Clear(); manager = next; }
                if (manager == null)
                {
                    if (!warned && Array.Exists(shown, piece => piece >= 0))
                    {
                        warned = true;
                        Mod.Log.Warning("Cosmetic armor coverage deferred: customization manager not found on character/handler.");
                    }
                    return;
                }
                uint mask = 0U;
                for (int i = 0; i < 8 && i < shown.Length; i++)
                    if (shown[i] >= 0) mask |= 1U << i;
                HashSet<CustomizationSlotEntry> current = Entries();
                List<ICustomizationHandler> handlers = new List<ICustomizationHandler>();
                foreach (ICustomizationHandler handler in manager.Handlers)
                    if (handler != null && !handler.IsDestroyed) handlers.Add(handler);
                foreach (CustomizationSlotEntry entry in new List<CustomizationSlotEntry>(owned.Keys))
                {
                    if (!current.Contains(entry) || !Covered(entry, mask))
                    {
                        Restore(entry, owned[entry], current.Contains(entry), handlers);
                        owned.Remove(entry);
                    }
                }
                foreach (CustomizationSlotEntry entry in current)
                {
                    if (!Covered(entry, mask) || entry.DisablerCount > 0) continue;
                    TwoColors original = entry.Arguments as TwoColors;
                    Dictionary<ICustomizationHandler, TwoColors> overlays;
                    if (!owned.TryGetValue(entry, out overlays))
                    {
                        overlays = new Dictionary<ICustomizationHandler, TwoColors>();
                        owned.Add(entry, overlays);
                    }
                    foreach (ICustomizationHandler stale in new List<ICustomizationHandler>(overlays.Keys))
                        if (!handlers.Contains(stale)) overlays.Remove(stale);
                    foreach (ICustomizationHandler handler in handlers)
                    {
                        CosmeticPacker.Input input = Input(entry, handler);
                        if (input == null) continue;
                        TwoColors previous;
                        overlays.TryGetValue(handler, out previous);
                        if (original.IsImpeded)
                        {
                            if (previous != null && input.Arguments == previous)
                                Change(entry, handler, original);
                            overlays.Remove(handler);
                            continue;
                        }
                        if (previous != null && input.Arguments == previous &&
                            previous.Primary == original.Primary && previous.Secondary == original.Secondary) continue;
                        TwoColors compact = new TwoColors { Primary = original.Primary,
                            Secondary = original.Secondary, IsImpeded = true };
                        // Record before changing, so a partial failure can still restore.
                        overlays[handler] = compact;
                        Change(entry, handler, compact);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!warned)
                {
                    warned = true;
                    Mod.Log.Warning("Cosmetic armor coverage deferred: " + ex.GetBaseException().Message);
                }
            }
        }

        private static PlayerCharacterCustomizationManager ManagerOf(Transform root)
        {
            PlayerCharacterCustomizationManager manager = ManagerOn(root.GetComponent<PlayerCharacter>());
            if (manager != null) return manager;
            foreach (PlayerCustomizationHandler handler in root.GetComponentsInChildren<PlayerCustomizationHandler>(true))
            {
                manager = ManagerOn(handler);
                if (manager != null) return manager;
            }
            return null;
        }

        private static PlayerCharacterCustomizationManager ManagerOn(object target)
        {
            if (target == null) return null;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(Members))
                    if (field.FieldType == typeof(PlayerCharacterCustomizationManager))
                    {
                        var manager = field.GetValue(target) as PlayerCharacterCustomizationManager;
                        if (manager != null) return manager;
                    }
                foreach (PropertyInfo property in type.GetProperties(Members))
                    if (property.PropertyType == typeof(PlayerCharacterCustomizationManager) &&
                        property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        var manager = property.GetValue(target, null) as PlayerCharacterCustomizationManager;
                        if (manager != null) return manager;
                    }
            }
            return null;
        }

        private static void Change(CustomizationSlotEntry entry, ICustomizationHandler handler, CustomizationArguments colors)
        {
            entry.Part.ChangeArguments(new ICustomizationHandler[] { handler }, colors);
        }

        private HashSet<CustomizationSlotEntry> Entries()
        {
            CustomizationVisualManager visuals = Read(manager, "internalManager") as CustomizationVisualManager;
            if (visuals == null) throw new InvalidOperationException("Native cosmetic visual manager unavailable");
            var items = Read(visuals, "items") as Dictionary<uint, List<CustomizationSlotEntry>>;
            if (items == null) throw new InvalidOperationException("Native cosmetic entries unavailable");
            HashSet<CustomizationSlotEntry> entries = new HashSet<CustomizationSlotEntry>();
            foreach (var list in items.Values)
                foreach (CustomizationSlotEntry entry in list)
                    if (entry != null) entries.Add(entry);
            return entries;
        }

        private static object Read(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Members);
                if (field != null) return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, Members);
                if (property != null && property.CanRead) return property.GetValue(target, null);
            }
            return null;
        }

        private static CosmeticPacker.Input Input(CustomizationSlotEntry entry, ICustomizationHandler handler)
        {
            ICosmeticMeshPartBase part = entry.Part as ICosmeticMeshPartBase;
            CosmeticPacker.Input input;
            if (part == null || handler == null || handler.IsDestroyed || handler.CharacterDummy == null ||
                handler.CharacterDummy.CosmeticPacker == null) return null;
            return handler.CharacterDummy.CosmeticPacker.Inputs.TryGetValue(part.PartId, out input) ? input : null;
        }

        private static bool Covered(CustomizationSlotEntry entry, uint mask)
        {
            if (entry == null || !(entry.Arguments is TwoColors) || entry.Part is SkinMeshPart ||
                entry.Visual == null || !(entry.Visual.Definition is PlayerCosmeticDefinition)) return false;
            ICosmeticMeshPartBase part = entry.Part as ICosmeticMeshPartBase;
            if (part == null || part.IsTextureSharingForced || entry.Part.Slots == null) return false;
            // Never compact an item without a complete set of authored compact LODs.
            var lods = part.Lods;
            var compact = part.ImpededLods;
            if (lods == null || lods.Length == 0 || compact == null || compact.Length < lods.Length) return false;
            bool any = false;
            for (int i = 0; i < lods.Length; i++)
                if (lods[i] != null) { if (compact[i] == null) return false; any = true; }
            if (!any) return false;
            bool covered = false;
            foreach (int slot in entry.Part.Slots)
            {
                // Exclude held accessories/non-clothing, matching the native reference.
                if (slot >= 301 && slot != 331 && slot != 501 && slot != 511) return false;
                if (CoversSlot(mask, slot)) covered = true;
            }
            return covered;
        }

        // Repository order: head, chest, left/right shoulder, left/right arm,
        // left/right tasset. Do not cast the differently ordered native enum.
        internal static bool CoversSlot(uint mask, int slot)
        {
            return ((mask & 1U) != 0 && (Between(slot, 201, 213) || Between(slot, 251, 263) || slot == 331))
                || ((mask & 2U) != 0 && (Between(slot, 11, 55) || Between(slot, 181, 195) || Between(slot, 281, 285) || slot == 511))
                || ((mask & 4U) != 0 && (Between(slot, 81, 85) || Between(slot, 101, 105)))
                || ((mask & 8U) != 0 && (Between(slot, 91, 95) || Between(slot, 111, 115)))
                || ((mask & 16U) != 0 && (Between(slot, 121, 125) || Between(slot, 141, 145) || Between(slot, 161, 165)))
                || ((mask & 32U) != 0 && (Between(slot, 131, 135) || Between(slot, 151, 155) || Between(slot, 171, 175)))
                || ((mask & 192U) != 0 && (Between(slot, 51, 75) || slot == 501));
        }

        private static bool Between(int slot, int first, int last) { return slot >= first && slot <= last; }

        private static void Restore(CustomizationSlotEntry entry, Dictionary<ICustomizationHandler, TwoColors> overlays,
            bool registered, List<ICustomizationHandler> handlers)
        {
            if (!registered || entry.Part == null || entry.Arguments == null || entry.DisablerCount > 0) return;
            foreach (KeyValuePair<ICustomizationHandler, TwoColors> overlay in overlays)
            {
                if (!handlers.Contains(overlay.Key)) continue;
                CosmeticPacker.Input input = Input(entry, overlay.Key);
                // Restore only our own overlay, never overwrite another mod/native change.
                if (input != null && input.Arguments == overlay.Value)
                    Change(entry, overlay.Key, entry.Arguments);
            }
        }

        internal void Clear()
        {
            if (manager != null)
            {
                try
                {
                    HashSet<CustomizationSlotEntry> current = Entries();
                    List<ICustomizationHandler> handlers = new List<ICustomizationHandler>();
                    foreach (ICustomizationHandler handler in manager.Handlers)
                        if (handler != null && !handler.IsDestroyed) handlers.Add(handler);
                    foreach (var pair in owned)
                        try { Restore(pair.Key, pair.Value, current.Contains(pair.Key), handlers); }
                        catch (Exception) { }
                }
                catch (Exception) { }
            }
            owned.Clear();
            manager = null;
            warned = false;
        }
    }
}
