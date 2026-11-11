using System;
using System.Reflection;
using Alta.Networking;
using HarmonyLib;
using UnityEngine;

namespace ArmorRestored
{
    // Server-only kill drops, ported from the supplied reference. No global
    // table insertion, no SpawnHelper-wide reroll, and no changes to saved items.
    internal static class WyrmArmorLoot
    {
        private static readonly System.Random random = new System.Random();
        private static PropertyInfo alive, entity;
        private static bool installed;
        internal sealed class DeathState
        {
            internal NetworkEntity Entity;
            internal Vector3 Position;
        }
        internal static void Install()
        {
            if (installed || !Mod.IsServer) return;
            Type wyrm = AccessTools.TypeByName("Alta.Intelligence.Wyrm");
            if (wyrm == null) throw new MissingMemberException("Alta.Intelligence.Wyrm");
            MethodInfo death = null;
            foreach (MethodInfo method in wyrm.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                ParameterInfo[] args = method.GetParameters();
                if (method.Name == "Killed" && args.Length == 1 && args[0].ParameterType.Name == "HealthObject")
                {
                    if (death != null) throw new AmbiguousMatchException("Wyrm.Killed(HealthObject)");
                    death = method;
                }
            }
            alive = AccessTools.Property(wyrm, "IsAlive");
            entity = AccessTools.Property(wyrm, "Entity");
            if (death == null || alive == null || alive.PropertyType != typeof(bool) || entity == null)
                throw new MissingMemberException("Wyrm.Killed, IsAlive or Entity changed");
            var harmony = new HarmonyLib.Harmony("armorrestored.wyrm-kill-loot");
            harmony.Patch(death,
                prefix: new HarmonyMethod(typeof(WyrmArmorLoot), "BeforeDeath"),
                postfix: new HarmonyMethod(typeof(WyrmArmorLoot), "AfterDeath"));
            installed = true;
            Mod.Log.Msg("Wyrm armor: kill-only drops installed (2.5% per Wyrm kill, one of eight slots); wyrm_loot controls drops.");
        }
        private static bool Supported(uint hash)
        {
            // Native ordinary and crystal Wyrm variants from the reference DLL.
            return hash == 6004U || hash == 21642U || hash == 37392U || hash == 48128U;
        }
        private static void BeforeDeath(object __instance, out DeathState __state)
        {
            __state = null;
            if (!Mod.IsServer || !Settings.Loot || !Settings.WyrmLoot || !Prefabs.Done) return;
            try
            {
                if (!(bool)alive.GetValue(__instance, null)) return;
                NetworkEntity victim = entity.GetValue(__instance, null) as NetworkEntity;
                Component component = __instance as Component;
                if (victim == null || victim.Prefab == null || component == null || !Supported(victim.Prefab.Hash)) return;
                __state = new DeathState { Entity = victim, Position = component.transform.position + Vector3.up * .35f };
            }
            catch (Exception error) { Mod.Log.Warning("Wyrm kill capture failed: " + error.Message); }
        }
        private static void AfterDeath(object __instance, DeathState __state)
        {
            if (__state == null || !Mod.IsServer) return;
            try
            {
                // Only an actual alive-to-dead transition can award a drop.
                // Calls on already dead Wyrms have no prefix state.
                if ((bool)alive.GetValue(__instance, null)) return;
                NetworkEntity victim = __state.Entity;
                if (victim == null || victim.IsBeingDestroyed || victim.Chunk == null) return;
                if (random.Next(40) != 0) return;
                int piece = Catalog.Piece(2, random.Next(8));
                var prefab = Prefabs.ByPiece[piece];
                if (prefab == null) return;
                var drop = SpawnHelper.Spawn(prefab, SpawnData.DontGenerate, victim.Chunk,
                    __state.Position, Quaternion.identity, null);
                if (drop != null) Mod.Log.Msg("Wyrm kill dropped " + Catalog.Id(piece));
            }
            catch (Exception error) { Mod.Log.Warning("Wyrm armor drop failed: " + error.Message); }
        }
    }
}
