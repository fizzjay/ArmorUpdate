using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppAlta.QuickAccessActions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestoredQuest
{
    // Reveal the native action only; leave Run/Open/Close/IsValid untouched.
    internal static class EquipmentMenu
    {
        private sealed class Added
        {
            internal QuickAccessMenuController Menu;
            internal EquipmentModeQuickAccess Action;
        }
        private static readonly List<Added> added = new List<Added>();
        private static float nextScan;
        private static bool installed;
        private static bool warned;
        private static bool wasOpen;

        internal static void Install()
        {
            if (installed) return;
            try
            {
                MethodInfo getter = AccessTools.PropertyGetter(typeof(QuickAccessMenuAction), "IsIgnored");
                if (getter == null) throw new MissingMethodException("QuickAccessMenuAction.IsIgnored");
                new Harmony("armorrestored.quest.equipment-menu").Patch(getter,
                    postfix: new HarmonyMethod(typeof(EquipmentMenu), nameof(IgnoredPostfix)));
                installed = true;
                Mod.Log.Msg("equipment menu: native Equipment Mode visibility hook ready; validity and transitions unchanged.");
            }
            catch (Exception ex)
            {
                Mod.Log.Warning("equipment menu: visibility hook unavailable: " + ex.Message);
            }
        }

        private static void IgnoredPostfix(QuickAccessMenuAction __instance, ref bool __result)
        {
            if (!Gate.IsOpen || __instance == null || PlayerController.Current == null) return;
            if (__instance.TryCast<EquipmentModeQuickAccess>() != null) __result = false;
        }

        internal static void Tick()
        {
            if (!installed) return;
            if (!Gate.IsOpen)
            {
                // Don't destroy an action still referenced by an open bubble.
                // Remove from root menus now; retain assets until controllers die.
                if (wasOpen)
                {
                    warned = false;
                    foreach (Added entry in added)
                        if (entry.Menu != null) Remove(entry);
                }
                wasOpen = false;
                return;
            }
            if (!wasOpen) { nextScan = 0f; wasOpen = true; }
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f;
            for (int i = added.Count - 1; i >= 0; i--)
            {
                if (added[i].Menu != null) continue;
                if (added[i].Action != null) Object.Destroy(added[i].Action);
                added.RemoveAt(i);
            }
            PlayerController local = PlayerController.Current;
            if (local == null || local.NetworkPlayer == null) return;
            foreach (Object value in Resources.FindObjectsOfTypeAll(Il2CppType.Of<QuickAccessMenuController>()))
            {
                QuickAccessMenuController menu = value == null ? null : value.TryCast<QuickAccessMenuController>();
                if (menu == null || !menu.gameObject.activeInHierarchy || menu.Controller == null
                    || menu.Controller.PlayerController == null
                    || menu.Controller.PlayerController.Pointer != local.Pointer) continue;
                try { Ensure(menu, local); }
                catch (Exception ex)
                {
                    if (!warned) Mod.Log.Warning("equipment menu: " + ex.Message);
                    warned = true;
                }
            }
        }

        private static void Ensure(QuickAccessMenuController menu, PlayerController local)
        {
            Il2CppReferenceArray<QuickAccessMenuAction> menus = Menus(menu);
            if (menus == null) return; // Controller initialization has not completed.
            foreach (QuickAccessMenuAction action in menus)
                if (action != null && action.TryCast<EquipmentModeQuickAccess>() != null) return;
            EquipmentModeQuickAccess native = null;
            foreach (Object value in Resources.FindObjectsOfTypeAll(Il2CppType.Of<EquipmentModeQuickAccess>()))
            {
                native = value == null ? null : value.TryCast<EquipmentModeQuickAccess>();
                if (native != null) break;
            }
            if (native == null)
            {
                if (!warned) Mod.Log.Warning("equipment menu: native EquipmentModeQuickAccess asset not loaded; no replacement action created.");
                warned = true;
                return;
            }
            EquipmentModeQuickAccess clone = null;
            foreach (Added entry in added)
                if (entry.Menu != null && entry.Menu.Pointer == menu.Pointer && entry.Action != null)
                { clone = entry.Action; break; }
            bool created = clone == null;
            if (created)
            {
                clone = Object.Instantiate(native);
                clone.name = "ArmorRestored Equipment Mode";
            }
            try
            {
                clone.InitializeForPlayer(local.NetworkPlayer, false);
                var updated = new Il2CppReferenceArray<QuickAccessMenuAction>(menus.Length + 1);
                for (int i = 0; i < menus.Length; i++) updated[i] = menus[i];
                updated[menus.Length] = clone;
                SetMenus(menu, updated);
                if (created) added.Add(new Added { Menu = menu, Action = clone });
                Mod.Log.Msg("equipment menu: native Equipment Mode added to local wheel " + menu.GetInstanceID() + "; reopen the wheel.");
            }
            catch
            {
                if (created && clone != null) Object.Destroy(clone);
                throw;
            }
        }

        private static void Remove(Added entry)
        {
            try
            {
                var menus = Menus(entry.Menu);
                if (menus == null || entry.Action == null) return;
                var remaining = new List<QuickAccessMenuAction>();
                foreach (QuickAccessMenuAction action in menus)
                    if (action == null || action.Pointer != entry.Action.Pointer) remaining.Add(action);
                if (remaining.Count != menus.Length)
                    SetMenus(entry.Menu, new Il2CppReferenceArray<QuickAccessMenuAction>(remaining.ToArray()));
            }
            catch (Exception ex)
            {
                if (!warned) Mod.Log.Warning("equipment menu cleanup: " + ex.Message);
                warned = true;
            }
        }

        // Il2CppInterop exposes native private fields as managed properties.
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Il2CppReferenceArray<QuickAccessMenuAction> Menus(QuickAccessMenuController menu)
        {
            PropertyInfo property = typeof(QuickAccessMenuController).GetProperty("quickAccessMenus", Members);
            if (property != null) return property.GetValue(menu, null) as Il2CppReferenceArray<QuickAccessMenuAction>;
            FieldInfo field = typeof(QuickAccessMenuController).GetField("quickAccessMenus", Members);
            if (field == null) throw new MissingMemberException("QuickAccessMenuController.quickAccessMenus");
            return field.GetValue(menu) as Il2CppReferenceArray<QuickAccessMenuAction>;
        }
        private static void SetMenus(QuickAccessMenuController menu, Il2CppReferenceArray<QuickAccessMenuAction> value)
        {
            PropertyInfo property = typeof(QuickAccessMenuController).GetProperty("quickAccessMenus", Members);
            if (property != null) { property.SetValue(menu, value, null); return; }
            FieldInfo field = typeof(QuickAccessMenuController).GetField("quickAccessMenus", Members);
            if (field == null) throw new MissingMemberException("QuickAccessMenuController.quickAccessMenus");
            field.SetValue(menu, value);
        }
    }
}
