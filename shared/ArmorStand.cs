using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
#if ARMOR_QUEST
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace ArmorRestoredQuest
#else
using Alta;
using Alta.Inventory;
using Alta.Networking;
namespace ArmorRestored
#endif
{
    // A stationary native network prefab, NOT a ninth player equipment slot.
    // Native PickupDock owns the actual pickups, including material/persistence data.
    internal static class ArmorStand
    {
        internal const uint Number = 52854U;
        internal const string Name = "ATT_ArmorStand";
        internal static NetworkPrefab Prefab;
        internal static string Status = "not registered";
        private static Mesh mesh;
        private static Material material;
        private static Texture2D texture;
        private static readonly List<Object> settings = new List<Object>();
        private static readonly string[] Slots = { "helmet", "chest", "shoulder_l", "shoulder_r",
            "gauntlet_l", "gauntlet_r", "tasset_l", "tasset_r" };
        // Model is in metres, upright with its base at y=0. Positive X is left
        // on the supplied model. Initial pickup display poses; not player offsets.
        private static readonly Vector3[] Mounts = {
            new Vector3(0f, 1.34f, -0.016f), new Vector3(0f, 1.02f, -0.025f),
            new Vector3(0.22f, 1.16f, -0.025f), new Vector3(-0.22f, 1.16f, -0.025f),
            new Vector3(0.31f, 0.64f, 0.02f), new Vector3(-0.31f, 0.64f, 0.02f),
            new Vector3(0.13f, 0.72f, -0.025f), new Vector3(-0.13f, 0.72f, -0.025f) };

        internal static void Register(GameObject staging, NetworkPrefab[] pieces, Item[] items)
        {
            if (Prefab != null) return;
            GameObject root = null;
            int settingsStart = settings.Count;
            try
            {
                if (!Pack.Loaded || staging == null || staging.activeInHierarchy || pieces.Length != 24 || items.Length != 24)
                    throw new InvalidOperationException("Stand requires inactive staging and all 24 armor templates");
                for (int i = 0; i < 24; i++)
                    if (pieces[i] == null || items[i] == null)
                        throw new InvalidOperationException("Stand requires armor template " + i);
                if (FindPrefab() != null || ItemNumberOccupied())
                    throw new InvalidOperationException("Stand number 52854 is already occupied");
                int ground = LayerMask.NameToLayer("Ground");
                if (ground < 0) throw new InvalidOperationException("Ground layer missing");
                root = new GameObject(Name);
                root.SetActive(false);
                root.transform.SetParent(staging.transform, false);
                root.layer = ground;
                NetworkEntity entity = Add<NetworkEntity>(root);
                NetworkPrefab prefab = Add<NetworkPrefab>(root);
                Set(prefab, "entity", entity);
                Set(prefab, "hash", (int)Number);
                Set(prefab, "SaveMode", 0);
                Bind(entity, new MonoBehaviour[0]);
                // Solid base/post only: do not block hands from reaching the armor.
                BoxCollider foot = Add<BoxCollider>(root);
                foot.center = new Vector3(0f, 0.105f, 0f);
                foot.size = new Vector3(0.704f, 0.21f, 0.508f);
                GameObject post = Child(root.transform, "Post", Vector3.zero, ground);
                BoxCollider pole = Add<BoxCollider>(post);
                pole.center = new Vector3(0f, 0.72f, -0.025f);
                pole.size = new Vector3(0.08f, 1.04f, 0.08f);
                if (prefab.Embedded.Count != 0) throw new InvalidOperationException("Stand embedded list is not empty");
                for (int slot = 0; slot < 8; slot++)
                    InstallDock(root, prefab, slot, pieces, items);
#if ARMOR_QUEST
                bool visuals = true;
#else
                bool visuals = !Mod.IsServer;
#endif
                if (visuals)
                {
                    LoadVisuals();
                    GameObject model = Child(root.transform, "Stand model", Vector3.zero, ground);
                    Add<MeshFilter>(model).sharedMesh = mesh;
                    Add<MeshRenderer>(model).sharedMaterial = material;
                }
                // ActiveSelf mirrors native prefab templates, but staging keeps all
                // Awake/OnEnable/network callbacks deferred until a real spawn.
                root.SetActive(true);
#if ARMOR_QUEST
                PrefabManager.prefabMap.Add(Number, prefab);
#else
                typeof(PrefabManager).GetMethod("AddToPrefabMap", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { new NetworkPrefab[] { prefab } });
#endif
                if (!Same(FindPrefab(), prefab))
                    throw new InvalidOperationException("Stand prefab map registration failed");
                Prefab = prefab;
                Status = "ready";
                Mod.Log.Msg("armor stand registered: " + Name + " number=" + Number + " docks=" + prefab.Embedded.Count);
            }
            catch (Exception ex)
            {
                // Remove only our own partially published template, never a collision.
                if (root != null) RemoveMap(Get<NetworkPrefab>(root));
                if (root != null) Object.DestroyImmediate(root);
                for (int i = settings.Count - 1; i >= settingsStart; i--)
                { Object.DestroyImmediate(settings[i]); settings.RemoveAt(i); }
                ClearVisuals();
                Status = ex.GetBaseException().Message;
                Mod.Log.Error("armor stand refused: " + Status);
            }
        }

        private static void InstallDock(GameObject root, NetworkPrefab prefab, int slot,
            NetworkPrefab[] pieces, Item[] items)
        {
            // Use the same trigger/snap geometry on PC and Quest, derived from the
            // existing pickup colliders, not renderer.bounds (empty on a server).
            Bounds bounds = PickupBounds(pieces[slot]);
            for (int style = 1; style < 3; style++) bounds.Encapsulate(PickupBounds(pieces[style * 8 + slot]));
            int layer = -1;
            foreach (Collider collider in Colliders(pieces[slot].gameObject))
                if (collider != null && !collider.isTrigger) { layer = collider.gameObject.layer; break; }
            if (layer < 0) throw new InvalidOperationException("Missing pickup collision layer for " + Slots[slot]);
            GameObject frame = Child(root.transform, Slots[slot], Mounts[slot] - bounds.center, layer);
            NetworkEntity entity = Add<NetworkEntity>(frame);
            Set(entity, "hash", slot + 1);
            PickupDockSettings config = NewSettings();
            config.name = "Armor stand " + Slots[slot];
            settings.Add(config);
            Set(config, "isStackingDisabled", true);
            Set(config, "allowPhysicalJoints", false);
            Set(config, "hoverOn", AnimationCurve.Linear(0f, 1f, 0.1f, 1f));
            Set(config, "hoverOff", AnimationCurve.Linear(0f, 1f, 0.1f, 1f));
            for (int style = 0; style < 3; style++) config.IncludedItems.Add(items[style * 8 + slot]);
            BoxCollider range = Add<BoxCollider>(frame);
            bounds.Expand(0.08f);
            range.center = bounds.center;
            range.size = bounds.size;
            range.isTrigger = true;
            PickupDock dock = Add<PickupDock>(frame);
            Set(dock, "entity", entity);
            Set(dock, "settings", config);
            Set(dock, "snapPoint", frame.transform);
            Set(dock, "pickupRange", range);
            dock.Channels = (InteractionChannel)1;
            // Grab the docked pickup itself, never the fixed stand socket.
            dock.IsInteractable = false;
            Bind(entity, new MonoBehaviour[] { dock });
            prefab.Embedded.Add(entity);
        }

        private static Bounds PickupBounds(NetworkPrefab prefab)
        {
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (Collider collider in Colliders(prefab.gameObject))
            {
                if (collider == null || collider.isTrigger) continue;
                Bounds local;
                BoxCollider box = Native<BoxCollider>(collider);
                SphereCollider sphere = Native<SphereCollider>(collider);
                CapsuleCollider capsule = Native<CapsuleCollider>(collider);
                if (box != null) local = new Bounds(box.center, box.size);
                else if (sphere != null) local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                else if (capsule != null)
                {
                    Vector3 size = Vector3.one * capsule.radius * 2f;
                    size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                    local = new Bounds(capsule.center, size);
                }
                else throw new InvalidOperationException("Unsupported armor pickup collider on " + prefab.name);
                Matrix4x4 matrix = prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    point = matrix.MultiplyPoint3x4(point);
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!any) throw new InvalidOperationException("No solid pickup colliders on " + prefab.name);
            return bounds;
        }

        // Native docking wins over the mod's body-proximity equip on release.
        // The server also checks actual occupancy before consuming an equip request.
        internal static bool Contains(Pickup pickup)
        {
            if (pickup == null || Prefab == null) return false;
            foreach (PickupDock dock in LiveDocks())
                if (dock.HasDockedPickup && Same(dock.DockedPickup, pickup)) return true;
            return false;
        }

        internal static bool NearSocket(Pickup pickup)
        {
            if (pickup == null || Prefab == null) return false;
            foreach (PickupDock dock in LiveDocks())
            {
                if (dock.HasDockedPickup && Same(dock.DockedPickup, pickup)) return true;
                if (dock.HasDockedPickup || !dock.Settings.IncludedItems.Contains(pickup.Item)) continue;
                BoxCollider range = Get<BoxCollider>(dock.gameObject);
                if (range == null) continue;
                Bounds area = range.bounds;
                area.Expand(0.08f);
                // Pickup pivots are not necessarily at their visible centre.
                foreach (Collider collider in Colliders(pickup.gameObject))
                    if (collider != null && !collider.isTrigger && area.Intersects(collider.bounds)) return true;
            }
            return false;
        }

        private static List<PickupDock> LiveDocks()
        {
            List<PickupDock> result = new List<PickupDock>();
#if ARMOR_QUEST
            foreach (Object value in Object.FindObjectsOfType(Il2CppType.Of<PickupDock>()))
#else
            foreach (Object value in Object.FindObjectsOfType(typeof(PickupDock)))
#endif
            {
                PickupDock dock = Native<PickupDock>(value);
                if (dock == null || !dock.gameObject.activeInHierarchy) continue;
                Transform parent = dock.transform.parent;
                NetworkPrefab prefab = parent == null ? null : Get<NetworkPrefab>(parent.gameObject);
                if (prefab != null && prefab.Hash == Number) result.Add(dock);
            }
            return result;
        }

        internal static void Unregister()
        {
            RemoveMap(Prefab);
            Prefab = null;
            foreach (Object value in settings) if (value != null) Object.Destroy(value);
            settings.Clear();
            ClearVisuals();
            Status = "not registered";
        }

        private static void ClearVisuals()
        {
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
            if (texture != null) Object.Destroy(texture);
            mesh = null; material = null; texture = null;
        }

        private static NetworkPrefab FindPrefab()
        {
            // Quest GetPrefab indexes the dictionary and throws for a new ID.
            // An absent ID is expected before registration, not a refusal.
#if ARMOR_QUEST
            var map = PrefabManager.prefabMap;
#else
            var map = (Dictionary<uint, NetworkPrefab>)typeof(PrefabManager)
                .GetField("prefabMap", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
#endif
            if (map == null) throw new InvalidOperationException("Stand prefab map is not loaded");
            NetworkPrefab prefab;
            return map.TryGetValue(Number, out prefab) ? prefab : null;
        }

        private static bool ItemNumberOccupied()
        {
#if ARMOR_QUEST
            return HashedGeneralValue<Item>.items.ContainsKey(Number);
#else
            var items = (Dictionary<uint, Item>)typeof(HashedGeneralValue<Item>)
                .GetField("items", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            return items.ContainsKey(Number);
#endif
        }

        private static void RemoveMap(NetworkPrefab expected)
        {
            if (expected == null) return;
#if ARMOR_QUEST
            var map = PrefabManager.prefabMap;
#else
            var map = (Dictionary<uint, NetworkPrefab>)typeof(PrefabManager)
                .GetField("prefabMap", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
#endif
            NetworkPrefab current;
            if (map != null && map.TryGetValue(Number, out current) && Same(current, expected)) map.Remove(Number);
        }

        private static void LoadVisuals()
        {
            if (mesh != null && material != null) return;
            using (Stream stream = typeof(ArmorStand).Assembly.GetManifestResourceStream("ArmorRestored.stand"))
            {
                if (stream == null) throw new InvalidDataException("Missing ArmorRestored.stand resource; rebuild both projects");
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (new string(reader.ReadChars(4)) != "ASTD" || reader.ReadInt32() != 1)
                        throw new InvalidDataException("Invalid stand resource version");
                    int count = reader.ReadInt32(), indexCount = reader.ReadInt32();
                    if (count <= 0 || count > 65535 || indexCount <= 0 || indexCount > 300000 || indexCount % 3 != 0)
                        throw new InvalidDataException("Invalid stand mesh sizes");
                    Vector3[] vertices = new Vector3[count], normals = new Vector3[count];
                    Vector2[] uv = new Vector2[count];
                    for (int i = 0; i < count; i++)
                    {
                        vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    }
                    int[] indices = new int[indexCount];
                    for (int i = 0; i < indexCount; i++)
                    {
                        indices[i] = reader.ReadInt32();
                        if (indices[i] < 0 || indices[i] >= count) throw new InvalidDataException("Invalid stand index");
                    }
                    int width = reader.ReadInt32(), height = reader.ReadInt32(), bytes = reader.ReadInt32();
                    if (width <= 0 || width > 2048 || height <= 0 || height > 2048 || bytes != width * height * 4)
                        throw new InvalidDataException("Invalid stand atlas size");
                    byte[] rgba = reader.ReadBytes(bytes);
                    if (rgba.Length != bytes || stream.Position != stream.Length) throw new InvalidDataException("Truncated/trailing stand data");
                    mesh = new Mesh(); mesh.name = "Armor stand";
                    texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
#if ARMOR_QUEST
                    mesh.vertices = new Il2CppStructArray<Vector3>(vertices);
                    mesh.normals = new Il2CppStructArray<Vector3>(normals);
                    mesh.uv = new Il2CppStructArray<Vector2>(uv);
                    mesh.triangles = new Il2CppStructArray<int>(indices);
                    texture.LoadRawTextureData(new Il2CppStructArray<byte>(rgba));
#else
                    mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = indices;
                    texture.LoadRawTextureData(rgba);
#endif
                    mesh.RecalculateBounds();
                    texture.name = "Stand Atlas"; texture.Apply(true, true);
                    Shader shader = Shader.Find("Township Tale/Cheap/SimpleStandard");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) throw new InvalidOperationException("No stand shader available");
                    material = new Material(shader); material.name = "Armor stand atlas";
                    material.enableInstancing = true;
                    material.SetColor("_Color", Color.white);
                    material.SetTexture("_MainTex", texture);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
                    Object.DontDestroyOnLoad(mesh); Object.DontDestroyOnLoad(texture); Object.DontDestroyOnLoad(material);
                }
            }
        }

        private static GameObject Child(Transform parent, string name, Vector3 at, int layer)
        {
            GameObject child = new GameObject(name); child.layer = layer;
            child.transform.SetParent(parent, false); child.transform.localPosition = at;
            return child;
        }

        private static PickupDockSettings NewSettings()
        {
#if ARMOR_QUEST
            return ScriptableObject.CreateInstance(Il2CppType.Of<PickupDockSettings>()).TryCast<PickupDockSettings>();
#else
            return ScriptableObject.CreateInstance<PickupDockSettings>();
#endif
        }

#if ARMOR_QUEST
        private static T Add<T>(GameObject obj) where T : Component { return obj.AddComponent(Il2CppType.Of<T>()).TryCast<T>(); }
        private static T Get<T>(GameObject obj) where T : Component { return obj.GetComponent(Il2CppType.Of<T>())?.TryCast<T>(); }
        private static T Native<T>(Object obj) where T : Il2CppObjectBase { return obj == null ? null : obj.TryCast<T>(); }
        private static bool Same(Il2CppObjectBase a, Il2CppObjectBase b) { return a != null && b != null && a.Pointer == b.Pointer; }
        private static void Bind(NetworkEntity entity, MonoBehaviour[] behaviours) { Set(entity, "networkEntityBehaviours", new Il2CppReferenceArray<MonoBehaviour>(behaviours)); }
        private static List<Collider> Colliders(GameObject obj)
        {
            List<Collider> result = new List<Collider>();
            foreach (Component value in obj.GetComponentsInChildren(Il2CppType.Of<Collider>(), true)) result.Add(value.TryCast<Collider>());
            return result;
        }
#else
        private static T Add<T>(GameObject obj) where T : Component { return (T)obj.AddComponent(typeof(T)); }
        private static T Get<T>(GameObject obj) where T : Component { return (T)obj.GetComponent(typeof(T)); }
        private static T Native<T>(Object obj) where T : class { return obj as T; }
        private static bool Same(Object a, Object b) { return a != null && object.ReferenceEquals(a, b); }
        private static void Bind(NetworkEntity entity, MonoBehaviour[] behaviours) { Set(entity, "networkEntityBehaviours", behaviours); }
        private static Collider[] Colliders(GameObject obj) { return obj.GetComponentsInChildren<Collider>(true); }
#endif

        private static void Set(object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, flags);
                PropertyInfo property = type.GetProperty(name, flags);
                Type memberType = field != null ? field.FieldType : property == null ? null : property.PropertyType;
                if (memberType == null) continue;
                if (memberType.IsEnum && value is int) value = Enum.ToObject(memberType, value);
                if (field != null) field.SetValue(target, value);
                else if (property.CanWrite) property.SetValue(target, value, null);
                else throw new InvalidOperationException(name + " is read only");
                return;
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }
    }
}
