using System;
using System.Collections.Generic;
using Alta;
using UnityEngine;

namespace ArmorRestored
{
    // Sample native cold albedo, then tint the original armor atlas relative to iron.
    // Never replace the armor shader/maps with a physical material's shader/maps.
    internal static class WornMaterials
    {
        private static readonly Dictionary<uint, Color> Colors = new Dictionary<uint, Color>();
        private static readonly Dictionary<uint, Color> RawColors = new Dictionary<uint, Color>();
        private static readonly Dictionary<uint, Dictionary<int, Material>> Tinted = new Dictionary<uint, Dictionary<int, Material>>();
        private static readonly Dictionary<uint, float> Retry = new Dictionary<uint, float>();
        private static readonly HashSet<uint> Warned = new HashSet<uint>();
        private static float nextTick;

        internal static Material[] Apply(int piece, uint hash, Material[] originals, string meshName = null)
        {
            Material[] result = (Material[])originals.Clone();
            for (int i = 0; i < result.Length; i++) result[i] = Apply(piece, hash, result[i], meshName);
            return result;
        }

        internal static Material Apply(int piece, uint hash, Material original, string meshName = null)
        {
            if (piece / 8 != 1 && piece / 8 != 3 || hash == 0U || original == null) return original;
            // The flexible glove backing is fabric, not a metal plate. Keep its
            // original neutral atlas appearance even when the plates are colored.
            if (meshName != null &&
                (meshName == "metal_gauntlet_L_glove_flair_LOD0" ||
                 meshName == "metal_gauntlet_R_glove_flair_LOD0")) return original;
            if (meshName != null && meshName.StartsWith("hebios/",StringComparison.Ordinal) && meshName.Contains("/255/")) return original;
            Dictionary<int, Material> variants;
            if (!Tinted.TryGetValue(hash, out variants))
            {
                variants = new Dictionary<int, Material>();
                Tinted[hash] = variants;
            }
            Material tinted;
            int id = original.GetInstanceID();
            if (!variants.TryGetValue(id, out tinted) || tinted == null)
            {
                // Keep a copy even while unresolved: Tick can tint this same material
                // once native assets arrive, without rebuilding the fitted meshes.
                tinted = new Material(original);
                tinted.name = original.name + " material " + hash;
                variants[id] = tinted;
            }
            if (Resolve(hash)) Paint(tinted, Colors[hash]);
            return tinted;
        }

        private static void Paint(Material material, Color tint)
        {
            tint.a = 1f;
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            foreach (KeyValuePair<uint, Dictionary<int, Material>> entry in Tinted)
            {
                if (!Resolve(entry.Key)) continue;
                foreach (Material material in entry.Value.Values)
                    if (material != null) Paint(material, Colors[entry.Key]);
            }
        }

        private static bool Resolve(uint hash)
        {
            if (Colors.ContainsKey(hash)) return true;
            float next;
            if (Retry.TryGetValue(hash, out next) && Time.unscaledTime < next) return false;
            Retry[hash] = Time.unscaledTime + 1f;
            try
            {
                Color? raw = ReadCold(hash);
                PhysicalMaterial iron = HashedGeneralValue<PhysicalMaterial>.Get("iron", false);
                Color? baseline = iron == null ? null : ReadCold(iron.Hash);
                if (raw.HasValue && baseline.HasValue)
                {
                    Color value = raw.Value;
                    Color reference = baseline.Value;
                    Colors[hash] = new Color(Ratio(value.r, reference.r),
                        Ratio(value.g, reference.g), Ratio(value.b, reference.b), 1f);
                    Retry.Remove(hash);
                    Mod.Log.Msg("worn cold-albedo tint resolved for physical material " + hash);
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (Warned.Add(hash)) Mod.Log.Warning("worn cold-albedo tint " + hash + ": " + ex.Message + "; will retry.");
                return false;
            }
            if (Warned.Add(hash)) Mod.Log.Warning("Native cold material unavailable for " + hash + "; keeping armor textures and retrying tint.");
            return false;
        }

        private static float Ratio(float value, float baseline)
        {
            return Mathf.Clamp(value / Math.Max(baseline, 0.01f), 0f, 2f);
        }

        private static Color? ReadCold(uint hash)
        {
            Color cached;
            if (RawColors.TryGetValue(hash, out cached)) return cached;
            // Direct keyed lookup avoids walking the live native registry. Validate
            // the hash because an unresolved lookup may return a default asset.
            PhysicalMaterial physical = HashedGeneralValue<PhysicalMaterial>.Get(hash);
            if (physical == null || physical.Hash != hash) return null;
            Material material = physical.GetNonAtlasedNonDeformingMaterial(0);
            Color? sampled = SampleColdColor(material);
            if (sampled.HasValue) RawColors[hash] = sampled.Value;
            return sampled;
        }

        private static bool Finite(Color c)
        {
            return !(float.IsNaN(c.r) || float.IsInfinity(c.r) ||
                float.IsNaN(c.g) || float.IsInfinity(c.g) || float.IsNaN(c.b) || float.IsInfinity(c.b));
        }

        private static Color ShaderColor(Material material, string property)
        {
            Color color = material.GetColor(property);
            Shader shader = material.shader;
            int index = shader == null ? -1 : shader.FindPropertyIndex(property);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && index >= 0 &&
                shader.GetPropertyType(index) == UnityEngine.Rendering.ShaderPropertyType.Color)
                color = color.linear;
            return color;
        }

        private static Color ColdAlbedo(Color first, Color second, Color sample, bool packed)
        {
            Color value = packed ? Color.Lerp(first, second, Mathf.Clamp01(sample.g)) : first * sample;
            return new Color(Mathf.Clamp01(value.r), Mathf.Clamp01(value.g), Mathf.Clamp01(value.b), 1f);
        }

        private static Color? SampleColdColor(Material material)
        {
            if (material == null) return null;
            bool packed = material.HasProperty("_ColorA") && material.HasProperty("_ColorB");
            Color first = packed ? ShaderColor(material, "_ColorA") :
                (material.HasProperty("_Color") ? ShaderColor(material, "_Color") : Color.white);
            Color second = packed ? ShaderColor(material, "_ColorB") : first;
            if (!Finite(first) || !Finite(second)) return null;
            Texture texture = material.mainTexture;
            if (texture == null) return ColdAlbedo(first, second, Color.white, packed);
            // GPU downsample supports non-readable native textures. Read albedo
            // only, not the heated/emissive output of the physical-metal shader.
            RenderTexture active = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D readable = null;
            try
            {
                temporary = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0f, 0f, 8f, 8f), 0, 0);
                readable.Apply();
                Color sum = Color.clear;
                var pixels = readable.GetPixels();
                foreach (Color sample in pixels) sum += ColdAlbedo(first, second, sample, packed);
                Color result = sum / pixels.Length;
                result.a = 1f;
                return Finite(result) ? (Color?)result : null;
            }
            finally
            {
                RenderTexture.active = active;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }

        internal static void Clear()
        {
            foreach (Dictionary<int, Material> variants in Tinted.Values)
                foreach (Material material in variants.Values)
                    if (material != null) UnityEngine.Object.Destroy(material);
            Tinted.Clear();
            Colors.Clear();
            RawColors.Clear();
            Retry.Clear();
            Warned.Clear();
            nextTick = 0f;
        }
    }
}
