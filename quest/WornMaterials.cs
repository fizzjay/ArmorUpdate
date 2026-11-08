using System;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;
using Il2CppAlta;
using UnityEngine;

namespace ArmorRestoredQuest
{
    // Resolve the game's own visual data, never a hard-coded metal-name/color table.
    // Reflection supports the managed PC fields and generated Quest properties.
    internal static class WornMaterials
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<uint, Material> Native = new Dictionary<uint, Material>();
        private static readonly Dictionary<uint, Color> Colors = new Dictionary<uint, Color>();
        private static readonly Dictionary<uint, Dictionary<int, Material>> Tinted = new Dictionary<uint, Dictionary<int, Material>>();
        private static readonly HashSet<uint> Resolved = new HashSet<uint>();

        internal static Material[] Apply(int piece, uint hash, Material[] originals)
        {
            Material[] result = (Material[])originals.Clone();
            for (int i = 0; i < result.Length; i++) result[i] = Apply(piece, hash, result[i]);
            return result;
        }

        internal static Material Apply(int piece, uint hash, Material original)
        {
            if (piece / 8 != 1 || hash == 0U || original == null) return original;
            Resolve(hash);
            Material native;
            if (Native.TryGetValue(hash, out native) && native != null) return native;
            Color color;
            if (!Colors.TryGetValue(hash, out color)) return original;
            Dictionary<int, Material> variants;
            if (!Tinted.TryGetValue(hash, out variants))
            {
                variants = new Dictionary<int, Material>();
                Tinted[hash] = variants;
            }
            int id = original.GetInstanceID();
            Material tinted;
            if (!variants.TryGetValue(id, out tinted) || tinted == null)
            {
                // Keep armor textures, normal maps and shader when the game exposes a tint.
                tinted = new Material(original);
                tinted.name = original.name + " material " + hash;
                if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", color);
                if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", color);
                variants[id] = tinted;
            }
            return tinted;
        }

        private static void Resolve(uint hash)
        {
            if (!Resolved.Add(hash)) return;
            try
            {
                PhysicalMaterial physical = HashedGeneralValue<PhysicalMaterial>.Get(hash);
                if (physical != null)
                {
                    // Only inspect visual members; avoid unrelated gameplay properties.
                    foreach (string name in new[] { "material", "renderMaterial", "unityMaterial", "visualMaterial" })
                    {
                        Material material = Read(physical, name) as Material;
                        if (material != null)
                        {
                            Native[hash] = material;
                            return;
                        }
                    }
                    foreach (string name in new[] { "color", "colour", "tint", "materialColor", "materialColour", "mainColor", "baseColor", "displayColor" })
                    {
                        object value = Read(physical, name);
                        if (value is Color)
                        {
                            Colors[hash] = (Color)value;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Warning("worn material " + hash + ": " + ex.Message);
            }
            // Fail visibly in the log, but leave armor wearable if a game API changes.
            Mod.Log.Warning("No visual material/color exposed for physical material " + hash + "; keeping the armor-pack appearance.");
        }

        private static object Read(object target, string name)
        {
            Type type = target.GetType();
            while (type != null)
            {
                foreach (FieldInfo field in type.GetFields(Members | BindingFlags.DeclaredOnly))
                    if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) return field.GetValue(target);
                foreach (PropertyInfo property in type.GetProperties(Members | BindingFlags.DeclaredOnly))
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.CanRead && property.GetIndexParameters().Length == 0)
                        return property.GetValue(target, null);
                type = type.BaseType;
            }
            return null;
        }

        internal static void Clear()
        {
            foreach (Dictionary<int, Material> variants in Tinted.Values)
                foreach (Material material in variants.Values)
                    if (material != null) UnityEngine.Object.Destroy(material);
            Tinted.Clear();
            Native.Clear();
            Colors.Clear();
            Resolved.Clear();
        }
    }
}
