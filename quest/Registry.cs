using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace ArmorRestoredQuest
{
	// Token: 0x02000013 RID: 19
	internal static class Registry
	{
		// Token: 0x06000065 RID: 101 RVA: 0x00006628 File Offset: 0x00004828
		internal static bool IsOurs(uint hash)
		{
			int num = Catalog.PieceOf(hash);
			return num >= 0 && Registry.Prefabs[num] != null;
		}

		// Token: 0x06000066 RID: 102 RVA: 0x00006650 File Offset: 0x00004850
		internal static string Register()
		{
			if (Registry.Done)
			{
				return Registry._refused;
			}
			DateTime utcNow = DateTime.UtcNow;
			string refused;
			try
			{
				Crumb.At("register: PrepareSpawnSetups");
				PrefabManager.PrepareSpawnSetups();
				Crumb.At("register: CheckItems");
				HashedGeneralValue<Item>.CheckItems();
				Crumb.At("register: prefabMap");
				Dictionary<uint, NetworkPrefab> prefabMap = PrefabManager.prefabMap;
				Crumb.At("register: items");
				Dictionary<uint, Item> items = HashedGeneralValue<Item>.items;
				Registry.Done = true;
				if (prefabMap == null || items == null || items.Count == 0)
				{
					Registry._refused = "registry not loaded";
					Mod.Log.Error(string.Concat(new string[]
					{
						"the prefab map or the item list is not loaded (",
						(prefabMap == null) ? "no map" : (prefabMap.Count.ToString() + " prefabs"),
						", ",
						(items == null) ? "no items" : (items.Count.ToString() + " items"),
						"); no armor registered."
					}));
					refused = Registry._refused;
				}
				else
				{
					Crumb.At("register: template");
					NetworkPrefab prefab = PrefabManager.GetPrefab(23950U);
					Component component = (prefab == null) ? null : prefab.GetComponent(Il2CppType.Of<Pickup>());
					Pickup pickup = (component == null) ? null : component.TryCast<Pickup>();
					Item item = (pickup == null) ? null : pickup.Item;
					if (prefab == null || prefab.name != "Wooden Stirring Spoon" || item == null || item.Hash != 41010U)
					{
						Registry._refused = "template";
						Mod.Log.Error(string.Concat(new string[]
						{
							"template ",
							23950U.ToString(),
							" is not Wooden Stirring Spoon with Item ",
							41010U.ToString(),
							"; no armor registered."
						}));
						refused = Registry._refused;
					}
					else
					{
						Crumb.At("register: free grab");
						string text;
						Registry._noGrabPoints = Registry.NoGrabPoints(out text);
						Registry._freeGrab = 0;
						Registry._looks = 0;
						bool packOk = true;
						try
						{
							Pack.Load();
						}
						catch (Exception ex)
						{
							packOk = false;
							MelonLogger.Instance log = Mod.Log;
							string str = "armor pack failed to load; pieces keep the spoon look and nothing is drawn on players: ";
							Exception ex2 = ex;
							log.Error(str + ((ex2 != null) ? ex2.ToString() : null));
						}
						Registry._holder = new GameObject("ArmorRestored Templates");
						Registry._holder.SetActive(false);
						Object.DontDestroyOnLoad(Registry._holder);
						List<string> list = new List<string>();
						int num = 0;
						for (int i = 0; i < 24; i++)
						{
							string text2;
							try
							{
								text2 = Registry.Build(i, prefab, item, prefabMap, items, packOk);
							}
							catch (Exception ex3)
							{
								text2 = "threw " + ex3.Message;
							}
							if (text2 == null)
							{
								num++;
							}
							else
							{
								list.Add(Catalog.Id(i));
								Mod.Log.Error(Catalog.Id(i) + " refused: " + text2);
							}
						}
						Crumb.At("register: itemsByName reset");
						HashedGeneralValue<Item>.itemsByName = null;
						Registry._refused = string.Join(",", list.ToArray());
						Mod.Log.Msg(string.Concat(new string[]
						{
							num.ToString(),
							"/",
							24.ToString(),
							" armor pieces registered (numbers ",
							52830.ToString(),
							"-",
							52853.ToString(),
							") in ",
							((int)(DateTime.UtcNow - utcNow).TotalMilliseconds).ToString(),
							" ms: ",
							Registry._looks.ToString(),
							" with the armor look, ",
							Registry._freeGrab.ToString(),
							" with free grab like the server (empty grab point list from ",
							text ?? "nothing",
							")."
						}));
						Crumb.At("register: done " + num.ToString());
						refused = Registry._refused;
					}
				}
			}
			catch (Exception ex4)
			{
				Registry.Done = true;
				Registry._refused = "registration failed";
				MelonLogger.Instance log2 = Mod.Log;
				string str2 = "registration failed: ";
				Exception ex5 = ex4;
				log2.Error(str2 + ((ex5 != null) ? ex5.ToString() : null));
				refused = Registry._refused;
			}
			return refused;
		}

		// Token: 0x06000067 RID: 103 RVA: 0x00006AEC File Offset: 0x00004CEC
		private static string Build(int piece, NetworkPrefab source, Item sourceItem, Dictionary<uint, NetworkPrefab> map, Dictionary<uint, Item> items, bool packOk)
		{
			uint num = Catalog.Number(piece);
			if (map.ContainsKey(num))
			{
				return "prefab " + num.ToString() + " is already taken";
			}
			if (items.ContainsKey(num))
			{
				return "Item " + num.ToString() + " is already taken";
			}
			Crumb.First("Instantiate prefab");
			Object @object = Object.Instantiate(source.gameObject, Registry._holder.transform);
			GameObject gameObject = (@object == null) ? null : @object.TryCast<GameObject>();
			if (gameObject == null)
			{
				return "the template copy is not a GameObject";
			}
			gameObject.name = Catalog.Title(piece);
			Component component = gameObject.GetComponent(Il2CppType.Of<NetworkPrefab>());
			NetworkPrefab networkPrefab = (component == null) ? null : component.TryCast<NetworkPrefab>();
			if (networkPrefab == null)
			{
				Object.DestroyImmediate(gameObject);
				return "the copy has no NetworkPrefab";
			}
			Crumb.First("set NetworkPrefab.hash");
			networkPrefab.hash = (int)num;
			if (networkPrefab.Hash != num)
			{
				Object.DestroyImmediate(gameObject);
				return "the copy's hash did not take";
			}
			Crumb.First("Instantiate item");
			Object object2 = Object.Instantiate(sourceItem);
			Item item = (object2 == null) ? null : object2.TryCast<Item>();
			if (item == null)
			{
				Object.DestroyImmediate(gameObject);
				return "the item copy is not an Item";
			}
			item.name = Catalog.Title(piece);
			Crumb.First("set Item.hash and Prefab");
			item.hash = (int)num;
			item.Prefab = networkPrefab;
			foreach (Component component2 in gameObject.GetComponentsInChildren(Il2CppType.Of<Pickup>(), true))
			{
				Pickup pickup = (component2 == null) ? null : component2.TryCast<Pickup>();
				if (pickup != null && pickup.Item != null && pickup.Item.Pointer == sourceItem.Pointer)
				{
					pickup.Item = item;
				}
			}
			Component component3 = gameObject.GetComponent(Il2CppType.Of<Pickup>());
			Pickup pickup2 = (component3 == null) ? null : component3.TryCast<Pickup>();
			if (pickup2 == null)
			{
				Object.DestroyImmediate(gameObject);
				return "the copy has no Pickup";
			}
			Crumb.First("set Pickup.isTightGrab and grabPoints");
			pickup2.isTightGrab = false;
			pickup2.grabPoints = Registry._noGrabPoints;
			Il2CppReferenceArray<GrabPoint> grabPoints = pickup2.GrabPoints;
			if (!pickup2.IsTightGrab && grabPoints != null && grabPoints.Length == 0)
			{
				Registry._freeGrab++;
			}
			else
			{
				Mod.Log.Error(string.Concat(new string[]
				{
					Catalog.Id(piece),
					": free grab did not take (tight ",
					pickup2.IsTightGrab.ToString(),
					", points ",
					(grabPoints == null) ? "none" : grabPoints.Length.ToString(),
					")."
				}));
			}
			if (packOk)
			{
				string text = Registry.Look(piece, gameObject, pickup2);
				if (text == null)
				{
					Registry._looks++;
				}
				else
				{
					Mod.Log.Error(Catalog.Id(piece) + " keeps the spoon look: " + text);
				}
			}
			ArmorMaterials.Configure(piece, gameObject);
			Crumb.First("prefabMap.Add");
			map.Add(num, networkPrefab);
			NetworkPrefab prefab = PrefabManager.GetPrefab(num);
			if (prefab == null || prefab.Pointer != networkPrefab.Pointer)
			{
				return "the prefab map did not take it";
			}
			Crumb.First("items.Add");
			items.Add(num, item);
			Registry.Prefabs[piece] = networkPrefab;
			Registry.Items[piece] = item;
			return null;
		}

		// Token: 0x06000068 RID: 104 RVA: 0x00006E70 File Offset: 0x00005070
		private static string Look(int piece, GameObject copy, Pickup root)
		{
			Transform transform = copy.transform;
			Transform transform2 = transform.Find("Tight Grip");
			Transform transform3 = transform.Find("Mesh");
			Transform transform4 = transform.Find("Collision");
			if (transform2 == null || transform3 == null || transform4 == null)
			{
				return "the spoon has no Tight Grip, Mesh or Collision child";
			}
			PieceDef pieceDef = Pack.Pieces[piece];
			List<GameObject> list = Registry.Children(transform4);
			List<GameObject> list2 = Registry.Children(transform3);
			List<GameObject> list3 = new List<GameObject>();
			List<Collider> list4 = new List<Collider>();
			Il2CppReferenceArray<Collider> childrenColliders = null;
			bool flag = false;
			try
			{
				Crumb.First("look: colliders");
				for (int i = 0; i < pieceDef.Colliders.Count; i++)
				{
					ColliderDef colliderDef = pieceDef.Colliders[i];
					GameObject gameObject = new GameObject("Collider " + i.ToString());
					list3.Add(gameObject);
					gameObject.layer = transform4.gameObject.layer;
					gameObject.transform.SetParent(transform4, false);
					Pack.Place(gameObject.transform, colliderDef.Local);
					list4.Add(Registry.AddCollider(gameObject, colliderDef));
				}
				Crumb.First("look: meshes");
				foreach (LookPart lookPart in pieceDef.Look)
				{
					GameObject gameObject2 = new GameObject(Pack.Meshes[lookPart.Mesh].Name);
					list3.Add(gameObject2);
					gameObject2.layer = transform3.gameObject.layer;
					gameObject2.transform.SetParent(transform3, false);
					Pack.Place(gameObject2.transform, lookPart.Local);
					gameObject2.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>().sharedMesh = Pack.MeshOf(lookPart.Mesh);
					gameObject2.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>().sharedMaterial = Pack.MaterialOf(lookPart.Mat);
				}
				Crumb.First("look: other colliders");
				HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
				hashSet.Add(transform2.Pointer);
				foreach (GameObject gameObject3 in list)
				{
					hashSet.Add(gameObject3.transform.Pointer);
				}
				HashSet<IntPtr> hashSet2 = new HashSet<IntPtr>();
				foreach (Collider collider in list4)
				{
					hashSet2.Add(collider.Pointer);
				}
				foreach (Component component in copy.GetComponentsInChildren(Il2CppType.Of<Collider>(), true))
				{
					Collider collider2 = (component == null) ? null : component.TryCast<Collider>();
					if (!(collider2 == null) && !hashSet2.Contains(collider2.Pointer) && !Registry.Under(collider2.transform, hashSet))
					{
						list4.Add(collider2);
					}
				}
				Crumb.First("look: childrenColliders");
				Il2CppReferenceArray<Collider> il2CppReferenceArray = new Il2CppReferenceArray<Collider>(Array.CreateInstance(Il2CppType.Of<Collider>(), list4.Count).Pointer);
				for (int j = 0; j < list4.Count; j++)
				{
					il2CppReferenceArray[j] = list4[j];
				}
				childrenColliders = root.childrenColliders;
				root.childrenColliders = il2CppReferenceArray;
				flag = true;
				Crumb.First("look: Colliders set");
				HashSet<Collider> colliders = root.Colliders;
				if (colliders != null)
				{
					colliders.Clear();
					foreach (Collider collider3 in list4)
					{
						colliders.Add(collider3);
					}
				}
			}
			catch (Exception ex)
			{
				try
				{
					if (flag)
					{
						root.childrenColliders = childrenColliders;
					}
					foreach (GameObject gameObject4 in list3)
					{
						if (gameObject4 != null)
						{
							Object.DestroyImmediate(gameObject4);
						}
					}
				}
				catch (Exception)
				{
				}
				return ex.GetType().Name + ": " + ex.Message;
			}
			Crumb.First("look: spoon parts out");
			Object.DestroyImmediate(transform2.gameObject);
			foreach (GameObject gameObject5 in list)
			{
				if (gameObject5 != null)
				{
					Object.DestroyImmediate(gameObject5);
				}
			}
			Component component2 = transform3.GetComponent(Il2CppType.Of<LODGroup>());
			if (component2 != null)
			{
				Object.DestroyImmediate(component2);
			}
			foreach (GameObject gameObject6 in list2)
			{
				if (gameObject6 != null)
				{
					Object.DestroyImmediate(gameObject6);
				}
			}
			return null;
		}

		// Token: 0x06000069 RID: 105 RVA: 0x00007464 File Offset: 0x00005664
		private static bool Under(Transform t, HashSet<IntPtr> set)
		{
			int num = 0;
			while (t != null && num < 32)
			{
				if (set.Contains(t.Pointer))
				{
					return true;
				}
				num++;
				t = t.parent;
			}
			return false;
		}

		// Token: 0x0600006A RID: 106 RVA: 0x000074A0 File Offset: 0x000056A0
		private static List<GameObject> Children(Transform t)
		{
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < t.childCount; i++)
			{
				list.Add(t.GetChild(i).gameObject);
			}
			return list;
		}

		// Token: 0x0600006B RID: 107 RVA: 0x000074D8 File Offset: 0x000056D8
		private static Collider AddCollider(GameObject go, ColliderDef c)
		{
			Vector3 center = default(Vector3);
			center.x = c.CX;
			center.y = c.CY;
			center.z = c.CZ;
			if (c.Shape == 0)
			{
				BoxCollider boxCollider = go.AddComponent(Il2CppType.Of<BoxCollider>()).TryCast<BoxCollider>();
				Vector3 size = default(Vector3);
				size.x = c.SX;
				size.y = c.SY;
				size.z = c.SZ;
				boxCollider.center = center;
				boxCollider.size = size;
				return boxCollider;
			}
			if (c.Shape == 1)
			{
				CapsuleCollider capsuleCollider = go.AddComponent(Il2CppType.Of<CapsuleCollider>()).TryCast<CapsuleCollider>();
				capsuleCollider.center = center;
				capsuleCollider.radius = c.Radius;
				capsuleCollider.height = c.Height;
				capsuleCollider.direction = c.Direction;
				return capsuleCollider;
			}
			SphereCollider sphereCollider = go.AddComponent(Il2CppType.Of<SphereCollider>()).TryCast<SphereCollider>();
			sphereCollider.center = center;
			sphereCollider.radius = c.Radius;
			return sphereCollider;
		}

		// Token: 0x0600006C RID: 108 RVA: 0x000075D4 File Offset: 0x000057D4
		private static Il2CppReferenceArray<GrabPoint> NoGrabPoints(out string from)
		{
			Il2CppReferenceArray<Object> il2CppReferenceArray = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Pickup>());
			if (il2CppReferenceArray != null)
			{
				foreach (Object @object in il2CppReferenceArray)
				{
					Pickup pickup = (@object == null) ? null : @object.TryCast<Pickup>();
					if (!(pickup == null))
					{
						Il2CppReferenceArray<GrabPoint> grabPoints = pickup.grabPoints;
						if (grabPoints != null && grabPoints.Length == 0)
						{
							from = pickup.name;
							return grabPoints;
						}
					}
				}
			}
			from = "a new array";
			Crumb.At("register: new grab point list");
			return new Il2CppReferenceArray<GrabPoint>(Array.CreateInstance(Il2CppType.Of<GrabPoint>(), 0).Pointer);
		}

		// Token: 0x0600006D RID: 109 RVA: 0x0000768C File Offset: 0x0000588C
		internal static string Unregister()
		{
			Dictionary<uint, NetworkPrefab> prefabMap = PrefabManager.prefabMap;
			Dictionary<uint, Item> items = HashedGeneralValue<Item>.items;
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < 24; i++)
			{
				uint num3 = Catalog.Number(i);
				if (prefabMap != null && Registry.Prefabs[i] != null && prefabMap.ContainsKey(num3) && prefabMap[num3].Pointer == Registry.Prefabs[i].Pointer && prefabMap.Remove(num3))
				{
					num++;
				}
				if (items != null && Registry.Items[i] != null && items.ContainsKey(num3) && items[num3].Pointer == Registry.Items[i].Pointer && items.Remove(num3))
				{
					num2++;
				}
				if (Registry.Items[i] != null)
				{
					Object.Destroy(Registry.Items[i]);
				}
				Registry.Prefabs[i] = null;
				Registry.Items[i] = null;
			}
			HashedGeneralValue<Item>.itemsByName = null;
			if (Registry._holder != null)
			{
				Object.Destroy(Registry._holder);
			}
			Registry._holder = null;
			Registry.Done = false;
			Registry._refused = "not registered";
			Crumb.At("unregistered");
			return string.Concat(new string[]
			{
				"armor pieces removed: ",
				num.ToString(),
				" prefabs, ",
				num2.ToString(),
				" items."
			});
		}

		// Token: 0x04000085 RID: 133
		private const uint TemplatePrefab = 23950U;

		// Token: 0x04000086 RID: 134
		private const uint TemplateItem = 41010U;

		// Token: 0x04000087 RID: 135
		private const string TemplateName = "Wooden Stirring Spoon";

		// Token: 0x04000088 RID: 136
		internal static bool Done;

		// Token: 0x04000089 RID: 137
		private static readonly NetworkPrefab[] Prefabs = new NetworkPrefab[24];

		// Token: 0x0400008A RID: 138
		private static readonly Item[] Items = new Item[24];

		// Token: 0x0400008B RID: 139
		private static GameObject _holder;

		// Token: 0x0400008C RID: 140
		private static string _refused = "not registered";

		// Token: 0x0400008D RID: 141
		private static Il2CppReferenceArray<GrabPoint> _noGrabPoints;

		// Token: 0x0400008E RID: 142
		private static int _freeGrab;

		// Token: 0x0400008F RID: 143
		private static int _looks;
	}
}
