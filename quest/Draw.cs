using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppAlta.Customization;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestoredQuest
{
	// Token: 0x02000005 RID: 5
	internal static class Draw
	{
		// Token: 0x06000011 RID: 17 RVA: 0x000023F8 File Offset: 0x000005F8
		internal static void OnTable(Dictionary<int, int[]> rows)
		{
			Draw.Table.Clear();
			int num = 0;
			foreach (KeyValuePair<int, int[]> keyValuePair in rows)
			{
				Draw.Table[keyValuePair.Key] = keyValuePair.Value;
				int[] value = keyValuePair.Value;
				for (int i = 0; i < 8; i++)
				{
					if (value[i] >= 0)
					{
						num++;
					}
				}
			}
			Draw._dirty = true;
			if (Settings.Debug)
			{
				Mod.Log.Msg(string.Concat(new string[]
				{
					"armor table: ",
					rows.Count.ToString(),
					" player(s), ",
					num.ToString(),
					" piece(s) worn."
				}));
			}
		}

		// Token: 0x06000012 RID: 18 RVA: 0x000024E0 File Offset: 0x000006E0
		internal static int[] WornBy(Dressed d)
		{
			int[] result;
			if (d == null || !Draw.Table.TryGetValue(d.User, out result))
			{
				return null;
			}
			return result;
		}

		// Token: 0x06000013 RID: 19 RVA: 0x00002508 File Offset: 0x00000708
        // Do not build hundreds of renderers while the local avatar is arriving.
        // Native registration and the join handshake remain mandatory and immediate.
        private static IntPtr loadingRig;
        private static float visualReadyAt, nextReadyProbe;
        private static bool visualsReady, waitingLogged;
        internal static bool VisualsReady()
        {
            if(!Gate.IsOpen || !Registry.Done) return false;
            if(Time.unscaledTime<nextReadyProbe) return visualsReady;
            nextReadyProbe=Time.unscaledTime+.5f;
            var controller=PlayerController.Current;
            var player=controller==null ? null : controller.NetworkPlayer;
            var character=CharacterOf(player);
            var rig=character==null ? null : RigOf(character);
            if(character==null || !character.gameObject.activeInHierarchy || rig==null || !rig.gameObject.activeInHierarchy)
            {loadingRig=IntPtr.Zero;visualsReady=false;visualReadyAt=0f;return false;}
            if(loadingRig!=rig.Pointer)
            {
                loadingRig=rig.Pointer;visualsReady=false;visualReadyAt=Time.unscaledTime+10f;
                if(!waitingLogged) {waitingLogged=true;Mod.Log.Msg("Quest armor visuals: local avatar found; waiting 10 seconds before worn rendering.");}
            }
            if(!visualsReady && Time.unscaledTime>=visualReadyAt)
            {visualsReady=true;Mod.Log.Msg("Quest armor visuals: avatar settling delay complete; staged worn rendering enabled.");}
            return visualsReady;
        }

		internal static void Clear()
		{
            loadingRig=IntPtr.Zero;visualReadyAt=nextReadyProbe=0f;visualsReady=waitingLogged=false;
			foreach (Dressed dressed in Draw.Bodies.Values)
			{
                dressed.Coverage.Clear();
				for (int i = 0; i < dressed.Slots.Length; i++)
				{
					Draw.Remove(dressed, i);
				}
			}
			Draw.Bodies.Clear();
			WornMaterials.Clear();
			Draw.Table.Clear();
			Draw.Local = null;
			Draw._dirty = false;
			Draw._lastCensus = null;
		}

		// Token: 0x06000014 RID: 20 RVA: 0x00002598 File Offset: 0x00000798
		internal static void Tick()
		{
			if (!Gate.IsOpen || !Registry.Done || !Pack.Loaded)
			{
				return;
			}
            if(!VisualsReady()) return;
            WornMaterials.Tick();
			float unscaledTime = Time.unscaledTime;
			if (Settings.Debug && unscaledTime >= Draw._nextCensus)
			{
				Draw.Census(unscaledTime);
			}
			if (!Draw._dirty && unscaledTime < Draw._next)
			{
				return;
			}
			Draw._dirty = false;
			Draw._next = unscaledTime + 0.5f;
			Draw.Dress();
		}

		// Token: 0x06000015 RID: 21 RVA: 0x00002600 File Offset: 0x00000800
		private static void Dress()
		{
            int buildsRemaining=1; // Shared across players/dummies for this frame.
			List<IntPtr> list = new List<IntPtr>();
			foreach (KeyValuePair<IntPtr, Dressed> keyValuePair in Draw.Bodies)
			{
				if (keyValuePair.Value.Root == null)
				{
					list.Add(keyValuePair.Key);
				}
			}
			foreach (IntPtr key in list)
			{
				Draw.Bodies[key].Coverage.Clear();
                Draw.Bodies.Remove(key);
			}
			if (Draw.Local != null && Draw.Local.Root == null)
			{
				Draw.Local = null;
			}
			PlayerController playerController = PlayerController.Current;
			Player player = (playerController == null) ? null : playerController.NetworkPlayer;
			IntPtr intPtr = (player == null) ? IntPtr.Zero : player.Pointer;
			foreach (Object @object in Object.FindObjectsOfType(Il2CppType.Of<Player>()))
			{
				Player player2 = (@object == null) ? null : @object.TryCast<Player>();
                PlayerCharacter character = Draw.CharacterOf(player2);
				if (!(player2 == null) && player2.UserInfo != null && character != null)
				{
					int identifier = player2.UserInfo.Identifier;
					int[] array;
					if (!Draw.Table.TryGetValue(identifier, out array))
					{
						array = Catalog.Empty();
					}
					bool isLocal = player2.Pointer == intPtr;
                    bool dummy = false;
                    Transform transform = Draw.RigOf(character);
                    if (isLocal) transform = EquipmentMode.Resolve(transform, out dummy);
                    if (isLocal && transform == null)
                    {
                        Dressed stale;
                        if (Draw.Bodies.TryGetValue(character.transform.Pointer, out stale))
                        {
                            stale.Coverage.Clear();
                            for (int slot = 0; slot < 8; slot++) Draw.Remove(stale, slot);
                            Draw.Bodies.Remove(character.transform.Pointer);
                            if (Draw.Local == stale) Draw.Local = null;
                        }
                    }
					if (!(transform == null))
					{
						Transform transform2 = character.transform;
						Dressed dressed;
						if (Draw.Bodies.TryGetValue(transform2.Pointer, out dressed) && (dressed.Rig == null || dressed.Rig.Pointer != transform.Pointer))
						{
                            dressed.Coverage.Clear();
							for (int i = 0; i < 8; i++)
							{
								Draw.Remove(dressed, i);
							}
							if (Draw.Local == dressed)
							{
								Draw.Local = null;
							}
							dressed = null;
						}
						if (dressed == null)
						{
							dressed = new Dressed
							{
								Name = player2.UserInfo.Username,
								User = identifier,
								Root = dummy ? transform : transform2,
                                Dummy = dummy,
								Rig = transform
							};
							Draw.Wire(dressed);
							Draw.Bodies[transform2.Pointer] = dressed;
						}
                        // A newly opened equipment preview can exist before its
                        // baked skeleton. Never skin armor against placeholder joints.
                        if(dressed.BakedBindings==0)
                        {
                            if(Time.unscaledTime>=dressed.NextBindingRetry)
                            {dressed.NextBindingRetry=Time.unscaledTime+1f;Draw.Wire(dressed);}
                            if(dressed.BakedBindings==0) continue;
                        }
						bool flag = player2.Pointer == intPtr;
						if (flag)
						{
							Draw.Local = dressed;
						}
						bool flag2 = false;
						for (int j = 0; j < 8; j++)
						{
							int num = array[j];
                            if (flag && ArmorVisibility.Hidden(j)) num = -1;
							if (flag && !dressed.Dummy && j == 0 && !Settings.OwnHelmet)
							{
								num = -1;
							}
							uint material = array.Length >= 16 ? unchecked((uint)array[8 + j]) : 0U;
							if (dressed.Shown[j] != num || dressed.ShownMaterials[j] != material || dressed.ShownPalettes[j] != WornIngredientGroups.Key(array,j) || (num >= 0 && !Draw.HolderLive(dressed, j)))
							{
                                if(num>=0 && buildsRemaining==0) {Draw._dirty=true;continue;}
                                if(num>=0) buildsRemaining--;
								Draw.Remove(dressed, j);
								dressed.Shown[j] = -1;
								if (num >= 0)
								{
									Draw.Build(dressed, j, num, flag && !dressed.Dummy, material, array, j);
								}
								dressed.Shown[j] = num;
								dressed.ShownMaterials[j] = material;
                            dressed.ShownPalettes[j] = WornIngredientGroups.Key(array,j);
								flag2 = true;
							}
						}
                        dressed.Coverage.Update(dressed.Root, dressed.Shown);
						if (flag2)
						{
							Draw.Cost(dressed, flag);
						}
					}
				}
			}
		}

		// Token: 0x06000016 RID: 22 RVA: 0x00002954 File Offset: 0x00000B54
        private static PlayerCharacter CharacterOf(Player player)
        {
            if (player == null) return null;
            try
            {
                return player.PlayerCharacter;
            }
            catch (Il2CppInterop.Runtime.Il2CppException)
            {
                // Customization previews are Players too, but their controller is
                // not a PlayerCharacter. Skip this entry, not the entire census.
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

		private static Transform RigOf(PlayerCharacter character)
		{
			Transform result;
			try
			{
				PlayerCustomizationHandler customizationHandler = character.CustomizationHandler;
				result = ((customizationHandler == null || customizationHandler.IsDestroyed || customizationHandler.CharacterDummy == null) ? null : customizationHandler.RootBone);
			}
			catch (Exception)
			{
				result = null;
			}
			return result;
		}

		// Token: 0x06000017 RID: 23 RVA: 0x000029B0 File Offset: 0x00000BB0
		private static bool HolderLive(Dressed d, int s)
		{
			List<GameObject> list = d.Slots[s];
			return list != null && list.Count > 0 && list[0] != null;
		}

		// Token: 0x06000018 RID: 24 RVA: 0x000029E4 File Offset: 0x00000BE4
		private static void Cost(Dressed d, bool isLocal)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < 8; i++)
			{
				if (d.Shown[i] >= 0)
				{
					num++;
					num2 += ((d.Renderers[i] == null) ? 0 : d.Renderers[i].Count);
					num3 += d.Tris[i];
				}
			}
			string text = string.Concat(new string[]
			{
				d.Name,
				isLocal ? " (you)" : "",
				" shows ",
				num.ToString(),
				" armor piece(s): ",
				num2.ToString(),
				" renderers, ",
				num3.ToString(),
				" triangles."
			});
			if (text == d.Cost)
			{
				return;
			}
			d.Cost = text;
			Mod.Log.Msg(text);
		}

		// Token: 0x06000019 RID: 25 RVA: 0x00002ACC File Offset: 0x00000CCC
		private static void Wire(Dressed d)
		{
			Crumb.First("wire bones");
			d.Bones = new Dictionary<string, Transform>();
			d.Layer = -1;
			foreach (Component component in d.Rig.GetComponentsInChildren(Il2CppType.Of<Transform>(), true))
			{
				Transform transform = (component == null) ? null : component.TryCast<Transform>();
				if (!(transform == null))
				{
					string name = transform.name;
					if (name.EndsWith("_jnt") && !d.Bones.ContainsKey(name))
					{
						d.Bones[name] = transform;
					}
				}
			}
            // The baked body's renderer owns the skeleton actually used to draw
            // this avatar. RootBone's descendants can also contain dummy/reference
            // joints with the same names; those must not win name lookup.
            int bound = 0;
            foreach (Component component in d.Root.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(), true))
            {
                SkinnedMeshRenderer body = component == null ? null : component.TryCast<SkinnedMeshRenderer>();
                if (body == null || body.transform.parent == null || body.transform.parent.name != "Baked" || body.bones == null) continue;
                foreach (Transform bone in body.bones)
                {
                    if (bone == null) continue;
                    d.Bones[bone.name] = bone;
                    bound++;
                }
            }
            d.BakedBindings=bound;
            if(bound>0) Mod.Log.Msg("armor rig on " + d.Name + ": " + bound + " bone bindings from baked avatar renderers");
			foreach (Component component2 in d.Root.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(), true))
			{
				if (!(component2 == null) && !(component2.transform.parent == null) && !(component2.transform.parent.name != "Baked"))
				{
					d.Layer = component2.gameObject.layer;
					break;
				}
			}
			if (d.Layer < 0)
			{
				d.Layer = Math.Max(0, LayerMask.NameToLayer("Player"));
			}
		}

		// Token: 0x0600001A RID: 26 RVA: 0x00002C34 File Offset: 0x00000E34
		internal static Transform Bone(Dressed d, string name)
		{
			if (name == null || d.Bones == null)
			{
				return null;
			}
			Transform result;
			if (d.Bones.TryGetValue(name, out result))
			{
				return result;
			}
			string text = name.TrimEnd(new char[]
			{
				'0',
				'1',
				'2',
				'3',
				'4',
				'5',
				'6',
				'7',
				'8',
				'9'
			});
			if (text.EndsWith("_jnt") && d.Bones.TryGetValue(text, out result))
			{
				return result;
			}
			return null;
		}

		// Token: 0x0600001B RID: 27 RVA: 0x00002C9C File Offset: 0x00000E9C
		private static void Remove(Dressed d, int s)
		{
			if (d.Slots[s] != null)
			{
				foreach (GameObject gameObject in d.Slots[s])
				{
					if (gameObject != null)
					{
						Object.Destroy(gameObject);
					}
				}
			}
			d.Slots[s] = null;
			d.Renderers[s] = null;
			d.Skinned[s] = null;
			d.Tris[s] = 0;
		}

		// Token: 0x0600001C RID: 28 RVA: 0x00002D28 File Offset: 0x00000F28
		private static void Build(Dressed d, int s, int piece, bool isLocal, uint material, int[] palette, int slot)
		{
			DateTime utcNow = DateTime.UtcNow;
			List<GameObject> list = new List<GameObject>();
			List<Renderer> list2 = new List<Renderer>();
			List<Renderer> list3 = new List<Renderer>();
			d.Slots[s] = list;
			d.Renderers[s] = list2;
			d.Skinned[s] = list3;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			GameObject gameObject = new GameObject("ArmorRestored " + Catalog.Id(piece));
			gameObject.transform.SetParent(d.Root, false);
			list.Add(gameObject);
			foreach (PartDef partDef in Pack.Pieces[piece].Worn)
			{
				Mesh sharedMesh = Pack.MeshOf(partDef.Mesh);
				Material[] sectionMaterials;
                sharedMesh=ArmorCrafting.SplitWorn(sharedMesh,Pack.Meshes[partDef.Mesh].Name,piece,material,
                    new[] {Pack.MaterialOf(partDef.Mat)},palette,slot,out sectionMaterials);
				GameObject gameObject2 = new GameObject(Pack.Meshes[partDef.Mesh].Name);
				gameObject2.layer = d.Layer;
				if (!partDef.Skinned)
				{
					Transform transform = Draw.Bone(d, partDef.Bone);
					if (transform == null)
					{
						num3++;
						Object.Destroy(gameObject2);
						continue;
					}
					gameObject2.transform.SetParent(transform, false);
					Pack.Place(gameObject2.transform, partDef.Local);
					Crumb.First("rigid part");
					gameObject2.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>().sharedMesh = sharedMesh;
					MeshRenderer meshRenderer = gameObject2.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>();
					meshRenderer.sharedMaterials = new Il2CppReferenceArray<Material>(sectionMaterials);
					list.Add(gameObject2);
					list2.Add(meshRenderer);
					num++;
				}
				else
				{
					gameObject2.transform.SetParent(gameObject.transform, false);
					Transform transform2 = Draw.Bone(d, partDef.Root);
					Crumb.First("skinned part: bones array");
					Il2CppReferenceArray<Transform> il2CppReferenceArray = new Il2CppReferenceArray<Transform>(Il2CppSystem.Array.CreateInstance(Il2CppType.Of<Transform>(), partDef.Bones.Length).Pointer);
					for (int i = 0; i < partDef.Bones.Length; i++)
					{
						Transform transform3 = Draw.Bone(d, partDef.Bones[i]);
						if (transform3 == null)
						{
							transform3 = transform2;
							num3++;
						}
						il2CppReferenceArray[i] = transform3;
					}
					Crumb.First("skinned part: SkinnedMeshRenderer");
					SkinnedMeshRenderer skinnedMeshRenderer = gameObject2.AddComponent(Il2CppType.Of<SkinnedMeshRenderer>()).TryCast<SkinnedMeshRenderer>();
					skinnedMeshRenderer.bones = il2CppReferenceArray;
					skinnedMeshRenderer.rootBone = transform2;
					skinnedMeshRenderer.sharedMesh = sharedMesh;
					skinnedMeshRenderer.sharedMaterials = new Il2CppReferenceArray<Material>(sectionMaterials);
					skinnedMeshRenderer.updateWhenOffscreen = true;
					list2.Add(skinnedMeshRenderer);
					list3.Add(skinnedMeshRenderer);
					num2++;
				}
				num4 += Pack.Meshes[partDef.Mesh].Tris;
			}
			d.Tris[s] = num4;
			Crumb.First("piece drawn");
			if (Settings.Debug || num3 > 0)
			{
				Mod.Log.Msg(string.Concat(new string[]
				{
					"drew ",
					Catalog.Title(piece),
					" on ",
					d.Name,
					isLocal ? " (you)" : "",
					": ",
					num.ToString(),
					" rigid, ",
					num2.ToString(),
					" skinned, ",
					num3.ToString(),
					" bone(s) missing, ",
					num4.ToString(),
					" triangles, layer ",
					d.Layer.ToString(),
					", ",
					((int)(DateTime.UtcNow - utcNow).TotalMilliseconds).ToString(),
					" ms."
				}));
			}
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00003110 File Offset: 0x00001310
		private static void Census(float now)
		{
			Draw._nextCensus = now + 5f;
			int num = 0;
			string text = "";
			foreach (Object @object in Object.FindObjectsOfType(Il2CppType.Of<Pickup>()))
			{
				Pickup pickup = (@object == null) ? null : @object.TryCast<Pickup>();
				if (!(pickup == null) && !(pickup.Item == null) && Registry.IsOurs(pickup.Item.Hash))
				{
					num++;
					if (text.Length == 0)
					{
						text = pickup.Item.name + " at " + pickup.transform.position.ToString();
					}
				}
			}
			string text2 = num.ToString() + " armor pickup(s) loaded" + ((text.Length > 0) ? ("; first " + text) : "");
			if (text2 == Draw._lastCensus)
			{
				return;
			}
			Draw._lastCensus = text2;
			Mod.Log.Msg("census: " + text2);
		}

		// Token: 0x0400001A RID: 26
		private static readonly Dictionary<int, int[]> Table = new Dictionary<int, int[]>();

		// Token: 0x0400001B RID: 27
		private static readonly Dictionary<IntPtr, Dressed> Bodies = new Dictionary<IntPtr, Dressed>();

		// Token: 0x0400001C RID: 28
		private static bool _dirty;

		// Token: 0x0400001D RID: 29
		private static float _next;

		// Token: 0x0400001E RID: 30
		private static float _nextCensus;

		// Token: 0x0400001F RID: 31
		private static string _lastCensus;

		// Token: 0x04000020 RID: 32
		internal static Dressed Local;
	}
}
