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
                        object visual = Read(physical, name);
                        Material material = visual == null ? null : AsMaterial(visual);
                        if (material != null)
                        {
                            // Borrow only the tint, never the metal's texture/shader.
                            // Armor UVs are authored for the original armor atlas.
                            foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor", "_MainColor", "_AlbedoColor" })
                            {
                                if (!material.HasProperty(property)) continue;
                                Color tint = material.GetColor(property);
                                tint.a = 1f;
                                Colors[hash] = tint;
                                return;
                            }
                        }
                    }
                    foreach (string name in new[] { "color", "colour", "tint", "materialColor", "materialColour", "mainColor", "baseColor", "displayColor" })
                    {
                        object value = Read(physical, name);
                        if (value is Color)
                        {
                            Color tint = (Color)value;
                            tint.a = 1f;
                            Colors[hash] = tint;
                            return;
                        }
                    }
                    // Game versions can put visual data in a nested settings object,
                    // or expose a differently named Material/Color member. Inspect
                    // typed visual data rather than guessing a metal's color by hash.
                    Color discovered;
                    if (FindTint(physical, 0, new HashSet<object>(), out discovered))
                    {
                        discovered.a = 1f;
                        Colors[hash] = discovered;
                        Mod.Log.Msg("worn tint resolved for physical material " + hash);
                        return;
                    }
                    DescribeVisualMembers(physical, hash);
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Warning("worn material " + hash + ": " + ex.Message);
            }
            // Fail visibly in the log, but leave armor wearable if a game API changes.
            Mod.Log.Warning("No visual material/color exposed for physical material " + hash + "; keeping the armor-pack appearance.");
        }

        private static bool VisualName(string name)
        {
            name = name.ToLowerInvariant();
            return name.Contains("material") || name.Contains("colour") || name.Contains("color")
                || name.Contains("tint") || name.Contains("visual") || name.Contains("render")
                || name.Contains("appearance") || name.Contains("graphic");
        }

        private static bool FindTint(object value, int depth, HashSet<object> visited, out Color tint)
        {
            tint = default(Color);
            if (value == null || depth > 3) return false;
            if (value is Color) { tint = (Color)value; return true; }
            if (value is Color32) { tint = (Color32)value; return true; }
            Material material = AsMaterial(value);
            if (material != null)
            {
                foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor", "_MainColor", "_AlbedoColor" })
                {
                    if (!material.HasProperty(property)) continue;
                    tint = material.GetColor(property);
                    return true;
                }
                return false;
            }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string || !visited.Add(value)) return false;
            while (type != null)
            {
                foreach (FieldInfo field in type.GetFields(Members | BindingFlags.DeclaredOnly))
                {
                    if (!VisualMember(field.Name, field.FieldType)) continue;
                    try { if (FindTint(field.GetValue(value), depth + 1, visited, out tint)) return true; }
                    catch (Exception) { } // One inaccessible member must not hide the rest.
                }
                foreach (PropertyInfo property in type.GetProperties(Members | BindingFlags.DeclaredOnly))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0 ||
                        !VisualMember(property.Name, property.PropertyType)) continue;
                    try { if (FindTint(property.GetValue(value, null), depth + 1, visited, out tint)) return true; }
                    catch (Exception) { }
                }
                type = type.BaseType;
            }
            return false;
        }

        private static bool VisualMember(string name, Type type)
        {
            // Exclude emission, damage flashes and UI colors from the base tint.
            string lower = name.ToLowerInvariant();
            if (lower.Contains("emission") || lower.Contains("highlight") || lower.Contains("flash") ||
                lower.Contains("damage") || lower.Contains("particle")) return false;
            return VisualName(name) || type == typeof(Color) || type == typeof(Color32) ||
                typeof(Material).IsAssignableFrom(type);
        }

        private static Material AsMaterial(object value)
        {
            Material material = value as Material;
            if (material != null) return material;
            // Quest may return an Il2CppSystem.Object/UnityEngine.Object proxy:
            // CLR 'as Material' does not perform the native IL2CPP cast.
            foreach (MethodInfo method in value.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "TryCast" || !method.IsGenericMethodDefinition ||
                    method.GetGenericArguments().Length != 1 || method.GetParameters().Length != 0) continue;
                try { return method.MakeGenericMethod(typeof(Material)).Invoke(value, null) as Material; }
                catch (Exception) { return null; }
            }
            return null;
        }

        private static void DescribeVisualMembers(object physical, uint hash)
        {
            // Names and types only: lets us target the actual installed game API
            // if its visual data requires a method rather than a field/property.
            List<string> names = new List<string>();
            Type type = physical.GetType();
            while (type != null && names.Count < 60)
            {
                foreach (FieldInfo field in type.GetFields(Members | BindingFlags.DeclaredOnly))
                    if (!field.Name.StartsWith("Native")) names.Add(field.Name + ":" + field.FieldType.Name);
                foreach (PropertyInfo property in type.GetProperties(Members | BindingFlags.DeclaredOnly))
                    names.Add(property.Name + ":" + property.PropertyType.Name);
                type = type.BaseType;
            }
            Mod.Log.Warning("physical material " + hash + " visual lookup members: " + string.Join(", ", names.ToArray()));
        }

        private static object Read(object target, string name)
        {
            Type type = target.GetType();
            while (type != null)
            {
                foreach (FieldInfo field in type.GetFields(Members | BindingFlags.DeclaredOnly))
                    if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                        try { return field.GetValue(target); } catch (Exception) { }
                foreach (PropertyInfo property in type.GetProperties(Members | BindingFlags.DeclaredOnly))
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.CanRead && property.GetIndexParameters().Length == 0)
                        try { return property.GetValue(target, null); } catch (Exception) { }
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
            Colors.Clear();
            Resolved.Clear();
        }
    }
}
