using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;
#if ARMOR_QUEST
using Il2CppInterop.Runtime;
namespace ArmorRestoredQuest
#else
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
            Camera camera = Camera.main;
            if (camera == null) { ClearButtons(); return; }
            if (holder == null)
            {
                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                if (shader == null) return;
                ink = new Material(shader);
                ink.color = new Color(0.6f, 1f, 1f, 1f);
                holder = new GameObject("Armor visibility eyes (local)");
                holder.transform.SetParent(rig, false);
                target = rig;
                for (int slot = 0; slot < 8; slot++)
                {
                    GameObject button = new GameObject("Visibility " + slot);
                    button.transform.SetParent(holder.transform, false);
                    buttons[slot] = button.transform;
                    // Outline resembles a quick-access bubble, with an eye inside.
                    Stroke(button.transform, "bubble", Ring(0.029f, 32), true);
                    Vector3[] eye = new Vector3[25];
                    for (int i = 0; i < eye.Length; i++)
                    {
                        float t = i * Mathf.PI * 2f / (eye.Length - 1);
                        eye[i] = new Vector3(Mathf.Cos(t) * 0.021f, Mathf.Sin(t) * 0.010f, -0.001f);
                    }
                    Stroke(button.transform, "eye", eye, true);
                    Stroke(button.transform, "pupil", Ring(0.005f, 16), true);
                    slashes[slot] = Stroke(button.transform, "hidden", new[] {
                        new Vector3(-0.022f, -0.022f, -0.003f), new Vector3(0.022f, 0.022f, -0.003f) }, false);
                }
            }
            Vector3 right = camera.transform.right;
            for (int slot = 0; slot < 8; slot++)
            {
                Vector3 point = anchor(slot);
                float side = slot == 2 || slot == 4 || slot == 6 ? -1f : 1f;
                // Distinct left/right tasset buttons even though both share a hip anchor.
                buttons[slot].position = point + right * (side * 0.27f)
                    + (camera.transform.position - point).normalized * 0.16f;
                buttons[slot].rotation = camera.transform.rotation;
                slashes[slot].SetActive(Hidden(slot));
            }
        }
        private static Vector3[] Ring(float radius, int count)
        {
            Vector3[] points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = i * Mathf.PI * 2f / count;
                points[i] = new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, -0.002f);
            }
            return points;
        }
        private static GameObject Stroke(Transform parent, string name, Vector3[] points, bool loop)
        {
            GameObject child = new GameObject(name);
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
