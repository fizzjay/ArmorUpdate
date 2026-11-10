using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
#if ARMOR_QUEST
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppAlta.Crafting;
using Il2CppAlta.Blacksmithing;
using Il2CppAlta.Pages;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace ArmorRestoredQuest
#else
using Alta;
using Alta.Inventory;
using Alta.Networking;
using Alta.Crafting;
using Alta.Blacksmithing;
using Alta.Pages;
namespace ArmorRestored
#endif
{
    // Fixed immutable recipes, identical on all clients. No native serializer
    // transpiler and no runtime recipe edits/hash reuse. The shared crafting signature requires this set.
    internal static class ArmorCrafting
    {
        internal const uint RecipeStart = 60000U, PageStart = 60032U, GlyphStart = 52830U;
        internal static readonly GenericCraftingRecipe[] Recipes = new GenericCraftingRecipe[32];
        internal static readonly NetworkPrefab[] Pages = new NetworkPrefab[32];
        internal static readonly ArmorConstructionData.Plan[] Plans = new ArmorConstructionData.Plan[32];
        internal static byte[] Fingerprint;
        internal static string Status = "not registered";
        internal static bool Ready;
        internal static string Signature { get { return Ready && Fingerprint != null ? BitConverter.ToString(Fingerprint).Replace("-", "") + ":glyphs-52830-v1-worn-sections1-benchup2-hebios1-surfaces2" : ""; } }
        private static bool hooks;
        private static PageCollection collection;
        private static readonly Glyph[] recipeGlyphs = new Glyph[32];
        private static readonly Glyph[] previousGlyphs = new Glyph[32];
        private static Item[] resultItems;
        private static readonly Item[] pageItems = new Item[32];
        private static readonly List<Object> owned = new List<Object>();
        private static readonly Dictionary<string, Sprite> glyphs = new Dictionary<string, Sprite>();
        internal static int Index(GenericCraftingRecipe recipe)
        {
            if (recipe == null) return -1;
            uint hash = recipe.Hash;
            if (hash < RecipeStart || hash >= RecipeStart+32) return -1;
            var registered = Recipes[hash-RecipeStart];
            return registered != null && registered.GetInstanceID() == recipe.GetInstanceID() && recipe.ResultHash == (uint)Catalog.Number((int)(hash-RecipeStart)) ? (int)(hash-RecipeStart) : -1;
        }
        internal static void Register(GameObject holder, NetworkPrefab[] armor, Item[] items)
        {
            if (Ready) return;
            string stage = "initialization";
            try
            {
                if (holder == null || holder.activeInHierarchy) throw new InvalidOperationException("Inactive crafting staging required");
                #if !ARMOR_QUEST
                if (Mod.IsServer && !ArmorCraftingServer.Installed) ArmorCraftingServer.Install();
                if (Mod.IsServer && !ArmorCraftingServer.Installed) throw new InvalidOperationException("Crafting save/transaction backend unavailable");
#endif
                if (armor == null || items == null || armor.Length < Catalog.Count || items.Length < Catalog.Count)
                    throw new InvalidOperationException("Crafting requires all registered armor prefabs and result items");
#if !ARMOR_QUEST
                // Install may have succeeded on this later registration attempt.
                // Existing staged armor still needs the server save component.
                if (Mod.IsServer)
                    for (int i = 0; i < armor.Length; i++)
                        if (armor[i] != null) ArmorCraftingServer.AddRecord(armor[i].gameObject);
#endif
                resultItems = items;
                stage = "initializing native value registries";
                // Get/All initialize the lazy native maps. Accessing items directly
                // before this (especially Glyph on dedicated servers) sees null.
                InitializeRegistries();
                stage = "installing crafting hooks";
                InstallHooks();
                stage = "reading native ingredient templates";
                var templates = new Dictionary<uint, GenericCraftingPieceDefinition>();
#if ARMOR_QUEST
                foreach (GenericCraftingRecipe recipe in NativeEnumerable.Items<GenericCraftingRecipe>(HashedGeneralValue<GenericCraftingRecipe>.All))
#else
                foreach (GenericCraftingRecipe recipe in HashedGeneralValue<GenericCraftingRecipe>.All)
#endif
                {
                    if (recipe == null || recipe.Pieces == null) continue;
                    foreach (GenericCraftingPieceDefinition piece in recipe.Pieces)
                    {
                        if (piece == null || piece.source == null || piece.source.Prefab == null) continue;
                        uint hash = piece.source.Prefab.Hash;
                        GenericCraftingPieceDefinition previous;
                        if (!templates.TryGetValue(hash,out previous)) templates.Add(hash,piece);
                        else if (previous.nailSettings == null && piece.nailSettings != null) templates[hash] = piece;
                    }
                }
                foreach (uint source in new uint[] { 47760U, 63204U, 50988U, 62594U })
                    if (!templates.ContainsKey(source)) throw new InvalidOperationException("Missing native ingredient template " + source);
                stage = "resolving native recipe paper";
                var paper = ResolvePaper();
                // ResolvePaper loads the native prefab resources on Quest;
                // repair only after their Darksteel render assets can be loaded.
                RepairDarksteel();
                collection = HashedGeneralValue<PageCollection>.Get(57634U);
                if (collection == null || collection.PreferredProvider != 2 || collection.Size + 24 > 255)
                    throw new InvalidOperationException("Native crafting book collection unavailable/full");
                ArmorConstructionData data;
                using (Stream input = typeof(ArmorCrafting).Assembly.GetManifestResourceStream("ArmorRestored.construction.bin"))
                {
                    if (input == null) throw new InvalidDataException("Missing construction resource");
                    using (var memory = new MemoryStream())
                    {
                        input.CopyTo(memory);
                        byte[] bytes = memory.ToArray();
                        using (var sha = System.Security.Cryptography.SHA256.Create()) Fingerprint = sha.ComputeHash(bytes);
                        data = ArmorConstructionData.Read(new MemoryStream(bytes, false));
                    }
                }
                stage = "loading glyph textures";
                LoadGlyphs();
                if (collection.PageBases == null) throw new InvalidOperationException("Native crafting book has no page bases");
                // Preflight every namespace before mutating native registries.
                for (int i = 0; i < Catalog.Count; i++)
                {
                    if (i >= 16 && i < 24) continue;
                    if (armor[i] == null || items[i] == null) throw new InvalidOperationException("Missing armor result " + i);
                    stage = "checking IDs for slot " + i;
                    uint pageHash = PageStart + (uint)i, recipeHash = RecipeStart + (uint)i, glyphHash = (uint)Catalog.Number(i);
                    var conflicts = new List<string>();
                    var existingPage = FindPrefab(pageHash);
                    var existingItem = FindItem(pageHash);
                    var existingRecipe = FindGenericCraftingRecipe(recipeHash);
                    var existingGlyph = FindGlyph(glyphHash);
                    if (existingPage != null) conflicts.Add("page prefab " + pageHash + " (" + existingPage.name + ")");
                    if (existingItem != null) conflicts.Add("page item " + pageHash + " (" + existingItem.name + ")");
                    if (existingRecipe != null) conflicts.Add("recipe " + recipeHash + " (" + existingRecipe.name + ")");
                    if (existingGlyph != null) conflicts.Add("glyph " + glyphHash + " (" + existingGlyph.name + ")");
                    if (conflicts.Count != 0)
                        throw new InvalidOperationException("Crafting ID collision at slot " + i + ": " + string.Join(", ", conflicts.ToArray()));
                    Plans[i] = data.Find(Catalog.Id(i));
                    if (Plans[i] == null) throw new InvalidDataException("Missing eight-slot construction plan " + Catalog.Id(i));
                }
                for (int i = 0; i < Catalog.Count; i++)
                {
                    if (i >= 16 && i < 24) continue;
                    stage = "creating recipe for slot " + i;
                    var recipe = New<GenericCraftingRecipe>();
                    if (recipe == null) throw new InvalidOperationException("Native recipe allocation returned null");
                    owned.Add(recipe); Recipes[i] = recipe;
                    recipe.name = "ArmorRestored fixed-v1 " + Catalog.Id(i);
                    Set(recipe, "hash", (int)(RecipeStart + (uint)i));
                    Set(recipe, "resultHash", armor[i].Hash);
                    var definitions = new GenericCraftingPieceDefinition[Plans[i].Ingredients.Length];
                    for (int j = 0; j < definitions.Length; j++)
                    {
                        stage = "building slot " + i + " ingredient " + j + " source " + Plans[i].Ingredients[j].Source;
                        definitions[j] = BuildPiece(j, Plans[i], templates);
                    }
                    stage = "configuring recipe for slot " + i;
#if ARMOR_QUEST
                    Set(recipe, "pieces", new Il2CppReferenceArray<GenericCraftingPieceDefinition>(definitions));
#else
                    Set(recipe, "pieces", definitions);
#endif
                    Set(recipe, "spawnOffset", new SerializedTransform { position = Vector3.up * .35f, rotation = Quaternion.identity, scale = Vector3.one });
                    #if ARMOR_QUEST
                    Set(recipe, "uniqueGlyphs", new Il2CppReferenceArray<Glyph.Positioning>(0));
#else
                    Set(recipe, "uniqueGlyphs", new Glyph.Positioning[0]);
#endif
                    var glyph = New<Glyph>();
                    if (glyph == null) throw new InvalidOperationException("Native glyph allocation returned null");
                    owned.Add(glyph); recipeGlyphs[i] = glyph; previousGlyphs[i] = items[i].Glyph;
                    glyph.name = "ArmorRestored " + Catalog.Id(i);
                    Set(glyph, "hash", (int)((uint)Catalog.Number(i)));
                    Sprite sprite;
                    if (glyphs.TryGetValue(Plans[i].Glyph, out sprite)) Set(glyph, "sprite", sprite);
                    PublishGlyph(glyph);
                    Set(items[i], "glyph", glyph);
                    PublishRecipe(recipe);
                    if (!Same(FindGlyph((uint)Catalog.Number(i)),glyph) || !Same(FindGenericCraftingRecipe(RecipeStart + (uint)i),recipe))
                        throw new InvalidOperationException("Native registry did not retain armor recipe/glyph " + i);
                    stage = "building page for slot " + i;
                    BuildPage(i, holder, paper);
                }
                stage = "appending recipes to native book";
                var bases = new List<Object>();
                foreach (Object value in collection.PageBases) bases.Add(value);
                foreach (var recipe in Recipes) if (recipe != null) bases.Add(recipe);
#if ARMOR_QUEST
                Set(collection, "pageBases", new Il2CppReferenceArray<Object>(bases.ToArray()));
#else
                Set(collection, "pageBases", bases.ToArray());
#endif
                SetStatic(typeof(GenericCraftingRecipe), "mappedResult", null);
                Ready = true;
                Status = "24 native recipes/pages; Leather, Metal and Hebios";
                Mod.Log.Msg("armor crafting: " + Status);
                CraftLog("ready","registered signature="+Signature+" recipes="+Recipes.Length,0f);
            }
            catch (Exception error)
            {
                string failure = "disabled at " + stage + ": " + error.GetBaseException().Message;
                Mod.Log.Error("armor crafting " + failure + "\n" + error);
                try { Unregister(); }
                catch (Exception cleanupError) { Mod.Log.Error("armor crafting rollback failed: " + cleanupError); }
                finally { Ready = false; Status = failure; }
            }
        }
        private static GenericCraftingPieceDefinition BuildPiece(int index, ArmorConstructionData.Plan plan, Dictionary<uint, GenericCraftingPieceDefinition> templates)
        {
            var part = plan.Ingredients[index];
            Quaternion jigRotation; Vector3 jigLift;
            JigTransform(plan,out jigRotation,out jigLift);
            var template = templates[part.Source];
            int count = part.Vertices.Length / 8;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int n = i * 8;
                vertices[i] = jigRotation*new Vector3(part.Vertices[n], part.Vertices[n+1], part.Vertices[n+2])+jigLift;
                normals[i] = jigRotation*new Vector3(part.Vertices[n+3], part.Vertices[n+4], part.Vertices[n+5]);
                uv[i] = new Vector2(part.Vertices[n+6], part.Vertices[n+7]);
            }
            var mesh = new Mesh(); owned.Add(mesh); mesh.name = "Armor construction ingredient " + index;
#if ARMOR_QUEST
            mesh.vertices = new Il2CppStructArray<Vector3>(vertices);
            mesh.normals = new Il2CppStructArray<Vector3>(normals); mesh.uv = new Il2CppStructArray<Vector2>(uv);
#else
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv;
#endif
            // Native workbench pieces use one native material, not the worn shader.
            var indices = new List<int>(); foreach (int[] sub in part.Submeshes) indices.AddRange(sub);
#if ARMOR_QUEST
            mesh.triangles = new Il2CppStructArray<int>(indices.ToArray());
#else
            mesh.triangles = indices.ToArray();
#endif
            mesh.RecalculateBounds();
            var nails = new NailSettings();
            // Root ingredients may be nail-free and have no NailSettings at all.
            // Prefer this source's settings, otherwise use another native template.
            var nativeNails = template.nailSettings;
            if (nativeNails == null)
            {
                var sourceHashes = new List<uint>(templates.Keys); sourceHashes.Sort();
                foreach (uint sourceHash in sourceHashes)
                {
                    var candidate = templates[sourceHash];
                    if (candidate != null && candidate.nailSettings != null)
                    { nativeNails = candidate.nailSettings; break; }
                }
            }
            if (nativeNails == null)
                throw new InvalidOperationException("No native nail settings template for source " + part.Source);
            foreach (string field in new[] { "devianceToDamage", "progressToDevianceMultiplier", "damageSplitPerDot", "damageOverflowMultiplier", "devianceMultiplier", "nailDefinition" })
                Set(nails, field, Get(nativeNails, field));
            if (Get(nails, "nailDefinition") == null) Set(nails,"nailDefinition",GenericCraftingSettings.GenericNailDefaultDefinition);
            if (Get(nails,"nailDefinition") == null) throw new InvalidOperationException("Native nail definition unavailable");
            var pose = part.RequiresNail ? AccessibleNail(plan,index) : part.NailPose;
            if(part.RequiresNail)
            {
                Vector3 point=jigRotation*new Vector3(pose[0],pose[1],pose[2])+jigLift;
                Quaternion turn=jigRotation*new Quaternion(pose[3],pose[4],pose[5],pose[6]);
                pose=new[] {point.x,point.y,point.z,turn.x,turn.y,turn.z,turn.w};
            }
            if (part.RequiresNail)
                CraftLog("nail-"+plan.Id+"-"+index,"nail recipe="+plan.Id+" piece="+(index+1)+" source="+part.Source+
                    " level="+part.Level+" original="+new Vector3(part.NailPose[0],part.NailPose[1],part.NailPose[2]).ToString("F4")+
                    " selected="+new Vector3(pose[0],pose[1],pose[2]).ToString("F4")+
                    " outward="+(new Quaternion(pose[3],pose[4],pose[5],pose[6])*Vector3.back).ToString("F4"),0f);
            var positions = part.RequiresNail ? new[] { new SerializedTransform { position = new Vector3(pose[0],pose[1],pose[2]),
                rotation = new Quaternion(pose[3],pose[4],pose[5],pose[6]), scale = Vector3.one } } : new SerializedTransform[0];
#if ARMOR_QUEST
            Set(nails, "nails", new Il2CppReferenceArray<SerializedTransform>(positions));
#else
            Set(nails, "nails", positions);
#endif
            Set(nails, "nailTime", 90f); Set(nails, "progressHealth", .5f);
            Set(nails, "maxDeviance", (float)Math.Max(0, part.Clearance)); Set(nails, "maxDamagePerHit", .3f);
            var result = new GenericCraftingPieceDefinition();
            Set(result, "hash", index + 1); Set(result, "source", template.source); Set(result, "level", part.Level);
            Set(result, "mesh", mesh); Set(result, "collisionMesh", mesh);
            Set(result, "position", new SerializedTransform { position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one });
            Set(result, "failureHealth", 3f); Set(result, "isCritical", false); Set(result, "resultHash", 0);
            Set(result, "minChiselHitDamageToFall", .5f); Set(result, "nailSettings", nails);
            foreach (string field in new[] { "materialChannel", "defaultMaterial", "avoidsAtlasing" }) Set(result, field, Get(template, field));
            return result;
        }
        private static Vector3 JigUp(ArmorConstructionData.Plan plan)
        {
            // Verified against every nail of each complete assembly. Most jigs
            // keep their orientation; these need a quarter turn for upper access.
            return plan.Id=="metal.gauntlet_l" || plan.Id=="metal.tasset_l" || plan.Id=="metal.tasset_r" || plan.Id=="hebios.helmet" || plan.Id=="hebios.chest"
                ? Vector3.forward : Vector3.up;
        }
        private static void JigTransform(ArmorConstructionData.Plan plan,out Quaternion rotation,out Vector3 lift)
        {
            rotation=Quaternion.FromToRotation(JigUp(plan),Vector3.up);
            float minY=float.PositiveInfinity;
            foreach(var input in plan.Ingredients)
                for(int i=0;i<input.Vertices.Length;i+=8)
                    minY=Math.Min(minY,(rotation*new Vector3(input.Vertices[i],input.Vertices[i+1],input.Vertices[i+2])).y);
            // Native piecesParent is the workbench jig frame. Keep the complete
            // construction AND the unhammered heads above its local table plane.
            lift=new Vector3(0f,.06f-minY,0f);
        }
        private struct Surface
        {
            internal Vector3 A, B, C, Center, Normal;
            internal float Area;
            internal int Owner;
        }
        private static Vector3 Vertex(ArmorConstructionData.Ingredient part,int i)
        { int n=i*8; return new Vector3(part.Vertices[n],part.Vertices[n+1],part.Vertices[n+2]); }
        private static bool RayHits(Vector3 origin, Vector3 direction, Surface t)
        {
            Vector3 e1=t.B-t.A, e2=t.C-t.A, h=Vector3.Cross(direction,e2);
            float determinant=Vector3.Dot(e1,h);
            if (Math.Abs(determinant)<.00000001f) return false;
            float inv=1f/determinant;
            Vector3 s=origin-t.A; float u=inv*Vector3.Dot(s,h);
            if (u<0f || u>1f) return false;
            Vector3 q=Vector3.Cross(s,e1); float v=inv*Vector3.Dot(direction,q);
            return v>=0f && u+v<=1f && inv*Vector3.Dot(e2,q)>.001f;
        }
        private static float[] AccessibleNail(ArmorConstructionData.Plan plan,int index)
        {
            var surfaces=new List<Surface>();
            var bounds=new Bounds(); bool first=true;
            for (int j=0;j<plan.Ingredients.Length;j++)
            {
                var part=plan.Ingredients[j];
                foreach (var sub in part.Submeshes)
                    for (int k=0;k<sub.Length;k+=3)
                    {
                        Vector3 a=Vertex(part,sub[k]),b=Vertex(part,sub[k+1]),c=Vertex(part,sub[k+2]);
                        if (first) { bounds=new Bounds(a,Vector3.zero); first=false; }
                        bounds.Encapsulate(a);bounds.Encapsulate(b);bounds.Encapsulate(c);
                        Vector3 normal=Vector3.Cross(b-a,c-a); float area=normal.magnitude;
                        if (area<.00000001f) continue;
                        surfaces.Add(new Surface { A=a,B=b,C=c,Center=(a+b+c)/3f,Normal=normal/area,Area=area,Owner=j });
                    }
            }
            var candidates=surfaces.FindAll(t=>t.Owner==index);
            candidates.Sort((a,b)=>b.Area.CompareTo(a.Area));
            foreach (var candidate in candidates)
            {
                if(Vector3.Dot(candidate.Normal,JigUp(plan))<.25f) continue;
                // Reject inward-facing cavity surfaces even when a short ray
                // would fit. Check the COMPLETE assembly, including later pieces.
                if (Vector3.Dot(candidate.Normal,candidate.Center-bounds.center)<-.0001f) continue;
                bool blocked=false;
                Vector3 tangent=Vector3.Cross(candidate.Normal,Vector3.up);
                if (tangent.sqrMagnitude<.01f) tangent=Vector3.Cross(candidate.Normal,Vector3.right);
                tangent.Normalize(); Vector3 bitangent=Vector3.Cross(candidate.Normal,tangent);
                foreach (Vector3 offset in new[] { Vector3.zero,tangent*.015f,-tangent*.015f,bitangent*.015f,-bitangent*.015f })
                {
                    foreach (var obstacle in surfaces)
                        if (RayHits(candidate.Center+offset+candidate.Normal*.002f,candidate.Normal,obstacle))
                        { blocked=true;break; }
                    if (blocked) break;
                }
                if (blocked) continue;
                Quaternion rotation=Quaternion.FromToRotation(Vector3.forward,-candidate.Normal);
                // Native GenericCraftingNail.UpdatePosition extends its head
                // along local BACK, so forward must point INTO the assembly.
                Vector3 at=candidate.Center;
                return new[] {at.x,at.y,at.z,rotation.x,rotation.y,rotation.z,rotation.w};
            }
            throw new InvalidOperationException("No exterior hammer approach for " + plan.Id + " ingredient " + index);
        }
        private sealed class WornSplit
        {
            internal Mesh Mesh;
            internal int[] Groups, Sources;
        }
        private static readonly Dictionary<int,WornSplit> wornSplits=new Dictionary<int,WornSplit>();
        internal static Mesh SplitWorn(Mesh original,string name,int piece,uint fallback,
            Material[] originals,int[] palette,int slot,out Material[] materials)
        {
            byte[] groups=piece/8!=2 ? WornIngredientGroups.Get(name) : null;
            if(groups==null || palette==null || palette.Length<272)
            { materials=WornMaterials.Apply(piece,fallback,originals,name); return original; }
            WornSplit split; int id=original.GetInstanceID();
            if(!wornSplits.TryGetValue(id,out split))
            {
                var triangles=new List<int[]>(); var owners=new List<int>(); var sources=new List<int>();
                int cursor=0;
                for(int sub=0;sub<original.subMeshCount;sub++)
                {
                    var indices=original.GetTriangles(sub);
                    var buckets=new SortedDictionary<int,List<int>>();
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        if(cursor>=groups.Length) throw new InvalidDataException("Worn ingredient map truncated: "+name);
                        int group=groups[cursor++]; List<int> bucket;
                        if(!buckets.TryGetValue(group,out bucket)) { bucket=new List<int>(); buckets.Add(group,bucket); }
                        bucket.Add(indices[i]);bucket.Add(indices[i+1]);bucket.Add(indices[i+2]);
                    }
                    foreach(var entry in buckets)
                    { owners.Add(entry.Key);sources.Add(Math.Min(sub,originals.Length-1));triangles.Add(entry.Value.ToArray()); }
                }
                if(cursor!=groups.Length) throw new InvalidDataException("Worn ingredient map length mismatch: "+name);
                Mesh mesh=Cast<Mesh>(Object.Instantiate(original)); owned.Add(mesh);
                mesh.name=original.name+" ingredient sections";mesh.subMeshCount=triangles.Count;
                for(int sub=0;sub<triangles.Count;sub++)
                {
#if ARMOR_QUEST
                    mesh.SetTriangles(new Il2CppStructArray<int>(triangles[sub]),sub);
#else
                    mesh.SetTriangles(triangles[sub],sub);
#endif
                }
                split=new WornSplit {Mesh=mesh,Groups=owners.ToArray(),Sources=sources.ToArray()};wornSplits.Add(id,split);
                CraftLog("worn-map-"+name,"worn ingredient sections mesh="+name+" sections="+triangles.Count,0f);
            }
            materials=new Material[split.Groups.Length];
            for(int i=0;i<materials.Length;i++)
            {
                int group=split.Groups[i];
                materials[i]=group==255 ? originals[split.Sources[i]] :
                    ApplySurface(piece,group,WornIngredientGroups.Material(palette,slot,group,fallback),originals[split.Sources[i]],name);
            }
            return split.Mesh;
        }
        private static Material ApplySurface(int piece,int group,uint hash,Material original,string name)
        {
            var plan=piece>=0 && piece<Plans.Length ? Plans[piece] : null;
            if(plan!=null && group>=0 && group<plan.Ingredients.Length)
            {
                uint source=plan.Ingredients[group].Source;
                if(source==47760U || source==63204U) return WornMaterials.ApplyLeather(hash,original,name);
            }
            return WornMaterials.Apply(piece,hash,original,name);
        }
        private static void ConfigureDarksteelChannel(object channel,Material forging,Material cold)
        {
            Set(channel,"material",forging);
            Set(channel,"nonDeformingMaterial",cold);
            Set(channel,"atlasedMaterial",cold);
            Set(channel,"needsFixing",false);
            Set(channel,"offsetScale",new Vector4(0,0,1,1));
        }
        private static void RepairDarksteel()
        {
            // Repair native rendering channels, not distribution/loot weights.
            // Keep this optional: a missing visual asset must not disable crafting.
            try
            {
                PhysicalMaterial dark=HashedGeneralValue<PhysicalMaterial>.Get(55232U);
                if (dark==null) { CraftLog("darksteel-missing","Darksteel 55232 unavailable",0f); return; }
                Material forging=null, cold=null;
                foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (material==null) continue;
                    if (material.name=="Darksteel Alloy") forging=material;
                    if (material.name=="Darksteel Alloy NonForging") cold=material;
                }
                if (forging==null || cold==null)
                { CraftLog("darksteel-assets","Darksteel channels not replaced: forging="+(forging!=null)+" cold="+(cold!=null),0f); return; }
#if ARMOR_QUEST
                var channels=new PhysicalMaterial.MaterialChannel[6];
                for(int i=0;i<channels.Length;i++)
                {
                    channels[i]=new PhysicalMaterial.MaterialChannel();
                    ConfigureDarksteelChannel(channels[i],forging,cold);
                }
                dark.androidMaterialChannels=new Il2CppReferenceArray<PhysicalMaterial.MaterialChannel>(channels);
#else
                // PC's nested MaterialChannel is private. Derive the actual
                // array element type from its field; never name it in PC code.
                var channelField=AccessTools.Field(typeof(PhysicalMaterial),"materialChannels");
                if(channelField==null || !channelField.FieldType.IsArray)
                    throw new MissingFieldException("PhysicalMaterial.materialChannels array unavailable");
                Type channelType=channelField.FieldType.GetElementType();
                Array channels=Array.CreateInstance(channelType,6);
                for(int i=0;i<channels.Length;i++)
                {
                    object channel=Activator.CreateInstance(channelType,true);
                    ConfigureDarksteelChannel(channel,forging,cold);
                    channels.SetValue(channel,i);
                }
                channelField.SetValue(dark,channels);
#endif
                Set(dark,"material",forging); Set(dark,"nonDeformingMaterial",cold); Set(dark,"isShowingInList",true);
                var recipe=HashedGeneralValue<SmeltingRecipe>.Get(60118U);
                var upgrade=HashedGeneralValue<SmelterUpgrades>.Get(5674U);
                bool recipeAdded=false;
                if (recipe!=null && upgrade!=null)
                {
                    var availableMaterials=new List<PhysicalMaterial>(); bool darkListed=false;
#if ARMOR_QUEST
                    var existingMaterials=upgrade.physicalMaterials;
#else
                    var existingMaterials=(PhysicalMaterial[])Get(upgrade,"physicalMaterials");
#endif
                    if (existingMaterials!=null) foreach(var existing in existingMaterials)
                    { availableMaterials.Add(existing); if(existing!=null && existing.Hash==55232U) darkListed=true; }
                    if (!darkListed)
                    {
                        availableMaterials.Add(dark);
#if ARMOR_QUEST
                        upgrade.physicalMaterials=new Il2CppReferenceArray<PhysicalMaterial>(availableMaterials.ToArray());
#else
                        Set(upgrade,"physicalMaterials",availableMaterials.ToArray());
#endif
                    }
                    var recipes=new List<SmeltingRecipe>(); bool exists=false;
#if ARMOR_QUEST
                    var existingRecipes=upgrade.recipes;
#else
                    var existingRecipes=(SmeltingRecipe[])Get(upgrade,"recipes");
#endif
                    if (existingRecipes!=null) foreach(var existing in existingRecipes)
                    { recipes.Add(existing); if (existing!=null && existing.Hash==recipe.Hash) exists=true; }
                    if (!exists)
                    {
                        recipes.Add(recipe);
#if ARMOR_QUEST
                        upgrade.recipes=new Il2CppReferenceArray<SmeltingRecipe>(recipes.ToArray());
#else
                        Set(upgrade,"recipes",recipes.ToArray());
#endif
                        recipeAdded=true;
                    }
                }
                GameObject ingot=Resources.Load<GameObject>("Network Prefabs/Props/Ingots/Darksteel Ingot");
                if (ingot==null) ingot=Resources.Load<GameObject>("network prefabs/props/ingots/Darksteel Ingot");
                NetworkPrefab prefab=ingot==null ? null : Component<NetworkPrefab>(ingot);
                if (prefab!=null) Set(prefab,"canBeSpawnedThroughCommand",true);
                CraftLog("darksteel-ready","Darksteel 55232 visible; 6 native render channels repaired; command ingot="+(prefab!=null)+" smelting recipe added="+recipeAdded+"; natural spawning unchanged",0f);
            }
            catch(Exception error) { CraftLog("darksteel-error","Darksteel repair failed: "+error.GetBaseException().Message,0f); }
        }
        internal const string CraftingPatch = "creative3-diag1";
        private static readonly Dictionary<string,float> diagnosticTimes = new Dictionary<string,float>();
        internal static void CraftLog(string key,string message,float interval=3f)
        {
            float now=Time.realtimeSinceStartup, previous;
            if (diagnosticTimes.TryGetValue(key,out previous) && now-previous<interval) return;
            if (diagnosticTimes.Count>=256) diagnosticTimes.Clear();
            diagnosticTimes[key]=now;
#if ARMOR_QUEST
            const string side = "quest-client";
#else
            string side = Mod.IsServer ? "server" : "pc-client";
#endif
            Mod.Log.Msg("armor craft diag ["+CraftingPatch+" " + side + "] " + message);
        }
        private static void TrackDiagnostic(GenericCraftingStation __instance)
        {
            try
            {
                var project=Get(__instance,"currentProject") as IProject;
                if (project==null || Index(project.Recipe)<0) return;
                object tracked=Get(__instance,"trackedPickups");
                CraftLog("station-"+__instance.GetInstanceID(),"station="+__instance.GetInstanceID()+" recipe="+project.Recipe.Hash+
                    " tracked="+(tracked==null ? "null" : Convert.ToString(Get(tracked,"Count")))+" complete="+project.IsComplete,5f);
            }
            catch (Exception error) { CraftLog("track-diag-error","station diagnostic failed: "+error.GetBaseException().Message,10f); }
        }
        private static void InstallHooks()
        {
            if (hooks) return;
            var generate = AccessTools.Method(typeof(PageInstance), "Generate");
            var addPiece = AccessTools.Method(typeof(GenericCraftingStation), "TryAddPiece");
            if (generate == null || addPiece == null) throw new MissingMethodException("Native crafting page/bench hooks unavailable");
            new HarmonyLib.Harmony("armorrestored.crafting-pages").Patch(generate,
                prefix: new HarmonyMethod(typeof(ArmorCrafting), "GeneratePage"));
            new HarmonyLib.Harmony("armorrestored.crafting-bench-guard").Patch(addPiece,
                prefix: new HarmonyMethod(typeof(ArmorCrafting), "AllowPiece"));
            new HarmonyLib.Harmony("armorrestored.crafting-material-preview").Patch(
                AccessTools.Method(typeof(GenericCraftingPiece),"AcceptsComponent"),
                postfix: new HarmonyMethod(typeof(ArmorCrafting),"AcceptMaterial"));
            new HarmonyLib.Harmony("armorrestored.crafting-diagnostics").Patch(
                AccessTools.Method(typeof(GenericCraftingStation),"TrackPickups"),
                postfix: new HarmonyMethod(typeof(ArmorCrafting),"TrackDiagnostic"));
            CraftLog("hooks","native acceptance + station tracking diagnostic hooks installed",0f);
            hooks = true;
        }
        private static void AcceptMaterial(GenericCraftingPiece __instance, GenericCraftingComponent __0, ref bool __result)
        {
            if (!Ready || __0 == null) return;
            var project = Get(__instance,"project") as IProject;
            int index = project == null ? -1 : Index(project.Recipe);
            if (index < 0) return;
            if(RemovalGrace(project.Station)) { __result=false; return; }
            var definition = (GenericCraftingPieceDefinition)Get(__instance,"definition");
            uint source = definition.source.Prefab.Hash;
            string logKey="accept-"+__0.GetInstanceID()+"-"+definition.hash;
            string details="recipe="+project.Recipe.Hash+" piece="+definition.hash+" expectedSource="+source+
                " incomingSource="+(__0.Entity.Prefab==null ? 0U : __0.Entity.Prefab.Hash)+
                " incomingMaterial="+(__0.Material==null ? 0U : __0.Material.Hash)+
                " enabled="+__instance.IsEnabled+" filled="+__instance.IsFilled;
            if (!__result) { CraftLog(logKey,"native reject "+details); return; }
            // Creative builds may mix materials. Native source/level/fill checks
            // above remain authoritative; every input keeps its own provenance.
            CraftLog(logKey,"accepted "+details);
        }
        private static readonly Dictionary<int,float> removalGrace = new Dictionary<int,float>();
        internal static void BeginRemoval(GenericCraftingStation station)
        {
            if(station!=null) removalGrace[station.GetInstanceID()]=Time.unscaledTime+1f;
        }
        internal static bool RemovalGrace(GenericCraftingStation station)
        {
            if(station==null) return false;
            int id=station.GetInstanceID(); float until;
            if(!removalGrace.TryGetValue(id,out until)) return false;
            if(Time.unscaledTime<until) return true;
            removalGrace.Remove(id); return false;
        }
        private static bool AllowPiece(GenericCraftingStation __instance)
        {
            var project = Get(__instance,"currentProject") as IProject;
            if (project == null || Index(project.Recipe) < 0) return true;
            if(RemovalGrace(__instance)) return false;
            var entity = __instance.Entity;
            return entity != null && ((entity.Prefab != null && entity.Prefab.Hash == 28396U) ||
                (entity.PrefabRoot != null && entity.PrefabRoot.Prefab != null && entity.PrefabRoot.Prefab.Hash == 28396U));
        }
        private static bool GeneratePage(PageInstance __instance)
        {
            if (__instance.Entity == null || __instance.Entity.Prefab == null) return true;
            uint hash = __instance.Entity.Prefab.Hash;
            if (hash < PageStart || hash >= PageStart + 32) return true;
            int index = (int)(hash - PageStart);
            if (Pages[index] == null || !Same(__instance.Entity.Prefab,Pages[index])) return true;
            if (!Ready || Recipes[index] == null) throw new InvalidOperationException("Armor recipe page is unavailable");
            if (__instance.Data == null) __instance.Set(Recipes[index].CreatePage(2));
            return false;
        }
        private static Item PaperItem(NetworkPrefab prefab)
        {
            if (prefab == null) return null;
            var pickup = ChildComponent<Pickup>(prefab.gameObject);
            if (pickup == null) return null;
            return pickup.Item != null ? pickup.Item : FindItem(prefab.Hash);
        }
        private static bool IsPaper(NetworkPrefab prefab)
        {
            return prefab != null && ChildComponent<PageInstance>(prefab.gameObject) != null && PaperItem(prefab) != null;
        }
        private static NetworkPrefab ResolvePaper()
        {
            // Item.Prefab is NOT a stored reference on Quest: its getter calls
            // the throwing PrefabManager.GetPrefab. Enumerate the actual map instead.
            var original = FindPrefab(37066U);
            if (IsPaper(original)) return original;
            NetworkPrefab selected = null;
            bool selectedNamedPaper = false;
#if ARMOR_QUEST
            // Generated ValueCollection does not implicitly implement the
            // managed-looking IEnumerable wrapper. Copy directly, no bridge.
            var prefabValues = PrefabManager.prefabMap.Values;
            var nativePrefabs = new Il2CppReferenceArray<NetworkPrefab>(prefabValues.Count);
            prefabValues.CopyTo(nativePrefabs,0);
#else
            var map = (Dictionary<uint,NetworkPrefab>)AccessTools.Field(typeof(PrefabManager),"prefabMap").GetValue(null);
            var nativePrefabs = new List<NetworkPrefab>(map.Values);
#endif
            foreach (NetworkPrefab prefab in nativePrefabs)
            {
                if (prefab == null || (prefab.Hash >= PageStart && prefab.Hash < PageStart+32) || !IsPaper(prefab)) continue;
                var item = PaperItem(prefab);
                string name = (item.name ?? "") + " " + (prefab.name ?? "");
                bool namedPaper = name.IndexOf("paper",StringComparison.OrdinalIgnoreCase) >= 0;
                // Any native physical page supplies the required behavior. Prefer
                // paper by name; ties use hash so enumeration order cannot matter.
                if (selected == null || (namedPaper && !selectedNamedPaper) ||
                    (namedPaper == selectedNamedPaper && prefab.Hash < selected.Hash))
                { selected = prefab; selectedNamedPaper = namedPaper; }
            }
            int loadedFromFolder = 0, loadedPrefabCount = 0, loadedBookCount = 0;
            if (selected == null)
            {
                // PrepareSpawnSetups may already have run against a partial map.
                // Explicitly load the same resource folder used by the native
                // PC PrepareSpawnSetups, without clearing/rebuilding prefabMap.
#if ARMOR_QUEST
                var resources = Resources.LoadAll("Network Prefabs",Il2CppType.Of<NetworkPrefab>());
#else
                var resources = Resources.LoadAll<NetworkPrefab>("Network Prefabs");
#endif
                loadedFromFolder = resources.Length;
                foreach (Object value in resources)
                {
                    var candidate = Cast<NetworkPrefab>(value);
                    if (!IsPaper(candidate)) continue;
                    if (selected == null || candidate.Hash < selected.Hash) selected = candidate;
                }
#if ARMOR_QUEST
                var loadedPrefabs = Resources.FindObjectsOfTypeAll(Il2CppType.Of<NetworkPrefab>());
#else
                var loadedPrefabs = Resources.FindObjectsOfTypeAll<NetworkPrefab>();
#endif
                loadedPrefabCount = loadedPrefabs.Length;
                foreach (Object value in loadedPrefabs)
                {
                    var candidate = Cast<NetworkPrefab>(value);
                    // Only asset templates, never clone a live player's page.
                    if (candidate == null || candidate.gameObject.scene.IsValid() || !IsPaper(candidate)) continue;
                    if (selected == null || candidate.Hash < selected.Hash) selected = candidate;
                }
            }
            // Books hold the native paper asset directly. On Quest that asset
            // can be absent from prefabMap even though books already reference it.
            // Use this authored reference instead of fabricating a paper object.
            if (selected == null)
            {
#if ARMOR_QUEST
                var books = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Book>());
#else
                var books = Resources.FindObjectsOfTypeAll<Book>();
#endif
                loadedBookCount = books.Length;
                foreach (Object value in books)
                {
                    var book = Cast<Book>(value);
                    if (book == null) continue;
                    var paper = Get(book,"paperPrefab") as NetworkPrefab;
                    if (!IsPaper(paper)) continue;
                    if (selected == null || paper.Hash < selected.Hash) selected = paper;
                }
            }
            if (selected == null)
                throw new InvalidOperationException("No native paper template with Pickup, Item and PageInstance; #37066=" +
                    (original == null ? "absent" : original.name + ", pickup=" + (ChildComponent<Pickup>(original.gameObject)!=null) +
                     ", page=" + (ChildComponent<PageInstance>(original.gameObject)!=null) + ", item=" + (PaperItem(original)!=null)) +
                    "; folder=" + loadedFromFolder + ", loadedPrefabs=" + loadedPrefabCount + ", books=" + loadedBookCount +
                    "; " + PaperDiagnostics());
            Mod.Log.Msg("armor crafting: native paper fallback " + selected.Hash + " (" + selected.name + ")");
            return selected;
        }
        private static string PaperDiagnostics()
        {
            // Bounded diagnostics identify whether the APK has actual paper
            // components, rather than another round of guessed hash fallbacks.
#if ARMOR_QUEST
            var pages = Resources.FindObjectsOfTypeAll(Il2CppType.Of<PaperPageInstance>());
#else
            var pages = Resources.FindObjectsOfTypeAll<PaperPageInstance>();
#endif
            var details = new List<string>();
            foreach (Object value in pages)
            {
                var page = Cast<PaperPageInstance>(value);
                if (page == null || details.Count >= 6) continue;
                details.Add(page.name);
            }
            return "loadedPaperPages=" + pages.Length + " [" + string.Join(", ",details.ToArray()) + "]";
        }
        private static void BuildPage(int index, GameObject holder, NetworkPrefab paper)
        {
            GameObject root = Cast<GameObject>(Object.Instantiate(paper.gameObject, holder.transform));
            if (root == null) throw new InvalidOperationException("Could not clone recipe paper");
            owned.Add(root); root.name = "Armor recipe " + Catalog.Title(index);
            var prefab = Component<NetworkPrefab>(root);
            if (prefab == null || ChildComponent<PageInstance>(root) == null)
                throw new InvalidOperationException("Cloned paper is missing native prefab/page component");
            Pages[index] = prefab;
            Set(prefab, "hash", (int)(PageStart + (uint)index)); Set(prefab, "SaveMode", 0);
            var item = Cast<Item>(Object.Instantiate(PaperItem(paper)));
            if (item == null) throw new InvalidOperationException("Could not clone recipe paper item");
            owned.Add(item); pageItems[index] = item; item.name = root.name; Set(item, "hash", (int)(PageStart + (uint)index)); item.Prefab = prefab;
            foreach (Pickup pickup in Pickups(root)) if (pickup != null) pickup.Item = item;
            PublishPage(prefab, item);
            if (!Same(FindPrefab(PageStart + (uint)index),prefab) || !Same(FindItem(PageStart + (uint)index),item))
                throw new InvalidOperationException("Native registry did not retain armor recipe page " + index);
            root.SetActive(true); // inactive staging prevents initialization until spawn
        }
        internal static void Unregister()
        {
            Ready = false;
            if (collection != null && collection.PageBases != null)
            {
                var bases = new List<Object>();
                foreach (Object value in collection.PageBases)
                {
                    bool ours = false;
                    foreach (var recipe in Recipes) if (recipe != null && value != null && value.GetInstanceID() == recipe.GetInstanceID()) ours = true;
                    if (!ours) bases.Add(value);
                }
#if ARMOR_QUEST
                Set(collection, "pageBases", new Il2CppReferenceArray<Object>(bases.ToArray()));
#else
                Set(collection, "pageBases", bases.ToArray());
#endif
            }
            for (int i = 0; i < Catalog.Count; i++)
            {
                if (resultItems != null && resultItems[i] != null && recipeGlyphs[i] != null) Set(resultItems[i],"glyph",previousGlyphs[i]);
                RemovePublished(i);
            }
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
            owned.Clear(); wornSplits.Clear(); removalGrace.Clear(); glyphs.Clear(); Array.Clear(Recipes,0,32); Array.Clear(Pages,0,32); Array.Clear(Plans,0,32);
            Array.Clear(pageItems,0,32); Array.Clear(recipeGlyphs,0,32); Array.Clear(previousGlyphs,0,32); resultItems = null;
            collection = null; SetStatic(typeof(GenericCraftingRecipe), "mappedResult", null);
            Status = "not registered";
        }
        private static void LoadGlyphs()
        {
            using (var stream = typeof(ArmorCrafting).Assembly.GetManifestResourceStream("ArmorRestored.recipe-glyphs"))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != 0x594C4741U || reader.ReadInt32() != 1 || reader.ReadInt32() != 21)
                    throw new InvalidDataException("Invalid glyph resource");
                for (int i = 0; i < 21; i++)
                {
                    int length = reader.ReadInt32(); if (length < 1 || length > 100) throw new InvalidDataException("Invalid glyph name");
                    string name = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
                    int width = reader.ReadInt32(), height = reader.ReadInt32();
                    if (width != 256 || height != 256) throw new InvalidDataException("Invalid glyph dimensions");
                    byte[] pixels = reader.ReadBytes(width*height*4); if (pixels.Length != width*height*4) throw new EndOfStreamException();
                    var texture = new Texture2D(width,height,TextureFormat.RGBA32,false,false); owned.Add(texture);
#if ARMOR_QUEST
                    texture.LoadRawTextureData(new Il2CppStructArray<byte>(pixels));
#else
                    texture.LoadRawTextureData(pixels);
#endif
                    texture.Apply(false,true);
                    var sprite = Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),256f); owned.Add(sprite);
                    glyphs.Add(name,sprite);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing glyph data");
            }
        }
#if ARMOR_QUEST
        private static T New<T>() where T : ScriptableObject { return ScriptableObject.CreateInstance(Il2CppType.Of<T>()).TryCast<T>(); }
        private static T Cast<T>(Object value) where T : Il2CppObjectBase { return value == null ? null : value.TryCast<T>(); }
        private static T Component<T>(GameObject root) where T : UnityEngine.Component { return root.GetComponent(Il2CppType.Of<T>())?.TryCast<T>(); }
        private static T ChildComponent<T>(GameObject root) where T : UnityEngine.Component
        {
            if (root == null) return null;
            foreach (UnityEngine.Component component in root.GetComponentsInChildren(Il2CppType.Of<T>(),true))
            {
                var typed = component == null ? null : component.TryCast<T>();
                if (typed != null) return typed;
            }
            return null;
        }
        private static IEnumerable<Pickup> Pickups(GameObject root)
        { foreach (UnityEngine.Component component in root.GetComponentsInChildren(Il2CppType.Of<Pickup>(),true)) yield return component.TryCast<Pickup>(); }
        private static void PublishGlyph(Glyph glyph) { HashedGeneralValue<Glyph>.items.Add(glyph.Hash,glyph); HashedGeneralValue<Glyph>.itemsByName = null; }
        private static void PublishRecipe(GenericCraftingRecipe recipe) { HashedGeneralValue<GenericCraftingRecipe>.items.Add(recipe.Hash,recipe); HashedGeneralValue<GenericCraftingRecipe>.itemsByName = null; }
        private static void PublishPage(NetworkPrefab prefab, Item item) { PrefabManager.prefabMap.Add(prefab.Hash,prefab); HashedGeneralValue<Item>.items.Add(item.Hash,item); HashedGeneralValue<Item>.itemsByName = null; }
        private static void RemovePublished(int index)
        {
            Glyph glyph; GenericCraftingRecipe recipe; NetworkPrefab page; Item item;
            uint glyphHash = (uint)Catalog.Number(index), pageHash = PageStart+(uint)index, recipeHash = RecipeStart+(uint)index;
            if (recipeGlyphs[index] != null && HashedGeneralValue<Glyph>.items.TryGetValue(glyphHash,out glyph) && Same(glyph,recipeGlyphs[index])) HashedGeneralValue<Glyph>.items.Remove(glyphHash);
            HashedGeneralValue<Glyph>.itemsByName = null;
            if (Recipes[index] != null && HashedGeneralValue<GenericCraftingRecipe>.items.TryGetValue(recipeHash,out recipe) && Same(recipe,Recipes[index])) HashedGeneralValue<GenericCraftingRecipe>.items.Remove(recipeHash);
            if (Pages[index] != null && PrefabManager.prefabMap.TryGetValue(pageHash,out page) && Same(page,Pages[index])) PrefabManager.prefabMap.Remove(pageHash);
            if (pageItems[index] != null && HashedGeneralValue<Item>.items.TryGetValue(pageHash,out item) && Same(item,pageItems[index])) HashedGeneralValue<Item>.items.Remove(pageHash);
            HashedGeneralValue<GenericCraftingRecipe>.itemsByName = null; HashedGeneralValue<Item>.itemsByName = null;
        }
#else
        private static T New<T>() where T : ScriptableObject { return ScriptableObject.CreateInstance<T>(); }
        private static T Cast<T>(Object value) where T : class { return value as T; }
        private static T Component<T>(GameObject root) where T : UnityEngine.Component { return root.GetComponent<T>(); }
        private static T ChildComponent<T>(GameObject root) where T : UnityEngine.Component
        { return root == null ? null : root.GetComponentInChildren<T>(true); }
        private static IEnumerable<Pickup> Pickups(GameObject root) { return root.GetComponentsInChildren<Pickup>(true); }
        private static Dictionary<uint,T> Values<T>()
        {
            Type type = typeof(HashedGeneralValue<>).MakeGenericType(typeof(T));
            var field = AccessTools.Field(type,"items");
            if (field == null) throw new MissingMemberException(type.FullName,"items");
            var values = (Dictionary<uint,T>)field.GetValue(null);
            if (values == null)
            {
                var all = AccessTools.Property(type,"All");
                if (all == null) throw new MissingMemberException(type.FullName,"All");
                all.GetValue(null,null); // initialize through the native loader
                values = (Dictionary<uint,T>)field.GetValue(null);
            }
            if (values == null) throw new InvalidOperationException("Native registry remains unavailable: " + typeof(T).FullName);
            return values;
        }
        private static void PublishGlyph(Glyph glyph) { Values<Glyph>().Add(glyph.Hash,glyph); SetStatic(typeof(HashedGeneralValue<Glyph>),"itemsByName",null); }
        private static void PublishRecipe(GenericCraftingRecipe recipe) { Values<GenericCraftingRecipe>().Add(recipe.Hash,recipe); SetStatic(typeof(HashedGeneralValue<GenericCraftingRecipe>),"itemsByName",null); }
        private static void PublishPage(NetworkPrefab prefab, Item item)
        {
            AccessTools.Method(typeof(PrefabManager),"AddToPrefabMap").Invoke(null,new object[] { new[] { prefab } });
            Values<Item>().Add(item.Hash,item); SetStatic(typeof(HashedGeneralValue<Item>),"itemsByName",null);
        }
        private static void RemovePublished(int index)
        {
            Glyph glyph; GenericCraftingRecipe recipe; Item item;
            uint glyphHash = (uint)Catalog.Number(index), recipeHash = RecipeStart+(uint)index, pageHash = PageStart+(uint)index;
            if (recipeGlyphs[index] != null && Values<Glyph>().TryGetValue(glyphHash,out glyph) && Same(glyph,recipeGlyphs[index])) Values<Glyph>().Remove(glyphHash);
            SetStatic(typeof(HashedGeneralValue<Glyph>),"itemsByName",null);
            if (Recipes[index] != null && Values<GenericCraftingRecipe>().TryGetValue(recipeHash,out recipe) && Same(recipe,Recipes[index])) Values<GenericCraftingRecipe>().Remove(recipeHash);
            if (Pages[index] != null)
            {
                var map = (Dictionary<uint,NetworkPrefab>)AccessTools.Field(typeof(PrefabManager),"prefabMap").GetValue(null);
                NetworkPrefab existing;
                if (map.TryGetValue(PageStart+(uint)index,out existing) && existing == Pages[index]) map.Remove(PageStart+(uint)index);
                if (pageItems[index] != null && Values<Item>().TryGetValue(pageHash,out item) && Same(item,pageItems[index])) Values<Item>().Remove(pageHash);
            }
            SetStatic(typeof(HashedGeneralValue<GenericCraftingRecipe>),"itemsByName",null); SetStatic(typeof(HashedGeneralValue<Item>),"itemsByName",null);
        }
#endif
        private static void InitializeRegistries()
        {
            if (HashedGeneralValue<Glyph>.All == null || HashedGeneralValue<Item>.All == null ||
                HashedGeneralValue<GenericCraftingRecipe>.All == null)
                throw new InvalidOperationException("Native crafting registries are not available");
#if ARMOR_QUEST
            if (HashedGeneralValue<Glyph>.items == null || HashedGeneralValue<Item>.items == null ||
                HashedGeneralValue<GenericCraftingRecipe>.items == null || PrefabManager.prefabMap == null)
                throw new InvalidOperationException("Native crafting registry initialization did not populate maps");
#else
            Values<Glyph>(); Values<Item>(); Values<GenericCraftingRecipe>();
#endif
        }
        // GetPrefab/Get may index dictionaries and throw on Quest for free IDs.
        private static NetworkPrefab FindPrefab(uint hash)
        {
            NetworkPrefab value;
#if ARMOR_QUEST
            return PrefabManager.prefabMap.TryGetValue(hash,out value) ? value : null;
#else
            var map = (Dictionary<uint,NetworkPrefab>)AccessTools.Field(typeof(PrefabManager),"prefabMap").GetValue(null);
            return map.TryGetValue(hash,out value) ? value : null;
#endif
        }
        private static Item FindItem(uint hash)
        {
            Item value;
#if ARMOR_QUEST
            return HashedGeneralValue<Item>.items.TryGetValue(hash,out value) ? value : null;
#else
            return Values<Item>().TryGetValue(hash,out value) ? value : null;
#endif
        }
        private static GenericCraftingRecipe FindGenericCraftingRecipe(uint hash)
        {
            GenericCraftingRecipe value;
#if ARMOR_QUEST
            return HashedGeneralValue<GenericCraftingRecipe>.items.TryGetValue(hash,out value) ? value : null;
#else
            return Values<GenericCraftingRecipe>().TryGetValue(hash,out value) ? value : null;
#endif
        }
        private static Glyph FindGlyph(uint hash)
        {
            Glyph value;
#if ARMOR_QUEST
            return HashedGeneralValue<Glyph>.items.TryGetValue(hash,out value) ? value : null;
#else
            return Values<Glyph>().TryGetValue(hash,out value) ? value : null;
#endif
        }
        private static bool Same(Object a,Object b) { return a != null && b != null && a.GetInstanceID() == b.GetInstanceID(); }
        internal static object Get(object target, string name)
        {
            if (target == null) throw new InvalidOperationException("Cannot read native member " + name + " on null target");
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                const BindingFlags f = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
                FieldInfo field = t.GetField(name,f); if (field != null) return field.GetValue(target);
                PropertyInfo property = t.GetProperty(name,f); if (property != null) return property.GetValue(target,null);
            }
            throw new MissingMemberException(target.GetType().FullName,name);
        }
        internal static void Set(object target, string name, object value)
        {
            if (target == null) throw new InvalidOperationException("Cannot write native member " + name + " on null target");
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                const BindingFlags f = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
                FieldInfo field = t.GetField(name,f); PropertyInfo property = t.GetProperty(name,f);
                Type type = field != null ? field.FieldType : property == null ? null : property.PropertyType;
                if (type == null) continue;
                if (type.IsEnum && value is int) value = Enum.ToObject(type,value);
                else if (value != null && type.IsPrimitive && value is IConvertible && !type.IsInstanceOfType(value))
                    value = Convert.ChangeType(value,type,System.Globalization.CultureInfo.InvariantCulture);
                if (field != null) field.SetValue(target,value); else property.SetValue(target,value,null);
                return;
            }
            throw new MissingMemberException(target.GetType().FullName,name);
        }
        private static void SetStatic(Type type, string name, object value)
        {
            var field = AccessTools.Field(type,name);
            if (field != null) field.SetValue(null,value);
            else { var property = AccessTools.Property(type,name); if (property == null) throw new MissingMemberException(type.FullName,name); property.SetValue(null,value,null); }
        }
    }
}
