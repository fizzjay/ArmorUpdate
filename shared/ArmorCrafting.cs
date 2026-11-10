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
using Alta.Pages;
namespace ArmorRestored
#endif
{
    // Fixed immutable recipes, identical on all clients. No native serializer
    // transpiler and no runtime recipe edits/hash reuse. Wire 4 requires this set.
    internal static class ArmorCrafting
    {
        internal const uint RecipeStart = 60000U, PageStart = 60032U, GlyphStart = 52830U;
        internal static readonly GenericCraftingRecipe[] Recipes = new GenericCraftingRecipe[16];
        internal static readonly NetworkPrefab[] Pages = new NetworkPrefab[16];
        internal static readonly ArmorConstructionData.Plan[] Plans = new ArmorConstructionData.Plan[16];
        internal static byte[] Fingerprint;
        internal static string Status = "not registered";
        internal static bool Ready;
        internal static string Signature { get { return Ready && Fingerprint != null ? BitConverter.ToString(Fingerprint).Replace("-", "") + ":glyphs-52830-v1" : ""; } }
        private static bool hooks;
        private static PageCollection collection;
        private static readonly Glyph[] recipeGlyphs = new Glyph[16];
        private static readonly Glyph[] previousGlyphs = new Glyph[16];
        private static Item[] resultItems;
        private static readonly Item[] pageItems = new Item[16];
        private static readonly List<Object> owned = new List<Object>();
        private static readonly Dictionary<string, Sprite> glyphs = new Dictionary<string, Sprite>();
        internal static int Index(GenericCraftingRecipe recipe)
        {
            if (recipe == null) return -1;
            uint hash = recipe.Hash;
            if (hash < RecipeStart || hash >= RecipeStart+16) return -1;
            var registered = Recipes[hash-RecipeStart];
            return registered != null && registered.GetInstanceID() == recipe.GetInstanceID() && recipe.ResultHash == 52830U+hash-RecipeStart ? (int)(hash-RecipeStart) : -1;
        }
        internal static void Register(GameObject holder, NetworkPrefab[] armor, Item[] items)
        {
            if (Ready) return;
            try
            {
                if (holder == null || holder.activeInHierarchy) throw new InvalidOperationException("Inactive crafting staging required");
                #if !ARMOR_QUEST
                if (Mod.IsServer && !ArmorCraftingServer.Installed) ArmorCraftingServer.Install();
                if (Mod.IsServer && !ArmorCraftingServer.Installed) throw new InvalidOperationException("Crafting save/transaction backend unavailable");
#endif
                resultItems = items;
                InstallHooks();
                var templates = new Dictionary<uint, GenericCraftingPieceDefinition>();
#if ARMOR_QUEST
                foreach (GenericCraftingRecipe recipe in NativeEnumerable.Items<GenericCraftingRecipe>(HashedGeneralValue<GenericCraftingRecipe>.All))
#else
                foreach (GenericCraftingRecipe recipe in HashedGeneralValue<GenericCraftingRecipe>.All)
#endif
                    foreach (GenericCraftingPieceDefinition piece in recipe.Pieces)
                        if (piece != null && piece.source != null && piece.source.Prefab != null && !templates.ContainsKey(piece.source.Prefab.Hash))
                            templates.Add(piece.source.Prefab.Hash, piece);
                foreach (uint source in new uint[] { 47760U, 63204U, 50988U, 62594U })
                    if (!templates.ContainsKey(source)) throw new InvalidOperationException("Missing native ingredient template " + source);
                var paper = PrefabManager.GetPrefab(37066U);
                if (paper == null || Component<Pickup>(paper.gameObject) == null || Component<PageInstance>(paper.gameObject) == null)
                    throw new InvalidOperationException("Original Paper #37066 unavailable");
                collection = HashedGeneralValue<PageCollection>.Get(57634U);
                if (collection == null || collection.PreferredProvider != 2 || collection.Size + 16 > 255)
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
                LoadGlyphs();
                // Preflight every namespace before mutating native registries.
                for (int i = 0; i < 16; i++)
                {
                    if (armor[i] == null || items[i] == null) throw new InvalidOperationException("Missing armor result " + i);
                    uint pageHash = PageStart + (uint)i, recipeHash = RecipeStart + (uint)i, glyphHash = GlyphStart + (uint)i;
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
                for (int i = 0; i < 16; i++)
                {
                    var recipe = New<GenericCraftingRecipe>();
                    owned.Add(recipe); Recipes[i] = recipe;
                    recipe.name = "ArmorRestored fixed-v1 " + Catalog.Id(i);
                    Set(recipe, "hash", (int)(RecipeStart + (uint)i));
                    Set(recipe, "resultHash", armor[i].Hash);
                    var definitions = new GenericCraftingPieceDefinition[Plans[i].Ingredients.Length];
                    for (int j = 0; j < definitions.Length; j++) definitions[j] = BuildPiece(j, Plans[i].Ingredients[j], templates);
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
                    var glyph = New<Glyph>(); owned.Add(glyph); recipeGlyphs[i] = glyph; previousGlyphs[i] = items[i].Glyph;
                    glyph.name = "ArmorRestored " + Catalog.Id(i);
                    Set(glyph, "hash", (int)(GlyphStart + (uint)i));
                    Sprite sprite;
                    if (glyphs.TryGetValue(Plans[i].Glyph, out sprite)) Set(glyph, "sprite", sprite);
                    PublishGlyph(glyph);
                    Set(items[i], "glyph", glyph);
                    PublishRecipe(recipe);
                    BuildPage(i, holder, paper);
                }
                var bases = new List<Object>();
                foreach (Object value in collection.PageBases) bases.Add(value);
                foreach (var recipe in Recipes) bases.Add(recipe);
#if ARMOR_QUEST
                Set(collection, "pageBases", new Il2CppReferenceArray<Object>(bases.ToArray()));
#else
                Set(collection, "pageBases", bases.ToArray());
#endif
                SetStatic(typeof(GenericCraftingRecipe), "mappedResult", null);
                Ready = true;
                Status = "16 native recipes/pages; Leather and Metal only";
                Mod.Log.Msg("armor crafting: " + Status);
            }
            catch (Exception error)
            {
                Unregister();
                Status = "disabled: " + error.GetBaseException().Message;
                Mod.Log.Error("armor crafting " + Status);
            }
        }
        private static GenericCraftingPieceDefinition BuildPiece(int index, ArmorConstructionData.Ingredient part, Dictionary<uint, GenericCraftingPieceDefinition> templates)
        {
            var template = templates[part.Source];
            int count = part.Vertices.Length / 8;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int n = i * 8;
                vertices[i] = new Vector3(part.Vertices[n], part.Vertices[n+1], part.Vertices[n+2]);
                normals[i] = new Vector3(part.Vertices[n+3], part.Vertices[n+4], part.Vertices[n+5]);
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
            foreach (string field in new[] { "devianceToDamage", "progressToDevianceMultiplier", "damageSplitPerDot", "damageOverflowMultiplier", "devianceMultiplier", "nailDefinition" })
                Set(nails, field, Get(template.nailSettings, field));
            if (Get(nails, "nailDefinition") == null) Set(nails,"nailDefinition",GenericCraftingSettings.GenericNailDefaultDefinition);
            if (Get(nails,"nailDefinition") == null) throw new InvalidOperationException("Native nail definition unavailable");
            var pose = part.NailPose;
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
        private static void InstallHooks()
        {
            if (hooks) return;
            new HarmonyLib.Harmony("armorrestored.crafting-pages").Patch(AccessTools.Method(typeof(PageInstance), "Generate"),
                prefix: new HarmonyMethod(typeof(ArmorCrafting), "GeneratePage"));
            new HarmonyLib.Harmony("armorrestored.crafting-bench-guard").Patch(AccessTools.Method(typeof(GenericCraftingStation), "TryAddPiece"),
                prefix: new HarmonyMethod(typeof(ArmorCrafting), "AllowPiece"));
            hooks = true;
        }
        private static bool AllowPiece(GenericCraftingStation __instance)
        {
            var project = Get(__instance,"currentProject") as IProject;
            if (project == null || Index(project.Recipe) < 0) return true;
            var entity = __instance.Entity;
            return entity != null && ((entity.Prefab != null && entity.Prefab.Hash == 28396U) ||
                (entity.PrefabRoot != null && entity.PrefabRoot.Prefab != null && entity.PrefabRoot.Prefab.Hash == 28396U));
        }
        private static bool GeneratePage(PageInstance __instance)
        {
            if (__instance.Entity == null || __instance.Entity.Prefab == null) return true;
            uint hash = __instance.Entity.Prefab.Hash;
            if (hash < PageStart || hash >= PageStart + 16) return true;
            int index = (int)(hash - PageStart);
            if (Pages[index] == null || !Same(__instance.Entity.Prefab,Pages[index])) return true;
            if (!Ready || Recipes[index] == null) throw new InvalidOperationException("Armor recipe page is unavailable");
            if (__instance.Data == null) __instance.Set(Recipes[index].CreatePage(2));
            return false;
        }
        private static void BuildPage(int index, GameObject holder, NetworkPrefab paper)
        {
            GameObject root = Cast<GameObject>(Object.Instantiate(paper.gameObject, holder.transform));
            owned.Add(root); root.name = "Armor recipe " + Catalog.Title(index);
            var prefab = Component<NetworkPrefab>(root); Pages[index] = prefab;
            Set(prefab, "hash", (int)(PageStart + (uint)index)); Set(prefab, "SaveMode", 0);
            var item = Cast<Item>(Object.Instantiate(Component<Pickup>(paper.gameObject).Item));
            owned.Add(item); pageItems[index] = item; item.name = root.name; Set(item, "hash", (int)(PageStart + (uint)index)); item.Prefab = prefab;
            foreach (Pickup pickup in Pickups(root)) pickup.Item = item;
            PublishPage(prefab, item);
            root.SetActive(true); // inactive staging prevents initialization until spawn
        }
        internal static void Unregister()
        {
            Ready = false;
            if (collection != null)
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
            for (int i = 0; i < 16; i++)
            {
                if (resultItems != null && resultItems[i] != null && recipeGlyphs[i] != null) Set(resultItems[i],"glyph",previousGlyphs[i]);
                RemovePublished(i);
            }
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
            owned.Clear(); glyphs.Clear(); Array.Clear(Recipes,0,16); Array.Clear(Pages,0,16); Array.Clear(Plans,0,16);
            Array.Clear(pageItems,0,16); Array.Clear(recipeGlyphs,0,16); Array.Clear(previousGlyphs,0,16); resultItems = null;
            collection = null; SetStatic(typeof(GenericCraftingRecipe), "mappedResult", null);
            Status = "not registered";
        }
        private static void LoadGlyphs()
        {
            using (var stream = typeof(ArmorCrafting).Assembly.GetManifestResourceStream("ArmorRestored.recipe-glyphs"))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != 0x594C4741U || reader.ReadInt32() != 1 || reader.ReadInt32() != 14)
                    throw new InvalidDataException("Invalid glyph resource");
                for (int i = 0; i < 14; i++)
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
        private static IEnumerable<Pickup> Pickups(GameObject root)
        { foreach (UnityEngine.Component component in root.GetComponentsInChildren(Il2CppType.Of<Pickup>(),true)) yield return component.TryCast<Pickup>(); }
        private static void PublishGlyph(Glyph glyph) { HashedGeneralValue<Glyph>.items.Add(glyph.Hash,glyph); HashedGeneralValue<Glyph>.itemsByName = null; }
        private static void PublishRecipe(GenericCraftingRecipe recipe) { HashedGeneralValue<GenericCraftingRecipe>.items.Add(recipe.Hash,recipe); HashedGeneralValue<GenericCraftingRecipe>.itemsByName = null; }
        private static void PublishPage(NetworkPrefab prefab, Item item) { PrefabManager.prefabMap.Add(prefab.Hash,prefab); HashedGeneralValue<Item>.items.Add(item.Hash,item); HashedGeneralValue<Item>.itemsByName = null; }
        private static void RemovePublished(int index)
        {
            Glyph glyph; GenericCraftingRecipe recipe; NetworkPrefab page; Item item;
            uint glyphHash = GlyphStart+(uint)index, pageHash = PageStart+(uint)index, recipeHash = RecipeStart+(uint)index;
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
        private static IEnumerable<Pickup> Pickups(GameObject root) { return root.GetComponentsInChildren<Pickup>(true); }
        private static Dictionary<uint,T> Values<T>() { return (Dictionary<uint,T>)AccessTools.Field(typeof(HashedGeneralValue<>).MakeGenericType(typeof(T)),"items").GetValue(null); }
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
            uint glyphHash = GlyphStart+(uint)index, recipeHash = RecipeStart+(uint)index, pageHash = PageStart+(uint)index;
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
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                const BindingFlags f = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
                FieldInfo field = t.GetField(name,f); PropertyInfo property = t.GetProperty(name,f);
                Type type = field != null ? field.FieldType : property == null ? null : property.PropertyType;
                if (type == null) continue;
                if (type.IsEnum && value is int) value = Enum.ToObject(type,value);
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
