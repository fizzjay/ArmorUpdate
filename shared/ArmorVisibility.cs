using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
#if ARMOR_QUEST
using Il2Cpp;
using Il2CppAlta.QuickAccessActions;
using Il2CppInterop.Runtime;
namespace ArmorRestoredQuest
#else
using Alta.QuickAccessActions;
using Alta.Networking.Scripts.Player;
namespace ArmorRestored
#endif
{
    // Client-local display preferences. Never change equipment or the wire table.
    internal static class ArmorVisibility
    {
        private static bool loaded;
        private static int hidden;
        private static GameObject holder;
        private static readonly Transform[] buttons = new Transform[8];
        private static readonly GameObject[] slashes = new GameObject[8];
        private static readonly bool[] pressed = new bool[2];
        private static Material ink;
        private static Transform target;
        private static Mesh bubbleMesh;
        private static Material bubbleMaterial;
        private static int bubbleLayer;
        private static float retryBubble;
        private static string status;
        private static void Status(string message)
        {
            if (message == status) return;
            status = message;
            Mod.Log.Msg("visibility bubbles: " + message);
        }
        private static T Component<T>(GameObject obj) where T : UnityEngine.Component
        {
#if ARMOR_QUEST
            return obj.GetComponent(Il2CppType.Of<T>())?.TryCast<T>();
#else
            return obj.GetComponent<T>();
#endif
        }
        private static T Add<T>(GameObject obj) where T : UnityEngine.Component
        {
#if ARMOR_QUEST
            return obj.AddComponent(Il2CppType.Of<T>()).TryCast<T>();
#else
            return obj.AddComponent<T>();
#endif
        }
        private static object Read(object obj, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = obj.GetType().GetProperty(name, flags);
            if (property != null) return property.GetValue(obj, null);
            FieldInfo field = obj.GetType().GetField(name, flags);
            return field == null ? null : field.GetValue(obj);
        }
        private static bool NativeBubble()
        {
            if (bubbleMesh != null && bubbleMaterial != null) return true;
            if (Time.unscaledTime < retryBubble) return false;
            retryBubble = Time.unscaledTime + 1f;
#if ARMOR_QUEST
            var candidates = Resources.FindObjectsOfTypeAll(Il2CppType.Of<QuickAccessMenuBubble>());
#else
            var candidates = Resources.FindObjectsOfTypeAll<QuickAccessMenuBubble>();
#endif
            foreach (var value in candidates)
            {
#if ARMOR_QUEST
                var bubble = value == null ? null : value.TryCast<QuickAccessMenuBubble>();
#else
                var bubble = value;
#endif
                if (bubble == null) continue;
                Renderer renderer = Read(bubble, "bubbleRenderer") as Renderer;
                if (renderer == null) continue;
                MeshFilter filter = Component<MeshFilter>(renderer.gameObject);
                SkinnedMeshRenderer skinned = Component<SkinnedMeshRenderer>(renderer.gameObject);
                Mesh mesh = filter != null ? filter.sharedMesh : skinned == null ? null : skinned.sharedMesh;
#if ARMOR_QUEST
                Material material = Read(bubble, "activeBubbleMaterialAndroid") as Material;
#else
                Material material = Read(bubble, "activeBubbleMaterial") as Material;
#endif
                if (mesh == null || material == null) continue;
                bubbleMesh = mesh;
                bubbleMaterial = material;
                bubbleLayer = renderer.gameObject.layer;
                return true;
            }
            Status("waiting for native quick-access bubble mesh/material; open the quick-access wheel once");
            return false;
        }
        private static string PathName { get { return Path.Combine(Mod.DataDir, "visibility.txt"); } }

        internal static bool Hidden(int slot)
        {
            Load();
            return slot >= 0 && slot < 8 && (hidden & (1 << slot)) != 0;
        }
        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                int value;
                if (File.Exists(PathName) && int.TryParse(File.ReadAllText(PathName).Trim(), out value)) hidden = value & 255;
            }
            catch (Exception ex) { Mod.Log.Warning("armor visibility load: " + ex.Message); }
        }
        private static void Toggle(int slot)
        {
            Load();
            int previous = hidden;
            hidden ^= 1 << slot;
            try
            {
                // Commit before replacing the prior preference file.
                File.WriteAllText(PathName + ".tmp", hidden.ToString());
                if (File.Exists(PathName)) File.Copy(PathName, PathName + ".bak", true);
                File.Copy(PathName + ".tmp", PathName, true);
                File.Delete(PathName + ".tmp");
            }
            catch (Exception ex)
            {
                hidden = previous;
                Mod.Log.Warning("armor visibility save failed; change cancelled: " + ex.Message);
                return;
            }
            Mod.Log.Msg("local armor slot " + slot + (Hidden(slot) ? " hidden" : " visible"));
        }

        internal static void ClearButtons()
        {
            if (holder != null) Object.Destroy(holder);
            if (ink != null) Object.Destroy(ink);
            holder = null;
            ink = null;
            target = null;
            Array.Clear(buttons, 0, buttons.Length);
            Array.Clear(slashes, 0, slashes.Length);
            // Require a release after opening/switching targets before accepting a press.
            pressed[0] = pressed[1] = true;
        }

        // Return true while a free hand is at a button, reserving its grip from removal.
        internal static bool Hand(int hand, Vector3 position, bool down, bool free)
        {
            bool edge = down && !pressed[hand];
            pressed[hand] = down;
            if (holder == null || !free) return false;
            int nearest = -1;
            float distance = 0.075f;
            for (int slot = 0; slot < 8; slot++)
            {
                if (buttons[slot] == null) continue;
                float d = Vector3.Distance(position, buttons[slot].position);
                if (d < distance) { distance = d; nearest = slot; }
            }
            if (nearest < 0) return false;
            if (edge)
            {
                Toggle(nearest);
                slashes[nearest].SetActive(Hidden(nearest));
            }
            return true;
        }

        internal static void Show(Transform rig, Func<int, Vector3> anchor)
        {
            if (rig == null || !rig.gameObject.activeInHierarchy) { ClearButtons(); return; }
            if (!EquipmentMode.Same(target, rig)) ClearButtons();
            // VR cameras are not guaranteed to carry Unity's MainCamera tag.
            var local = PlayerController.Current;
            Transform view = local == null ? null : local.Head;
            if (view == null) { ClearButtons(); Status("waiting for local player head"); return; }
            if (holder == null)
            {
                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                if (shader == null) { Status("eye icon shader unavailable"); return; }
                if (!NativeBubble()) return;
                ink = new Material(shader);
                ink.color = new Color(0.6f, 1f, 1f, 1f);
                holder = new GameObject("Armor visibility eyes (local)");
                // Keep world scale/layer independent of the avatar skeleton.
                holder.layer = bubbleLayer;
                target = rig;
                for (int slot = 0; slot < 8; slot++)
                {
                    GameObject button = new GameObject("Visibility " + slot);
                    button.layer = bubbleLayer;
                    button.transform.SetParent(holder.transform, false);
                    buttons[slot] = button.transform;
                    // Reuse the exact native quick-access shell mesh and platform material.
                    GameObject shell = new GameObject("Native quick-access bubble visual");
                    shell.layer = bubbleLayer;
                    shell.transform.SetParent(button.transform, false);
                    Add<MeshFilter>(shell).sharedMesh = bubbleMesh;
                    Add<MeshRenderer>(shell).sharedMaterial = bubbleMaterial;
                    Vector3 size = bubbleMesh.bounds.size;
                    float diameter = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    float scale = 0.09f / Mathf.Max(0.001f, diameter);
                    shell.transform.localScale = Vector3.one * scale;
                    shell.transform.localPosition = -bubbleMesh.bounds.center * scale;
                    // Copy mesh/material only: no pooled bubble callbacks with null content.
                    Vector3[] eye = new Vector3[25];
                    for (int i = 0; i < eye.Length; i++)
                    {
                        float t = i * Mathf.PI * 2f / (eye.Length - 1);
                        eye[i] = new Vector3(Mathf.Cos(t) * 0.021f, Mathf.Sin(t) * 0.010f, -0.052f);
                    }
                    Stroke(button.transform, "eye", eye, true);
                    Stroke(button.transform, "pupil", Ring(0.005f, 16), true);
                    slashes[slot] = Stroke(button.transform, "hidden", new[] {
                        new Vector3(-0.022f, -0.022f, -0.054f), new Vector3(0.022f, 0.022f, -0.054f) }, false);
                }
                Mod.Log.Msg("visibility bubbles: created 8, mesh=" + bubbleMesh.name + " material="
                    + bubbleMaterial.name + " layer=" + bubbleLayer + " rig=" + rig.name);
            }
            Status("8 native bubble visuals active; outside dummy bounds, local saved visibility");
            Vector3 toward = view.position - rig.position;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.0001f) toward = -view.forward;
            toward.y = 0f;
            toward.Normalize();
            Vector3 right = Vector3.Cross(toward, Vector3.up).normalized;
            // Include the visible body AND worn armor, not just skeleton anchors.
            Bounds bounds = new Bounds(anchor(0), Vector3.one * 0.1f);
            for (int slot = 0; slot < 8; slot++) bounds.Encapsulate(anchor(slot));
#if ARMOR_QUEST
            var renderers = rig.GetComponentsInChildren(Il2CppType.Of<Renderer>(), false);
#else
            var renderers = rig.GetComponentsInChildren<Renderer>(false);
#endif
            foreach (var value in renderers)
            {
#if ARMOR_QUEST
                Renderer renderer = value == null ? null : value.TryCast<Renderer>();
#else
                Renderer renderer = value;
#endif
                if (renderer == null || !renderer.enabled) continue;
                Bounds candidate = renderer.bounds;
                if (candidate.size.magnitude > 4f || (candidate.center - rig.position).magnitude > 3f) continue;
                bounds.Encapsulate(candidate);
            }
            Vector3 extents = bounds.extents;
            float halfWidth = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.y) * extents.y + Mathf.Abs(right.z) * extents.z;
            float front = Vector3.Dot(bounds.center, toward) + Mathf.Abs(toward.x) * extents.x
                + Mathf.Abs(toward.y) * extents.y + Mathf.Abs(toward.z) * extents.z + 0.15f;
            for (int slot = 0; slot < 8; slot++)
            {
                Vector3 point = anchor(slot);
                float side = slot == 2 || slot == 4 || slot == 6 ? -1f : 1f;
                // Distinct left/right tasset buttons even though both share a hip anchor.
                float edge = Vector3.Dot(bounds.center, right) + side * (halfWidth + 0.12f);
                point += right * (edge - Vector3.Dot(point, right));
                point += toward * Mathf.Max(0f, front - Vector3.Dot(point, toward));
                // Separate near-identical shoulder/chest or paired hip anchors.
                for (int earlier = 0; earlier < slot; earlier++)
                    if (Vector3.Distance(point, buttons[earlier].position) < 0.12f) point += Vector3.down * 0.12f;
                buttons[slot].position = point;
                buttons[slot].rotation = Quaternion.LookRotation(point - view.position, Vector3.up);
                slashes[slot].SetActive(Hidden(slot));
            }
        }
        private static Vector3[] Ring(float radius, int count)
        {
            Vector3[] points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = i * Mathf.PI * 2f / count;
                points[i] = new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, -0.053f);
            }
            return points;
        }
        private static GameObject Stroke(Transform parent, string name, Vector3[] points, bool loop)
        {
            GameObject child = new GameObject(name);
            child.layer = bubbleLayer;
            child.transform.SetParent(parent, false);
#if ARMOR_QUEST
            LineRenderer line = child.AddComponent(Il2CppType.Of<LineRenderer>()).TryCast<LineRenderer>();
#else
            LineRenderer line = child.AddComponent<LineRenderer>();
#endif
            line.sharedMaterial = ink;
            line.useWorldSpace = false;
            line.loop = loop;
            line.startWidth = line.endWidth = 0.002f;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
            return child;
        }
    }
}
