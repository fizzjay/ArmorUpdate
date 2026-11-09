using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
#if ARMOR_QUEST
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Networking;
using Il2CppAlta.Impact;
using Il2CppAlta.Caves;
using Il2CppInterop.Runtime;
namespace ArmorRestoredQuest
#else
using Alta;
using Alta.Inventory;
using Alta.Impact;
using Alta.Caves;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
namespace ArmorRestored
#endif
{
    // Balance defaults, not claims about native metal stats. No new network behaviours.
    internal static class ArmorStats
    {
        internal sealed class Metal
        {
            internal string Name;
            internal float Weight;
            internal float Strength;
            internal int NaturalWeight;
        }
        internal static readonly Metal[] Metals = {
            Entry("copper", .85f, .8f, 100), Entry("iron", 1f, 1f, 80),
            Entry("rediron", 1.2f, 1.1f, 60), Entry("gold", 3f, 1.2f, 40),
            Entry("electrum", 1.6f, 1.3f, 30), Entry("viridium", 1.75f, 1.4f, 22),
            Entry("silver", 1.5f, 1.5f, 16), Entry("palladium", 1.25f, 1.6f, 10),
            Entry("mythril", .65f, 1.7f, 6), Entry("valyan", 1.1f, 1.8f, 3),
            Entry("darksteel", 1.4f, 1.9f, 0)
        };
        private static Metal Entry(string name, float weight, float strength, int rarity)
        { return new Metal { Name = name, Weight = weight, Strength = strength, NaturalWeight = rarity }; }
        internal static bool Enabled;
        private static bool preferNative = true;
        private static float nextTick;
        private static readonly HashSet<string> warned = new HashSet<string>();
        private static readonly Dictionary<uint, Metal> resolved = new Dictionary<uint, Metal>();

        internal static void Install()
        {
            Load();
            var harmony = new HarmonyLib.Harmony("armorrestored.material-stats");
            MethodInfo weight = AccessTools.PropertyGetter(typeof(Pickup), "PhysicalWeight");
            if (weight == null) throw new MissingMethodException("Pickup.PhysicalWeight");
            harmony.Patch(weight, postfix: new HarmonyMethod(typeof(ArmorStats), nameof(PickupWeight)));
#if !ARMOR_QUEST
            if (Mod.IsServer)
            {
                MethodInfo worn = AccessTools.Method(typeof(PlayerWeight), "GetInteractablesWeight");
                if (worn == null) throw new MissingMethodException("PlayerWeight.GetInteractablesWeight");
                harmony.Patch(worn, postfix: new HarmonyMethod(typeof(ArmorStats), nameof(WornWeight)));
            }
#endif
            Enabled = true;
            Mod.Log.Msg("armor material stats: pickup weight enabled; balance defaults in material-stats.txt; loot policy not yet wired.");
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static string Normalize(string value)
        { return (value ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace("(instance)", ""); }
        private static PhysicalMaterial FindMaterial(uint hash)
        {
            try { return HashedGeneralValue<PhysicalMaterial>.Get(hash); }
            catch (Exception ex)
            {
                if (warned.Add("lookup:" + hash)) Mod.Log.Warning("armor stats: material lookup " + hash + ": " + ex.Message);
                return null;
            }
        }
        internal static Metal Profile(uint hash)
        {
            if (hash == 0U) return Metals[1]; // Legacy/admin default armor is iron.
            Metal profile;
            if (resolved.TryGetValue(hash, out profile)) return profile;
            PhysicalMaterial material = FindMaterial(hash);
            if (material == null) return Metals[1]; // Retry when the registry is ready.
            string name = Normalize(material.name);
            foreach (Metal candidate in Metals)
                if (name == candidate.Name) { resolved[hash] = candidate; return candidate; }
            if (warned.Add("unknown:" + hash)) Mod.Log.Warning("armor stats: unrecognized material " + hash + " (" + material.name + "); using iron balance, not changing item identity.");
            return Metals[1];
        }
        internal static float Strength(uint hash) { return Enabled ? Profile(hash).Strength : 1f; }
        // Weight is not the protection ranking. Prefer a verified numeric native
        // weightMultiplier member if this build exposes one; never treat density as one.
        private static float WeightMultiplier(uint hash)
        {
            Metal profile = Profile(hash);
            try
            {
            if (preferNative && hash != 0U)
            {
                PhysicalMaterial material = FindMaterial(hash);
                if (material != null)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var field = material.GetType().GetField("weightMultiplier", flags);
                    var property = material.GetType().GetProperty("weightMultiplier", flags);
                    object raw = field != null ? field.GetValue(material) : property != null && property.GetIndexParameters().Length == 0 ? property.GetValue(material, null) : null;
                    if (raw is float || raw is double || raw is int)
                    {
                        float native = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                        if (Finite(native) && native > 0f) return native;
                    }
                    if (warned.Add("native-weight")) Mod.Log.Warning("armor stats: no usable native weightMultiplier found; using configurable fallback weights (not native values).");
                }
            }
            }
            catch (Exception ex)
            {
                if (warned.Add("native-weight-error")) Mod.Log.Warning("armor stats native weight lookup: " + ex.Message + "; using configured fallback.");
            }
            return profile.Weight;
        }
        internal static float Weight(int piece, uint hash)
        {
            int set = piece / 8;
            int slot = piece % 8;
            float total = set == 0 ? 6f : set == 1 ? 14f : 9f;
            // Original seven-slot hip share is split between our two tassets.
            float share = slot == 0 ? 1.5f : slot == 1 ? 3f : slot == 2 || slot == 3 ? 1.25f : 1f;
            float multiplier = set == 1 ? WeightMultiplier(hash) : 1f;
            return Mathf.Clamp(total * share / 11f * multiplier, .01f, 100f);
        }
        private static int Piece(Pickup pickup)
        {
            if (pickup == null || pickup.Entity == null || pickup.Entity.Prefab == null) return -1;
            uint hash = pickup.Entity.Prefab.Hash;
            return hash >= 52830U && hash <= 52853U ? (int)(hash - 52830U) : -1;
        }
        private static uint MaterialHash(Pickup pickup)
        {
#if ARMOR_QUEST
            var part = pickup.GetComponent(Il2CppType.Of<PhysicalMaterialPart>())?.TryCast<PhysicalMaterialPart>();
            return part == null || part.physicalMaterial == null ? 0U : part.physicalMaterial.Hash;
#else
            return ArmorMaterials.Read(pickup.Entity);
#endif
        }
        private static void PickupWeight(Pickup __instance, ref float __result)
        {
            if (!Enabled) return;
            int piece = Piece(__instance);
            if (piece >= 0) __result = Weight(piece, MaterialHash(__instance));
        }
        internal static void Tick()
        {
            if (!Enabled || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + .5f;
#if ARMOR_QUEST
            if (!Gate.IsOpen || !Registry.Done) return;
            var values = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Pickup>());
#else
            if (!Prefabs.Done || (!Mod.IsServer && !Gate.IsOpen)) return;
            var values = UnityEngine.Object.FindObjectsOfType<Pickup>();
#endif
            foreach (var value in values)
            {
#if ARMOR_QUEST
                Pickup pickup = value == null ? null : value.TryCast<Pickup>();
#else
                Pickup pickup = value;
#endif
                int piece = Piece(pickup);
                if (piece < 0) continue;
#if ARMOR_QUEST
                Rigidbody body = pickup.GetComponent(Il2CppType.Of<Rigidbody>())?.TryCast<Rigidbody>();
#else
                Rigidbody body = pickup.GetComponent<Rigidbody>();
#endif
                float mass = Weight(piece, MaterialHash(pickup));
                if (body != null && Mathf.Abs(body.mass - mass) > .0001f)
                {
                    body.mass = mass;
                    pickup.OnWeightChanged();
                }
            }
        }
#if !ARMOR_QUEST
        private static void WornWeight(PlayerWeight __instance, ref float __result)
        {
            if (!Enabled || !Mod.IsServer) return;
            foreach (IPlayer value in Player.AllPlayers)
            {
                Player player = value as Player;
                if (player == null) continue;
                bool matches = player.PlayerController != null && player.PlayerController.GetComponent<PlayerWeight>() == __instance;
                matches |= player.PlayerCharacter != null && player.PlayerCharacter.GetComponent<PlayerWeight>() == __instance;
                if (!matches) continue;
                int[] worn = ServerArmor.WornBy(player);
                uint[] materials = ServerArmor.MaterialsFor(Players.Name(player));
                for (int i = 0; i < 8; i++) if (worn[i] >= 0) __result += Weight(worn[i], materials[i]);
                return; // Add exactly once. Worn pickups are consumed, not in inventory.
            }
        }
        internal static void RefreshWearer(Player player)
        {
            if (!Mod.IsServer || player == null) return;
            PlayerWeight weight = player.PlayerController == null ? null : player.PlayerController.GetComponent<PlayerWeight>();
            if (weight == null && player.PlayerCharacter != null) weight = player.PlayerCharacter.GetComponent<PlayerWeight>();
            if (weight != null)
            {
                MethodInfo method = AccessTools.Method(typeof(PlayerWeight), "OnWeightChanged");
                if (method != null) method.Invoke(weight, null);
            }
        }
#endif
        private static void Load()
        {
            string path = Path.Combine(Mod.DataDir, "material-stats.txt");
            if (!File.Exists(path))
            {
                var lines = new List<string> { "# Proposed balance, NOT official game stats. Restart after editing.",
                    "# name=weightMultiplier,protectionStrength,naturalLootWeight (loot weights not wired yet)", "use_native_weight=true" };
                foreach (Metal metal in Metals) lines.Add(metal.Name + "=" + metal.Weight.ToString(CultureInfo.InvariantCulture)
                    + "," + metal.Strength.ToString(CultureInfo.InvariantCulture) + "," + metal.NaturalWeight);
                File.WriteAllLines(path, lines.ToArray());
                return;
            }
            foreach (string line in File.ReadAllLines(path))
            {
                string[] pair = line.Split('=');
                if (pair.Length != 2 || pair[0].TrimStart().StartsWith("#")) continue;
                if (pair[0].Trim() == "use_native_weight") { preferNative = pair[1].Trim() == "true"; continue; }
                foreach (Metal metal in Metals)
                {
                    if (Normalize(pair[0]) != metal.Name) continue;
                    string[] parts = pair[1].Split(',');
                    float weight, strength;
                    int rarity;
                    if (parts.Length != 3 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out weight)
                        || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out strength)
                        || !int.TryParse(parts[2], out rarity) || !Finite(weight) || !Finite(strength)
                        || weight <= 0f || strength <= 0f || weight > 10f || strength > 10f || rarity < 0)
                        throw new InvalidDataException("Invalid material stats: " + line);
                    metal.Weight = weight; metal.Strength = strength;
                    metal.NaturalWeight = metal.Name == "darksteel" ? 0 : rarity;
                }
            }
        }
    }
}
