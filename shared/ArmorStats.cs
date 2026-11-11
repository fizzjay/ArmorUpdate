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
                harmony.Patch(AccessTools.Method(typeof(PhysicalMaterialPart), "Generate"), prefix: new HarmonyMethod(typeof(ArmorStats), nameof(ArmorLootMaterial)));
                harmony.Patch(worn, postfix: new HarmonyMethod(typeof(ArmorStats), nameof(WornWeight)));
            }
#endif
            harmony.Patch(AccessTools.Method(typeof(DurabilityModule),"CheckBreak"), prefix: new HarmonyMethod(typeof(ArmorStats),nameof(KeepBrokenArmor)));
            Enabled = true;
            Mod.Log.Msg("armor material stats: pickup weight enabled; balance defaults in material-stats.txt; metal loot Copper/Iron 50:50; other materials crafting/commands only.");
        }
        internal static float Condition(float value) { return !Finite(value) ? 0f : Mathf.Clamp01(value); }
        internal static float MaximumDurability(int piece)
        {
            float[] totals={600f,1400f,900f,1600f,800f,500f};
            return totals[piece/8]*(piece%8==1 ? 3f : piece%8==0 ? 1.5f : piece%8<4 ? 1.25f : 1f)/11f;
        }
        internal static void ConfigureDurability(int piece,GameObject root)
        {
#if ARMOR_QUEST
            var module=root.GetComponent(Il2CppType.Of<DurabilityModule>())?.TryCast<DurabilityModule>();
            if(module==null) module=root.AddComponent(Il2CppType.Of<DurabilityModule>()).TryCast<DurabilityModule>();
            var settings=ScriptableObject.CreateInstance(Il2CppType.Of<DurabilitySettings>()).TryCast<DurabilitySettings>();
            var pickup=root.GetComponent(Il2CppType.Of<Pickup>())?.TryCast<Pickup>();
            var entity=root.GetComponent(Il2CppType.Of<NetworkEntity>())?.TryCast<NetworkEntity>();
#else
            var module=root.GetComponent<DurabilityModule>();
            if(module==null) module=root.AddComponent<DurabilityModule>();
            var settings=ScriptableObject.CreateInstance<DurabilitySettings>();
            var pickup=root.GetComponent<Pickup>();
            var entity=root.GetComponent<NetworkEntity>();
#endif
            float maximum=MaximumDurability(piece);
            settings.name="ArmorRestored durability "+Catalog.Id(piece);
            ArmorCrafting.Set(settings,"isChanceBasedDurability",false);
            ArmorCrafting.Set(settings,"durability",maximum);
            ArmorCrafting.Set(settings,"damageConversion",1f);
            ArmorCrafting.Set(module,"durabilitySettings",settings);
#if ARMOR_QUEST
            ArmorCrafting.Set(module,"stageVisuals",new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<DurabilityModule.DurabilityVisual>(0L));
#else
            ArmorCrafting.Set(module,"stageVisuals",new DurabilityModule.DurabilityVisual[0]);
#endif
            // Empty events are still required by native durability initialization.
            ArmorCrafting.Set(module,"stateChangeEffect",new PickupEvent());
            ArmorCrafting.Set(module,"brokeOnServer",new AltaEvent());
            ArmorCrafting.Set(module,"canDockWhenDamaged",true);
            ArmorCrafting.Set(module,"integrity",1f);
            ArmorCrafting.Set(module,"baseDurability",maximum);
            ArmorCrafting.Set(module,"currentDurability",maximum);
            ArmorCrafting.Set(module,"entity",entity);
            ArmorCrafting.Set(pickup,"durabilityModule",module);
            var behaviours=new List<MonoBehaviour>();
#if ARMOR_QUEST
            if(entity.networkEntityBehaviours!=null) foreach(var item in entity.networkEntityBehaviours) behaviours.Add(item);
            bool registered=false;foreach(var item in behaviours) if(item!=null && item.Pointer==module.Pointer) registered=true;
            if(!registered) behaviours.Add(module);
            entity.networkEntityBehaviours=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<MonoBehaviour>(behaviours.ToArray());
#else
            var existing=ArmorCrafting.Get(entity,"networkEntityBehaviours") as MonoBehaviour[];
            if(existing!=null) behaviours.AddRange(existing);
            if(!behaviours.Contains(module)) behaviours.Add(module);
            ArmorCrafting.Set(entity,"networkEntityBehaviours",behaviours.ToArray());
#endif
        }
        private static bool KeepBrokenArmor(DurabilityModule __instance,ref bool __result)
        {
#if ARMOR_QUEST
            var pickup=__instance.GetComponent(Il2CppType.Of<Pickup>())?.TryCast<Pickup>();
#else
            var pickup=__instance.GetComponent<Pickup>();
#endif
            if(Piece(pickup)<0 || __instance.Integrity>0f) return true;
            __result=true;return false; // Depleted armor remains repairable; never destroys saved provenance.
        }
#if !ARMOR_QUEST
        internal static float ReadCondition(NetworkEntity entity)
        {
            var module=entity==null ? null : entity.GetComponent<DurabilityModule>();
            return module==null ? 1f : Condition(module.Integrity);
        }
        internal static void RestoreCondition(NetworkEntity entity,float condition)
        {
            var module=entity==null ? null : entity.GetComponent<DurabilityModule>();
            if(module==null) return;
            ArmorCrafting.Set(module,"integrity",Condition(condition));
            if(ArmorCrafting.Get(module,"syncStage")!=null)
                AccessTools.Method(typeof(DurabilityModule),"OnDurabilityChanged",new[] {typeof(bool)}).Invoke(module,new object[] {false});
        }
#endif
        private sealed class PickupSurface
        {
            internal Material[] Original;
            internal uint Hash;
        }
        private static readonly Dictionary<Renderer,PickupSurface> pickupSurfaces = new Dictionary<Renderer,PickupSurface>();
        internal static void ClearPickupSurfaces() { pickupSurfaces.Clear(); }
        private static void RefreshPickupSurfaces(Pickup pickup,int piece,uint hash)
        {
            if(piece/8!=0 && piece/8!=4) return;
            if(piece/8==4 && hash==1U) hash=0U; // Uncrafted authored Wood default.
#if ARMOR_QUEST
            var renderers=pickup.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true);
#else
            var renderers=pickup.GetComponentsInChildren<Renderer>(true);
#endif
            foreach(var raw in renderers)
            {
#if ARMOR_QUEST
                Renderer renderer=raw==null ? null : raw.TryCast<Renderer>();
#else
                Renderer renderer=raw;
#endif
                if(renderer==null) continue;
                PickupSurface entry;
                if(!pickupSurfaces.TryGetValue(renderer,out entry))
                {
#if ARMOR_QUEST
                    var originals=new Material[renderer.sharedMaterials.Length];renderer.sharedMaterials.CopyTo(originals,0);
#else
                    var originals=renderer.sharedMaterials;
#endif
                    entry=new PickupSurface {Original=originals,Hash=uint.MaxValue};pickupSurfaces.Add(renderer,entry);
                }
                if(entry.Hash==hash) continue;
                var next=WornMaterials.Apply(piece,hash,entry.Original,renderer.gameObject.name);
#if ARMOR_QUEST
                renderer.sharedMaterials=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Material>(next);
#else
                renderer.sharedMaterials=next;
#endif
                entry.Hash=hash;
            }
        }
#if !ARMOR_QUEST
        private static PhysicalMaterial lootCopper,lootIron;
        private static bool ArmorLootMaterial(PhysicalMaterialPart __instance)
        {
            if(!Mod.IsServer || __instance==null || __instance.Entity==null || __instance.Entity.Prefab==null ||
                Catalog.PieceOf(__instance.Entity.Prefab.Hash)/8!=1 || Catalog.PieceOf(__instance.Entity.Prefab.Hash)<0) return true;
            if(lootCopper==null || lootIron==null)
                foreach(var material in Resources.FindObjectsOfTypeAll<PhysicalMaterial>())
                {
                    string name=Normalize(material.name);
                    if(name=="copper" || name=="copperalloy") lootCopper=material;
                    if(name=="iron" || name=="ironalloy") lootIron=material;
                }
            if(lootCopper==null || lootIron==null)
            {
                if(warned.Add("loot-material")) Mod.Log.Error("Metal loot requires native Copper and Iron materials; keeping native generation until both are available.");
                return true;
            }
            var selected=UnityEngine.Random.Range(0,2)==0 ? lootCopper : lootIron;
            AccessTools.Method(typeof(PhysicalMaterialPart),"SetMaterial",new[] {typeof(PhysicalMaterial)}).Invoke(__instance,new object[] {selected});
            return false; // Only armor Generate, never global distributions or saved-item loads.
        }
#endif
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
            float total = set == 0 ? 6f : set == 1 ? 14f : set == 3 ? 12f : set == 4 ? 8f : set == 5 ? 5f : 9f;
            // Original seven-slot hip share is split between our two tassets.
            float share = slot == 0 ? 1.5f : slot == 1 ? 3f : slot == 2 || slot == 3 ? 1.25f : 1f;
            float multiplier = (set == 1 || set == 3) ? WeightMultiplier(hash) : 1f;
            return Mathf.Clamp(total * share / 11f * multiplier, .01f, 100f);
        }
        private static int Piece(Pickup pickup)
        {
            if (pickup == null || pickup.Entity == null || pickup.Entity.Prefab == null) return -1;
            uint hash = pickup.Entity.Prefab.Hash;
            return Catalog.PieceOf(hash);
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
            foreach(var renderer in new List<Renderer>(pickupSurfaces.Keys)) if(renderer==null) pickupSurfaces.Remove(renderer);
#if ARMOR_QUEST
            if (!Gate.IsOpen || !Registry.Done || !Draw.VisualsReady()) return;
            nextTick = Time.unscaledTime + 2f; // Quest discovery is expensive in populated scenes.
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
                uint materialHash=MaterialHash(pickup);
                try { RefreshPickupSurfaces(pickup,piece,materialHash); }
                catch(Exception error) {if(warned.Add("pickup-surface:"+piece)) Mod.Log.Warning("Pickup surface deferred: "+error.GetBaseException().Message);}
                float mass = Weight(piece, materialHash);
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
