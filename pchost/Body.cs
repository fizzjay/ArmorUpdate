using System;
using System.Collections.Generic;
using System.Reflection;
using Alta.Heat;
using Alta.Impact;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x02000003 RID: 3
	internal static class Body
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000002 RID: 2 RVA: 0x0000206F File Offset: 0x0000026F
		internal static bool Ready
		{
			get
			{
				return Body.DetectorSlot != null && Body.HeatSlot != null && Body.HeatChanged != null;
			}
		}

		// Token: 0x06000003 RID: 3 RVA: 0x00002098 File Offset: 0x00000298
		private static int ZoneOfDetector(Transform t)
		{
			string name = t.name;
			string a = (t.parent != null) ? t.parent.name : "";
			if (a == "Head")
			{
				return 0;
			}
			if (a == "Chest")
			{
				return 1;
			}
			if (a == "Cog")
			{
				return 2;
			}
			if (name.EndsWith("shoulder_L_mov"))
			{
				return 3;
			}
			if (name.EndsWith("shoulder_R_mov"))
			{
				return 4;
			}
			if (name.EndsWith("elbow_L_mov") || name.EndsWith("wrist_L_mov"))
			{
				return 5;
			}
			if (name.EndsWith("elbow_R_mov") || name.EndsWith("wrist_R_mov"))
			{
				return 6;
			}
			return -1;
		}

		// Token: 0x06000004 RID: 4 RVA: 0x00002154 File Offset: 0x00000354
		private static int ZoneOfHeat(Transform t)
		{
			string name = t.name;
			if (name.EndsWith("Head_Heat Point"))
			{
				return 0;
			}
			if (name.EndsWith("Chest_Heat Point"))
			{
				return 1;
			}
			if (name.EndsWith("Shoulder_L_Heat Point"))
			{
				return 3;
			}
			if (name.EndsWith("Shoulder_R_Heat Point"))
			{
				return 4;
			}
			if (name.EndsWith("Forearm_L_Heat Point") || name.EndsWith("Wrist_L_Heat Point"))
			{
				return 5;
			}
			if (name.EndsWith("Forearm_R_Heat Point") || name.EndsWith("Wrist_R_Heat Point"))
			{
				return 6;
			}
			return -1;
		}

		// Token: 0x06000005 RID: 5 RVA: 0x000021E0 File Offset: 0x000003E0
		internal static Wiring Wire(Transform root, bool zones)
		{
			Wiring wiring = new Wiring
			{
				Root = root,
				Zones = new EquipmentReference[7]
			};
			foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
			{
				if (transform.name.EndsWith("_jnt") && !wiring.Bones.ContainsKey(transform.name))
				{
					wiring.Bones[transform.name] = transform;
				}
			}
			if (!zones || !Body.Ready)
			{
				return wiring;
			}
			GameObject gameObject = new GameObject("ArmorRestored Zones");
			gameObject.transform.SetParent(root, false);
			for (int j = 0; j < 7; j++)
			{
				EquipmentReference equipmentReference = gameObject.AddComponent<EquipmentReference>();
				equipmentReference.Changed += new EquipmentReference.EquipmentChangedHandler(Body.Noop);
				wiring.Zones[j] = equipmentReference;
			}
			foreach (ImpactDetector impactDetector in root.GetComponentsInChildren<ImpactDetector>(true))
			{
				int num = Body.ZoneOfDetector(impactDetector.transform);
				if (num >= 0 && Body.DetectorSlot.GetValue(impactDetector) == null)
				{
					Body.DetectorSlot.SetValue(impactDetector, wiring.Zones[num]);
					if (wiring.ZoneDetectors[num] == null)
					{
						wiring.ZoneDetectors[num] = new List<ImpactDetector>();
					}
					wiring.ZoneDetectors[num].Add(impactDetector);
					wiring.Detectors++;
				}
			}
			foreach (HeatPoint heatPoint in root.GetComponentsInChildren<HeatPoint>(true))
			{
				int num2 = Body.ZoneOfHeat(heatPoint.transform);
				if (num2 >= 0 && Body.HeatSlot.GetValue(heatPoint) == null)
				{
					Body.HeatSlot.SetValue(heatPoint, wiring.Zones[num2]);
					wiring.Zones[num2].Changed += (EquipmentReference.EquipmentChangedHandler)Delegate.CreateDelegate(typeof(EquipmentReference.EquipmentChangedHandler), heatPoint, Body.HeatChanged);
					wiring.Heat++;
				}
			}
			return wiring;
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000023E1 File Offset: 0x000005E1
		private static void Noop(ArmorDefinition a)
		{
		}

		// Token: 0x06000007 RID: 7 RVA: 0x000023E4 File Offset: 0x000005E4
		internal static HealthObject Health(Wiring w)
		{
			foreach (List<ImpactDetector> list in w.ZoneDetectors)
			{
				if (list != null)
				{
					foreach (ImpactDetector impactDetector in list)
					{
						if (impactDetector != null && impactDetector.HealthTarget != null)
						{
							return impactDetector.HealthTarget;
						}
					}
				}
			}
			return null;
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002470 File Offset: 0x00000670
		internal static string ZoneOf(Wiring w, ImpactDetector d)
		{
			if (d == null)
			{
				return "?";
			}
			for (int i = 0; i < 7; i++)
			{
				if (w.ZoneDetectors[i] != null && w.ZoneDetectors[i].Contains(d))
				{
					return Body.ZoneNames[i];
				}
			}
			return d.name;
		}

		// Token: 0x06000009 RID: 9 RVA: 0x000024C0 File Offset: 0x000006C0
		internal static void Apply(Wiring w, int[] worn, uint[] materials = null)
		{
			if (w == null || w.Zones[0] == null)
			{
				return;
			}
			int num = (worn[6] >= 0) ? worn[6] : worn[7];
			int[] array = new int[]
			{
				worn[0],
				worn[1],
				num,
				worn[2],
				worn[3],
				worn[4],
				worn[5]
			};
			int[] slots = { 0, 1, worn[6] >= 0 ? 6 : 7, 2, 3, 4, 5 };
            for (int i = 0; i < 7; i++)
            {
                uint material = materials == null ? 0U : materials[slots[i]];
                ArmorDefinition armorDefinition = (array[i] >= 0) ? Catalog.MaterialDefinition(array[i], material) : null;
				if (!object.ReferenceEquals(w.Zones[i].Equipment, armorDefinition))
				{
					w.Zones[i].Equipment = armorDefinition;
				}
			}
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002570 File Offset: 0x00000770
		internal static string Describe(Wiring w)
		{
			if (w == null)
			{
				return "not wired";
			}
			List<string> list = new List<string>();
			for (int i = 0; i < 7; i++)
			{
				ArmorDefinition armorDefinition = (w.Zones[i] != null) ? w.Zones[i].Equipment : null;
				list.Add(Body.ZoneNames[i] + "=" + ((armorDefinition == null) ? "-" : armorDefinition.name));
			}
			return string.Concat(new object[]
			{
				w.Detectors,
				" hit zones, ",
				w.Heat,
				" heat points wired; ",
				string.Join(" ", list.ToArray())
			});
		}

		// Token: 0x0600000B RID: 11 RVA: 0x00002634 File Offset: 0x00000834
		internal static Vector3 Anchor(Wiring w, int slot)
		{
			string anchor = Catalog.Slots[slot].Anchor;
			Transform transform;
			if (w != null && w.Bones.TryGetValue(anchor, out transform) && !(transform == null))
			{
				Vector3 vector = transform.position;
				if (slot == 0)
				{
					vector += Vector3.up * 0.08f;
				}
				Transform transform2;
				Transform transform3;
				if ((slot == 6 || slot == 7) && w.Bones.TryGetValue("shoulder_L_jnt", out transform2) && w.Bones.TryGetValue("shoulder_R_jnt", out transform3))
				{
					Vector3 vector2 = transform2.position - transform3.position;
					vector2.y = 0f;
					if (vector2.sqrMagnitude > 0.0001f)
					{
						vector += vector2.normalized * ((slot == 6) ? 0.12f : -0.12f);
					}
				}
				return vector;
			}
			if (w == null || !(w.Root != null))
			{
				return Vector3.zero;
			}
			return w.Root.position;
		}

		// Token: 0x04000007 RID: 7
		internal const int ZoneCount = 7;

		// Token: 0x04000008 RID: 8
		private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		// Token: 0x04000009 RID: 9
		internal static readonly string[] ZoneNames = new string[]
		{
			"Head",
			"Chest",
			"Hips",
			"LeftShoulder",
			"RightShoulder",
			"LeftArm",
			"RightArm"
		};

		// Token: 0x0400000A RID: 10
		private static readonly FieldInfo DetectorSlot = typeof(ImpactDetector).GetField("equipmentSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		// Token: 0x0400000B RID: 11
		private static readonly FieldInfo HeatSlot = typeof(HeatPoint).GetField("equipmentSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		// Token: 0x0400000C RID: 12
		private static readonly MethodInfo HeatChanged = typeof(HeatPoint).GetMethod("EuipmentChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}
}
