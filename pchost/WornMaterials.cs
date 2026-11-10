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
        private static readonly Dictionary<int,Color> AuthoredColors = new Dictionary<int,Color>();
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
                if(piece / 8 == 3 && original.HasProperty("_Color")) AuthoredColors[tinted.GetInstanceID()]=original.GetColor("_Color");
                variants[id] = tinted;
            }
            if (Resolve(hash)) Paint(tinted, Colors[hash]);
            return tinted;
        }

        private sealed class WoodVariant
        {
            internal Material Material;
            internal bool Ready;
        }
        private static readonly Dictionary<uint,Dictionary<int,WoodVariant>> Woods = new Dictionary<uint,Dictionary<int,WoodVariant>>();
        internal static Material ApplyWood(uint hash,Material original)
        {
            if(hash==0U || original==null) return original;
            // Authored moss-hide and rope bindings are not wood surfaces.
            string name=original.name.ToLowerInvariant();
            if(!name.Contains("gotera_oak_bark") && !name.Contains("heartwood") &&
               !name.Contains("bark_edges") && !name.Contains("bark_ridges") && !name.Contains("cut_grain")) return original;
            Dictionary<int,WoodVariant> variants;
            if(!Woods.TryGetValue(hash,out variants)) {variants=new Dictionary<int,WoodVariant>();Woods.Add(hash,variants);}
            int id=original.GetInstanceID();WoodVariant variant;
            if(!variants.TryGetValue(id,out variant))
            {
                variant=new WoodVariant {Material=new Material(original)};
                variants.Add(id,variant);UpdateWood(hash,variant);
            }
            return variant.Material;
        }
        private static void UpdateWood(uint hash,WoodVariant variant)
        {
            if(variant.Ready || variant.Material==null) return;
            try
            {
                var physical=HashedGeneralValue<PhysicalMaterial>.Get(hash);
                if(physical==null || physical.Hash!=hash) return;
                Material wood=physical.GetNonAtlasedNonDeformingMaterial(0);
                if(wood==null || wood.mainTexture==null) return;
                Material surface=variant.Material;
                surface.name="ArmorRestored wood / "+wood.name;
                surface.mainTexture=wood.mainTexture;
                surface.mainTextureScale=wood.mainTextureScale;
                surface.mainTextureOffset=wood.mainTextureOffset;
                if(surface.HasProperty("_Color")) surface.SetColor("_Color",wood.HasProperty("_Color") ? wood.GetColor("_Color") : Color.white);
                foreach(string key in new[] {"_UseAtlas","_UseVertexColor","_Glow"}) if(surface.HasProperty(key)) surface.SetFloat(key,0f);
                variant.Ready=true;
            }
            catch(Exception error) {if(Warned.Add(hash)) Mod.Log.Warning("Wood surface "+hash+": "+error.GetBaseException().Message+"; keeping authored texture and retrying.");}
        }

        private sealed class LeatherVariant
        {
            internal Material Original, Material;
            internal Texture2D Texture;
            internal bool Ready;
        }
        private static readonly Dictionary<uint,Dictionary<int,LeatherVariant>> Leathers = new Dictionary<uint,Dictionary<int,LeatherVariant>>();
        internal static Material ApplyLeather(uint hash,Material original,string meshName)
        {
            if(hash==0U || original==null) return original;
            // Only soft hide is dyed. Hardware and neutral glove backing retain their maps.
            string surface=original.name.ToLowerInvariant();
            Texture source=original.mainTexture;
            string atlas=source==null ? "" : source.name;
            if(!surface.Contains("leather") && !surface.Contains("oxblood") &&
                atlas!="leather_armor_d" && atlas!="ATT_Leather_Albedo") return original;
            Dictionary<int,LeatherVariant> variants;
            if(!Leathers.TryGetValue(hash,out variants)) { variants=new Dictionary<int,LeatherVariant>(); Leathers.Add(hash,variants); }
            int id=original.GetInstanceID(); LeatherVariant entry;
            if(!variants.TryGetValue(id,out entry))
            {
                entry=new LeatherVariant { Original=original,Material=new Material(original) };
                entry.Material.name=original.name+" leather dye "+hash;
                variants.Add(id,entry);
                UpdateLeather(hash,entry);
            }
            return entry.Material;
        }
        private static Material NativeLeather(uint hash)
        {
            var physical=HashedGeneralValue<PhysicalMaterial>.Get(hash);
            if(physical==null || physical.Hash!=hash) return null;
            Material dye=physical.GetNonAtlasedNonDeformingMaterial(0);
            if(dye==null || !dye.HasProperty("_Color2")) dye=physical.GetMaterial(0);
            return dye!=null && dye.shader!=null && dye.shader.name=="Township Tale/RampLerp3" &&
                dye.HasProperty("_Sampler") && dye.HasProperty("_Color1") && dye.HasProperty("_Color2") ? dye : null;
        }
        private static float Luminance(Color color) { return .2126f*color.r+.7152f*color.g+.0722f*color.b; }
        private static void UpdateLeather(uint hash,LeatherVariant entry)
        {
            if(entry.Ready || entry.Original==null || entry.Material==null) return;
            RenderTexture previous=RenderTexture.active,temporary=null; Texture2D texture=null;
            try
            {
                Material dye=NativeLeather(hash); if(dye==null) return;
                Color dark=ShaderColor(dye,"_Color"),middle=ShaderColor(dye,"_Color1"),light=ShaderColor(dye,"_Color2");
                float knee=dye.HasProperty("_Color1Position") ? Mathf.Clamp(dye.GetFloat("_Color1Position"),.01f,.99f) : .5f;
                Texture source=entry.Original.mainTexture;
                if(source==null)
                {
                    if(entry.Material.HasProperty("_Color")) entry.Material.SetColor("_Color",middle);
                    entry.Ready=true;return;
                }
                temporary=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                Graphics.Blit(source,temporary); RenderTexture.active=temporary;
                texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,true);
                texture.name="ArmorRestored leather tones "+source.name+" "+hash;
                texture.filterMode=source.filterMode;texture.wrapMode=source.wrapMode;texture.anisoLevel=source.anisoLevel;
                texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply(false,false);
                var pixels=texture.GetPixels();var histogram=new int[256];
                for(int i=0;i<pixels.Length;i++) histogram[Mathf.Clamp(Mathf.RoundToInt(Luminance(pixels[i])*255f),0,255)]++;
                int median=0,total=histogram[0]; while(median<255 && total<pixels.Length/2) total+=histogram[++median];
                // Map authored median to the native undyed leather's 0.38 tone.
                float low=0f,mid=.5f,high=1f,referenceKnee=.5f;
                Material reference=NativeLeather(33384U);
                if(reference!=null)
                {
                    float a=Luminance(ShaderColor(reference,"_Color")),b=Luminance(ShaderColor(reference,"_Color1")),c=Luminance(ShaderColor(reference,"_Color2"));
                    if(a<b && b<c) {low=a;mid=b;high=c;referenceKnee=Mathf.Clamp(reference.GetFloat("_Color1Position"),.01f,.99f);}
                }
                float referenceTone=.38f<referenceKnee ? Mathf.Lerp(low,mid,.38f/referenceKnee) : Mathf.Lerp(mid,high,(.38f-referenceKnee)/(1f-referenceKnee));
                float scale=referenceTone/(Math.Max(1,median)/255f);
                for(int i=0;i<pixels.Length;i++)
                {
                    float value=Luminance(pixels[i])*scale;
                    float tone=value<mid ? referenceKnee*Mathf.InverseLerp(low,mid,value) : referenceKnee+(1f-referenceKnee)*Mathf.InverseLerp(mid,high,value);
                    Color color=tone<knee ? Color.Lerp(dark,middle,tone/knee) : Color.Lerp(middle,light,(tone-knee)/(1f-knee));
                    color.a=pixels[i].a;pixels[i]=color;
                }
                texture.SetPixels(pixels);texture.Apply(true,true);
                // Bake the ramp into the authored atlas; retain the original shader,
                // normal/metal maps, UVs, alpha and geometry on both render paths.
                entry.Material.mainTexture=texture;
                // The new atlas already contains the dye colors; don't multiply
                // it by the old brown/oxblood material color a second time.
                if(entry.Material.HasProperty("_Color")) entry.Material.SetColor("_Color",Color.white);
                if(entry.Material.HasProperty("_BaseColor")) entry.Material.SetColor("_BaseColor",Color.white);
                entry.Texture=texture;texture=null;entry.Ready=true;
            }
            catch(Exception error) { if(Warned.Add(hash)) Mod.Log.Warning("Leather dye "+hash+": "+error.GetBaseException().Message+"; retaining original texture and retrying."); }
            finally
            {
                RenderTexture.active=previous;
                if(temporary!=null) RenderTexture.ReleaseTemporary(temporary);
                if(texture!=null) UnityEngine.Object.Destroy(texture);
            }
        }

        private static void Paint(Material material, Color tint)
        {
            Color authored;
            if(AuthoredColors.TryGetValue(material.GetInstanceID(),out authored)) tint*=authored;
            tint.a = 1f;
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            foreach(var palette in Woods) foreach(var variant in palette.Value.Values) UpdateWood(palette.Key,variant);
            foreach(var palette in Leathers) foreach(var variant in palette.Value.Values) UpdateLeather(palette.Key,variant);
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
            foreach(var palette in Leathers) foreach(var variant in palette.Value.Values)
            {
                if(variant.Material!=null) UnityEngine.Object.Destroy(variant.Material);
                if(variant.Texture!=null) UnityEngine.Object.Destroy(variant.Texture);
            }
            foreach(var palette in Woods) foreach(var variant in palette.Value.Values)
                if(variant.Material!=null) UnityEngine.Object.Destroy(variant.Material);
            Woods.Clear();
            Leathers.Clear();
            AuthoredColors.Clear();
            Tinted.Clear();
            Colors.Clear();
            RawColors.Clear();
            Retry.Clear();
            Warned.Clear();
            nextTick = 0f;
        }
    }
}
