using System;
using System.Collections.Generic;
using System.Reflection;
using Alta;
using Alta.Inventory;
using Alta.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestored
{
	// Token: 0x0200001C RID: 28
	internal static class Prefabs
	{
		// Token: 0x06000084 RID: 132 RVA: 0x00007EB8 File Offset: 0x000060B8
		internal static bool IsArmor(uint prefabHash)
		{
			int num = Catalog.PieceOf(prefabHash);
			return num >= 0 && Prefabs.ByPiece[num] != null;
		}

		// Token: 0x06000085 RID: 133 RVA: 0x00007EE0 File Offset: 0x000060E0
		internal static string RefusedLine()
		{
			if (!Prefabs.Done)
			{
				return "not registered";
			}
			List<string> list = new List<string>();
			for (int i = 0; i < Catalog.Count; i++)
			{
				if (Prefabs.Refused[i] != null)
				{
					list.Add(Catalog.Id(i));
				}
			}
			if (ArmorStand.Prefab == null) list.Add("armorstand");
            if (!ArmorCrafting.Ready) list.Add("crafting");
			return string.Join(",", list.ToArray());
		}

		// Token: 0x06000086 RID: 134 RVA: 0x00007F38 File Offset: 0x00006138
		internal static void Register(string seam)
		{
			if (Prefabs.Done || Prefabs._busy)
			{
				return;
			}
            // Harmony seams may still run after initialization has failed.
            // Never index an empty/partial pack or publish invalid armor prefabs.
            if (!Pack.Loaded)
            {
                if (!Prefabs._packWaitLogged)
                {
                    Prefabs._packWaitLogged = true;
                    Mod.Log.Error("armor registration skipped: armor.pack is not fully loaded; fix the earlier pack error and restart");
                }
                return;
            }
			Prefabs._busy = true;
			try
			{
				FieldInfo field = typeof(HashedGeneralValue<Item>).GetField("items", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method = typeof(PrefabManager).GetMethod("AddToPrefabMap", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo field2 = typeof(NetworkPrefab).GetField("hash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo field3 = typeof(HashedGeneralValue).GetField("hash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field == null || method == null || field2 == null || field3 == null)
				{
					Prefabs.Done = true;
					Mod.Log.Error("a game seam moved (Item.items, AddToPrefabMap, NetworkPrefab.hash or HashedGeneralValue.hash); no armor registered");
				}
				else
				{
					PrefabManager.PrepareSpawnSetups();
					HashedGeneralValue<Item>.CheckItems();
					Dictionary<uint, Item> dictionary = (Dictionary<uint, Item>)field.GetValue(null);
					if (dictionary == null || dictionary.Count == 0)
					{
						if (!Prefabs._waitLogged)
						{
							Mod.Log.Msg("item registry not loaded yet (" + seam + "); retrying");
						}
						Prefabs._waitLogged = true;
					}
					else
					{
						Prefabs.Done = true;
						Prefabs.Seam = seam;
						NetworkPrefab prefab = PrefabManager.GetPrefab(23950U);
						Pickup pickup = (prefab != null) ? prefab.GetComponent<Pickup>() : null;
						Item item = (pickup != null) ? pickup.Item : null;
						if (prefab == null || prefab.name != "Wooden Stirring Spoon" || item == null || item.Hash != 41010U)
						{
							for (int i = 0; i < Catalog.Count; i++)
							{
								Prefabs.Refused[i] = "template " + 23950U + " is not Wooden Stirring Spoon here";
							}
							Mod.Log.Error(string.Concat(new object[]
							{
								"template ",
								23950U,
								" is not Wooden Stirring Spoon with Item ",
								41010U,
								"; no armor registered"
							}));
						}
						else
						{
							Prefabs._holder = new GameObject("ArmorRestored Templates");
							Prefabs._holder.SetActive(false);
							Object.DontDestroyOnLoad(Prefabs._holder);
							int num = 0;
							for (int j = 0; j < Catalog.Count; j++)
							{
								try
								{
									Prefabs.Refused[j] = Prefabs.Build(j, prefab, item, dictionary, method, field2, field3);
									if (Prefabs.Refused[j] == null)
									{
										num++;
									}
									else
									{
										Mod.Log.Error(Catalog.Id(j) + " refused: " + Prefabs.Refused[j]);
									}
								}
								catch (Exception ex)
								{
									Prefabs.Refused[j] = "threw " + ex.Message;
									Mod.Log.Error(Catalog.Id(j) + ": " + ex);
								}
							}
							ArmorStand.Register(Prefabs._holder, Prefabs.ByPiece, Prefabs.Items);
                            ArmorCrafting.Register(Prefabs._holder, Prefabs.ByPiece, Prefabs.Items);
							FieldInfo field4 = typeof(HashedGeneralValue<Item>).GetField("itemsByName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
							if (field4 != null)
							{
								field4.SetValue(null, null);
							}
							Mod.Log.Msg(string.Concat(new object[]
							{
								num,
								"/",
								Catalog.Count,
								" armor pieces registered via ",
								seam,
								" (numbers ",
								52830,
								"-",
								Catalog.Number(Catalog.Count - 1),
								")."
							}));
							if (Mod.IsServer && Settings.Loot)
							{
								Loot.Add();
							}
						}
					}
				}
			}
			catch (Exception arg)
			{
				Prefabs.Done = true;
				Mod.Log.Error("registration failed: " + arg);
			}
			finally
			{
				Prefabs._busy = false;
			}
		}

		// Token: 0x06000087 RID: 135 RVA: 0x00008354 File Offset: 0x00006554
		internal static string Unregister()
		{
            ArmorCrafting.Unregister();
            ArmorStand.Unregister();
			FieldInfo field = typeof(PrefabManager).GetField("prefabMap", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo field2 = typeof(HashedGeneralValue<Item>).GetField("items", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			Dictionary<uint, NetworkPrefab> dictionary = (field != null) ? ((Dictionary<uint, NetworkPrefab>)field.GetValue(null)) : null;
			Dictionary<uint, Item> dictionary2 = (field2 != null) ? ((Dictionary<uint, Item>)field2.GetValue(null)) : null;
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < Catalog.Count; i++)
			{
				uint key = (uint)Catalog.Number(i);
				NetworkPrefab objA;
				if (dictionary != null && Prefabs.ByPiece[i] != null && dictionary.TryGetValue(key, out objA) && object.ReferenceEquals(objA, Prefabs.ByPiece[i]) && dictionary.Remove(key))
				{
					num++;
				}
				Item objA2;
				if (dictionary2 != null && Prefabs.Items[i] != null && dictionary2.TryGetValue(key, out objA2) && object.ReferenceEquals(objA2, Prefabs.Items[i]) && dictionary2.Remove(key))
				{
					num2++;
				}
				if (Prefabs.Items[i] != null)
				{
					Object.Destroy(Prefabs.Items[i]);
				}
				Prefabs.ByPiece[i] = null;
				Prefabs.Items[i] = null;
				Prefabs.Refused[i] = null;
			}
			FieldInfo field3 = typeof(HashedGeneralValue<Item>).GetField("itemsByName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (field3 != null)
			{
				field3.SetValue(null, null);
			}
			if (Prefabs._holder != null)
			{
				Object.Destroy(Prefabs._holder);
			}
			Prefabs._holder = null;
			Prefabs.Done = false;
			Prefabs.Seam = null;
			Prefabs._waitLogged = false;
			return string.Concat(new object[]
			{
				"armor pieces removed: ",
				num,
				" prefabs, ",
				num2,
				" items."
			});
		}

		// Token: 0x06000088 RID: 136 RVA: 0x0000853C File Offset: 0x0000673C
		private static string Build(int piece, NetworkPrefab source, Item sourceItem, Dictionary<uint, Item> items, MethodInfo addToMap, FieldInfo prefabHash, FieldInfo itemHash)
		{
			uint num = (uint)Catalog.Number(piece);
			NetworkPrefab prefab = PrefabManager.GetPrefab(num);
			if (prefab != null)
			{
				return string.Concat(new object[]
				{
					"prefab ",
					num,
					" is already '",
					prefab.name,
					"'"
				});
			}
			Item item;
			if (items.TryGetValue(num, out item))
			{
				return string.Concat(new object[]
				{
					"Item ",
					num,
					" is already '",
					item.name,
					"'"
				});
			}
			PieceDef pieceDef = Pack.Pieces[piece];
			GameObject gameObject = Object.Instantiate<GameObject>(source.gameObject, Prefabs._holder.transform);
			gameObject.name = Catalog.Title(piece);
			NetworkPrefab component = gameObject.GetComponent<NetworkPrefab>();
			prefabHash.SetValue(component, (int)num);
			Transform transform = gameObject.transform;
			Transform transform2 = transform.Find("Tight Grip");
			Transform transform3 = transform.Find("Mesh");
			Transform transform4 = transform.Find("Collision");
			if (transform2 == null || transform3 == null || transform4 == null)
			{
				Object.DestroyImmediate(gameObject);
				return "template has no Tight Grip, Mesh or Collision child";
			}
			Object.DestroyImmediate(transform2.gameObject);
			for (int i = transform4.childCount - 1; i >= 0; i--)
			{
				Object.DestroyImmediate(transform4.GetChild(i).gameObject);
			}
			for (int j = 0; j < pieceDef.Colliders.Count; j++)
			{
				Prefabs.AddCollider(transform4, "Collider " + j, pieceDef.Colliders[j]);
			}
			LODGroup component2 = transform3.GetComponent<LODGroup>();
			if (component2 != null)
			{
				Object.DestroyImmediate(component2);
			}
			for (int k = transform3.childCount - 1; k >= 0; k--)
			{
				Object.DestroyImmediate(transform3.GetChild(k).gameObject);
			}
			if (!Mod.IsServer)
			{
				for (int l = 0; l < pieceDef.Pickup.Count; l++)
				{
					PickupPart pickupPart = pieceDef.Pickup[l];
					GameObject gameObject2 = new GameObject(Pack.Meshes[pickupPart.Mesh].Name);
					gameObject2.transform.SetParent(transform3, false);
					Prefabs.Place(gameObject2.transform, pickupPart.Local);
					gameObject2.AddComponent<MeshFilter>().sharedMesh = Pack.Meshes[pickupPart.Mesh].Get();
					gameObject2.AddComponent<MeshRenderer>().sharedMaterials = Pack.Mats(pickupPart.Mats);
				}
			}
			Pickup component3 = gameObject.GetComponent<Pickup>();
			if (component3 == null)
			{
				Object.DestroyImmediate(gameObject);
				return "template has no Pickup";
			}
			Prefabs.SetField(component3, "isTightGrab", false);
			Prefabs.SetField(component3, "grabPoints", new GrabPoint[0]);
			Collider[] componentsInChildren = component3.GetComponentsInChildren<Collider>(true);
			Prefabs.SetField(component3, "childrenColliders", componentsInChildren);
			HashSet<Collider> colliders = component3.Colliders;
			if (colliders != null)
			{
				colliders.Clear();
				foreach (Collider item2 in componentsInChildren)
				{
					colliders.Add(item2);
				}
			}
			Item item3 = Object.Instantiate<Item>(sourceItem);
			item3.name = Catalog.Title(piece);
			itemHash.SetValue(item3, (int)num);
			item3.Prefab = component;
			foreach (Pickup pickup in gameObject.GetComponentsInChildren<Pickup>(true))
			{
				if (object.ReferenceEquals(pickup.Item, sourceItem))
				{
					pickup.Item = item3;
				}
			}
			ArmorMaterials.Configure(piece, gameObject);
            ArmorCraftingServer.AddRecord(gameObject);
			addToMap.Invoke(null, new object[]
			{
				new NetworkPrefab[]
				{
					component
				}
			});
			if (!object.ReferenceEquals(PrefabManager.GetPrefab(num), component))
			{
				return "the prefab map did not take it";
			}
			items[num] = item3;
			Prefabs.ByPiece[piece] = component;
			Prefabs.Items[piece] = item3;
			return null;
		}

		// Token: 0x06000089 RID: 137 RVA: 0x00008958 File Offset: 0x00006B58
		private static void SetField(object o, string name, object value)
		{
			Type type = o.GetType();
			while (type != null)
			{
				FieldInfo field = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (!(field == null))
				{
					field.SetValue(o, value);
					return;
				}
				type = type.BaseType;
			}
			throw new MissingFieldException(o.GetType().Name, name);
		}

		// Token: 0x0600008A RID: 138 RVA: 0x000089AC File Offset: 0x00006BAC
		internal static void Place(Transform t, Matrix4x4 m)
		{
			t.localPosition = m.GetColumn(3);
			Vector3 vector = m.GetColumn(2);
			Vector3 vector2 = m.GetColumn(1);
			Vector3 vector3 = m.GetColumn(0);
			Quaternion quaternion = (vector.sqrMagnitude > 1E-08f && vector2.sqrMagnitude > 1E-08f) ? Quaternion.LookRotation(vector, vector2) : Quaternion.identity;
			t.localRotation = quaternion;
			float num = vector3.magnitude * ((Vector3.Dot(vector3, quaternion * Vector3.right) < 0f) ? -1f : 1f);
			t.localScale = new Vector3(num, vector2.magnitude, vector.magnitude);
		}

		// Token: 0x0600008B RID: 139 RVA: 0x00008A74 File Offset: 0x00006C74
		private static void AddCollider(Transform parent, string name, ColliderSpec c)
		{
			GameObject gameObject = new GameObject(name);
			gameObject.layer = parent.gameObject.layer;
			gameObject.transform.SetParent(parent, false);
			Prefabs.Place(gameObject.transform, c.Local);
			switch (c.Shape)
			{
			case 0:
			{
				BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
				boxCollider.center = c.Center;
				boxCollider.size = c.Size;
				return;
			}
			case 1:
			{
				CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
				capsuleCollider.center = c.Center;
				capsuleCollider.radius = c.Radius;
				capsuleCollider.height = c.Height;
				capsuleCollider.direction = c.Direction;
				return;
			}
			default:
			{
				SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
				sphereCollider.center = c.Center;
				sphereCollider.radius = c.Radius;
				return;
			}
			}
		}

		// Token: 0x04000098 RID: 152
		private const uint TemplatePrefab = 23950U;

		// Token: 0x04000099 RID: 153
		private const uint TemplateItem = 41010U;

		// Token: 0x0400009A RID: 154
		private const string TemplateName = "Wooden Stirring Spoon";

		// Token: 0x0400009B RID: 155
		private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		// Token: 0x0400009C RID: 156
		private const BindingFlags Stat = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		// Token: 0x0400009D RID: 157
		internal static readonly NetworkPrefab[] ByPiece = new NetworkPrefab[Catalog.Count];

		// Token: 0x0400009E RID: 158
		internal static readonly Item[] Items = new Item[Catalog.Count];

		// Token: 0x0400009F RID: 159
		internal static readonly string[] Refused = new string[Catalog.Count];

		// Token: 0x040000A0 RID: 160
		internal static bool Done;

		// Token: 0x040000A1 RID: 161
		internal static string Seam;

		// Token: 0x040000A2 RID: 162
		private static bool _busy;

        private static bool _packWaitLogged;

		// Token: 0x040000A3 RID: 163
		private static bool _waitLogged;

		// Token: 0x040000A4 RID: 164
		private static GameObject _holder;
	}
}
