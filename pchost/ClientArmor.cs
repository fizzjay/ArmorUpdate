using System;
using System.Collections.Generic;
using System.Reflection;
using Alta.Customization;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestored
{
	// Token: 0x02000007 RID: 7
	internal static class ClientArmor
	{
        private static readonly EquipmentReleases Releases = new EquipmentReleases();
        private static Transform interactionRig;
		// Token: 0x0600001C RID: 28 RVA: 0x00002F50 File Offset: 0x00001150
		internal static void OnTable(Dictionary<int, int[]> rows)
		{
			ClientArmor.Table.Clear();
			foreach (KeyValuePair<int, int[]> keyValuePair in rows)
			{
				ClientArmor.Table[keyValuePair.Key] = keyValuePair.Value;
			}
			ClientArmor._tableDirty = true;
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00002FC0 File Offset: 0x000011C0
		internal static void OnTook(uint id, int hand)
		{
			ClientArmor._tookId = id;
			ClientArmor._tookHand = hand;
			ClientArmor._tookUntil = Time.unscaledTime + 3f;
			ClientArmor.Sound(false);
		}

		// Token: 0x0600001E RID: 30 RVA: 0x00002FE4 File Offset: 0x000011E4
		internal static void Clear()
		{
            ArmorVisibility.ClearButtons();
			foreach (ClientArmor.Dressed dressed in ClientArmor.Bodies.Values)
			{
				dressed.Coverage.Clear();
                foreach (GameObject gameObject in dressed.Slots)
				{
					if (gameObject != null)
					{
						Object.Destroy(gameObject);
					}
				}
			}
			ClientArmor.Bodies.Clear();
			WornMaterials.Clear();
			ClientArmor.Table.Clear();
            Releases.Clear();
            interactionRig = null;
			ClientArmor.Held[0] = (ClientArmor.Held[1] = null);
			ClientArmor.Closed[0] = (ClientArmor.Closed[1] = false);
			ClientArmor.PendingSlot[0] = (ClientArmor.PendingSlot[1] = -1);
			ClientArmor.Debugged[0] = (ClientArmor.Debugged[1] = null);
			ClientArmor._tookId = 0U;
			ClientArmor._tableDirty = false;
			ClientArmor._lastCensus = null;
		}

		// Token: 0x0600001F RID: 31 RVA: 0x000030D8 File Offset: 0x000012D8
		internal static void Tick()
		{
			if (!Gate.IsOpen || !Prefabs.Done)
			{
				return;
			}
            WornMaterials.Tick();
			if (Settings.Debug && Time.unscaledTime >= ClientArmor._nextCensus)
			{
				ClientArmor.Census();
			}
			if (ClientArmor._tableDirty || Time.unscaledTime >= ClientArmor._nextDress)
			{
				ClientArmor._tableDirty = false;
				ClientArmor._nextDress = Time.unscaledTime + 0.5f;
				ClientArmor.Dress();
			}
			ClientArmor.Hands();
			if (ClientArmor._tookId != 0U)
			{
				ClientArmor.GrabTaken();
			}
		}

		// Token: 0x06000020 RID: 32 RVA: 0x0000314C File Offset: 0x0000134C
		private static void Census()
		{
			ClientArmor._nextCensus = Time.unscaledTime + 3f;
			int num = 0;
			int num2 = 0;
			string text = "";
			foreach (Pickup pickup in Object.FindObjectsOfType<Pickup>())
			{
				int num3 = ClientArmor.PieceOf(pickup);
				if (num3 >= 0)
				{
					num++;
					foreach (Renderer renderer in pickup.GetComponentsInChildren<Renderer>(true))
					{
						if (renderer.enabled && renderer.gameObject.activeInHierarchy)
						{
							num2++;
							break;
						}
					}
					if (text.Length == 0)
					{
						text = string.Concat(new object[]
						{
							Catalog.Id(num3),
							" ",
							pickup.Entity.Identifier,
							" at ",
							pickup.transform.position.ToString("F2"),
							" active ",
							pickup.gameObject.activeInHierarchy
						});
					}
				}
			}
			string text2 = string.Concat(new object[]
			{
				num,
				" armor pickup(s), ",
				num2,
				" with a renderer on",
				(text.Length > 0) ? ("; first " + text) : ""
			});
			if (text2 == ClientArmor._lastCensus)
			{
				return;
			}
			ClientArmor._lastCensus = text2;
			Mod.Log.Msg("census: " + text2);
		}

		// Token: 0x06000021 RID: 33 RVA: 0x000032F4 File Offset: 0x000014F4
		private static void Dress()
		{
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, ClientArmor.Dressed> keyValuePair in ClientArmor.Bodies)
			{
				if (keyValuePair.Value.Root == null)
				{
					list.Add(keyValuePair.Key);
				}
			}
			foreach (int key in list)
			{
				ClientArmor.Bodies[key].Coverage.Clear();
                ClientArmor.Bodies.Remove(key);
			}
			Player objB = (PlayerController.Current != null) ? PlayerController.Current.NetworkPlayer : null;
			foreach (IPlayer player in Player.AllPlayers)
			{
				Player player2 = player as Player;
				if (!(player2 == null) && player2.UserInfo != null && !(player2.PlayerCharacter == null))
				{
					int[] array;
					if (!ClientArmor.Table.TryGetValue(player2.UserInfo.Identifier, out array))
					{
						array = ServerArmor.Empty();
					}
					bool isLocal = object.ReferenceEquals(player2, objB);
                    bool dummy = false;
                    Transform transform = ClientArmor.RigOf(player2);
                    if (isLocal) transform = EquipmentMode.Resolve(transform, out dummy);
                    if (isLocal && transform == null)
                    {
                        ClientArmor.Dressed stale;
                        int key = player2.PlayerCharacter.GetInstanceID();
                        if (ClientArmor.Bodies.TryGetValue(key, out stale))
                        {
                            stale.Coverage.Clear();
                            foreach (GameObject holder in stale.Slots)
                                if (holder != null) Object.Destroy(holder);
                            ClientArmor.Bodies.Remove(key);
                        }
                    }
					if (!(transform == null))
					{
						int instanceID = player2.PlayerCharacter.GetInstanceID();
						ClientArmor.Dressed dressed;
						if (ClientArmor.Bodies.TryGetValue(instanceID, out dressed) && dressed.Rig != transform)
						{
                            dressed.Coverage.Clear();
							foreach (GameObject gameObject in dressed.Slots)
							{
								if (gameObject != null)
								{
									Object.Destroy(gameObject);
								}
							}
							dressed = null;
						}
						if (dressed == null)
						{
							dressed = new ClientArmor.Dressed
							{
								Player = player2,
								Root = dummy ? transform : player2.PlayerCharacter.transform,
                                Dummy = dummy,
								Rig = transform,
								Shown = ServerArmor.Empty()
							};
							dressed.Bones = Body.Wire(transform, false);
                            // Prefer the visible avatar's renderer bindings over
                            // similarly named dummy/reference joints.
                            int bound = 0;
                            foreach (SkinnedMeshRenderer body in dressed.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            {
                                if (body.transform.parent == null || body.transform.parent.name != "Baked" || body.bones == null) continue;
                                foreach (Transform bone in body.bones)
                                {
                                    if (bone == null) continue;
                                    dressed.Bones.Bones[bone.name] = bone;
                                    bound++;
                                }
                            }
                            Mod.Log.Msg("armor rig on " + Players.Name(player2) + ": " + bound + " bone bindings from baked avatar renderers");
							ClientArmor.Bodies[instanceID] = dressed;
						}
						bool flag = object.ReferenceEquals(player2, objB);
						for (int j = 0; j < 8; j++)
						{
							int num = array[j];
                            if (flag && ArmorVisibility.Hidden(j)) num = -1;
							if (flag && !dressed.Dummy && j == 0 && !Settings.OwnHelmet)
							{
								num = -1;
							}
							uint material = array.Length >= 16 ? unchecked((uint)array[8 + j]) : 0U;
							if (dressed.Shown[j] != num || dressed.ShownMaterials[j] != material || (num >= 0 && !(dressed.Slots[j] != null)))
							{
								if (dressed.Slots[j] != null)
								{
									Object.Destroy(dressed.Slots[j]);
								}
								dressed.Slots[j] = ((num >= 0) ? ClientArmor.Build(dressed, num, material) : null);
								dressed.Shown[j] = num;
								dressed.ShownMaterials[j] = material;
								if (Settings.Debug && flag && num >= 0)
								{
									Mod.Log.Msg(Catalog.Title(num) + " sits at " + ClientArmor.FromHead(ClientArmor.SlotPoint(dressed, j)).ToString("F2") + " from the head (right, up, forward)");
								}
							}
						}
                        dressed.Coverage.Update(dressed.Root, dressed.Shown);
					}
				}
			}
		}

		// Token: 0x06000022 RID: 34 RVA: 0x00003650 File Offset: 0x00001850
		private static Transform RigOf(Player p)
		{
			Transform result;
			try
			{
				PlayerCustomizationHandler customizationHandler = p.PlayerCharacter.CustomizationHandler;
				result = ((customizationHandler == null || customizationHandler.IsDestroyed || customizationHandler.CharacterDummy == null) ? null : customizationHandler.RootBone);
			}
			catch (Exception)
			{
				result = null;
			}
			return result;
		}

		// Token: 0x06000023 RID: 35 RVA: 0x000036AC File Offset: 0x000018AC
		private static GameObject Build(ClientArmor.Dressed d, int piece, uint material)
		{
			PieceDef pieceDef = Pack.Pieces[piece];
			GameObject gameObject = new GameObject("ArmorRestored " + Catalog.Id(piece));
			gameObject.transform.SetParent(d.Root, false);
			int layer = ClientArmor.BodyLayer(d.Root);
			int num = 0;
			foreach (WornPart wornPart in pieceDef.Worn)
			{
				MeshDef meshDef = Pack.Meshes[wornPart.Mesh];
				GameObject gameObject2 = new GameObject(meshDef.Name);
				gameObject2.layer = layer;
				if (!wornPart.Skinned)
				{
					Transform transform = ClientArmor.Bone(d.Bones, wornPart.Bone);
					if (transform == null)
					{
						num++;
						Object.Destroy(gameObject2);
					}
					else
					{
						gameObject2.transform.SetParent(transform, false);
						Prefabs.Place(gameObject2.transform, wornPart.Local);
						gameObject2.AddComponent<MeshFilter>().sharedMesh = meshDef.Get();
						gameObject2.AddComponent<MeshRenderer>().sharedMaterials = WornMaterials.Apply(piece, material, Pack.Mats(wornPart.Mats), meshDef.Name);
						gameObject2.AddComponent<ClientArmor.Owner>().Holder = gameObject;
					}
				}
				else
				{
					gameObject2.transform.SetParent(gameObject.transform, false);
					SkinnedMeshRenderer skinnedMeshRenderer = gameObject2.AddComponent<SkinnedMeshRenderer>();
					Transform transform2 = ClientArmor.Bone(d.Bones, wornPart.Root);
					Transform[] array = new Transform[wornPart.Bones.Length];
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = ClientArmor.Bone(d.Bones, wornPart.Bones[i]);
						if (array[i] == null)
						{
							array[i] = transform2;
							num++;
						}
					}
					skinnedMeshRenderer.bones = array;
					skinnedMeshRenderer.rootBone = transform2;
					skinnedMeshRenderer.sharedMesh = meshDef.Get();
					skinnedMeshRenderer.sharedMaterials = WornMaterials.Apply(piece, material, Pack.Mats(wornPart.Mats), meshDef.Name);
					skinnedMeshRenderer.updateWhenOffscreen = true;
				}
			}
			if (num > 0)
			{
				Mod.Log.Warning(string.Concat(new object[]
				{
					Catalog.Id(piece),
					" on ",
					Players.Name(d.Player),
					": ",
					num,
					" bone(s) not found"
				}));
			}
			return gameObject;
		}

		// Token: 0x06000024 RID: 36 RVA: 0x0000392C File Offset: 0x00001B2C
		private static Transform Bone(Wiring w, string name)
		{
			if (name == null)
			{
				return null;
			}
			Transform result;
			if (w.Bones.TryGetValue(name, out result))
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
			if (text.EndsWith("_jnt") && w.Bones.TryGetValue(text, out result))
			{
				return result;
			}
			return null;
		}

		// Token: 0x06000025 RID: 37 RVA: 0x000039B4 File Offset: 0x00001BB4
		private static int BodyLayer(Transform root)
		{
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				if (skinnedMeshRenderer.transform.parent != null && skinnedMeshRenderer.transform.parent.name == "Baked")
				{
					return skinnedMeshRenderer.gameObject.layer;
				}
			}
			if (LayerMask.NameToLayer("Player") < 0)
			{
				return 0;
			}
			return LayerMask.NameToLayer("Player");
		}

		// Token: 0x06000026 RID: 38 RVA: 0x00003A34 File Offset: 0x00001C34
		private static ClientArmor.Dressed LocalBody()
		{
			PlayerController playerController = PlayerController.Current;
			Player player = (playerController != null) ? playerController.NetworkPlayer : null;
			if (player == null || player.PlayerCharacter == null)
			{
				return null;
			}
			ClientArmor.Dressed result;
			if (!ClientArmor.Bodies.TryGetValue(player.PlayerCharacter.GetInstanceID(), out result))
			{
				return null;
			}
			return result;
		}

		// Token: 0x06000027 RID: 39 RVA: 0x00003A90 File Offset: 0x00001C90
		private static Interactor HandInteractor(int hand)
		{
			PlayerController playerController = PlayerController.Current;
			if (playerController == null)
			{
				return null;
			}
			Controller controller = (hand == 0) ? playerController.LeftController : playerController.RightController;
			if (!(controller != null))
			{
				return null;
			}
			return controller.Interactor;
		}

		// Token: 0x06000028 RID: 40 RVA: 0x00003AD1 File Offset: 0x00001CD1
		private static float Reach(int slot)
		{
			if (slot != 1 && slot < 6)
			{
				return 0.3f;
			}
			return 0.4f;
		}

		// Token: 0x06000029 RID: 41 RVA: 0x00003AE8 File Offset: 0x00001CE8
		private static void Hands()
		{
			ClientArmor.Dressed dressed = ClientArmor.LocalBody();
            Transform nextRig = dressed == null ? null : dressed.Rig;
            if (!EquipmentMode.Same(interactionRig, nextRig))
            {
                Releases.Clear();
                ClientArmor.Held[0] = ClientArmor.Held[1] = null;
                ClientArmor.PendingSlot[0] = ClientArmor.PendingSlot[1] = -1;
                ClientArmor.Closed[0] = ClientArmor.Closed[1] = false;
                interactionRig = nextRig;
            }
            bool modeDummy;
            Transform modeRig = EquipmentMode.Resolve(dressed == null ? null : dressed.Rig, out modeDummy);
            // Armor changes are only available through the native equipment dummy.
            if (dressed == null || !modeDummy || !dressed.Dummy || !EquipmentMode.Same(modeRig, dressed.Rig) || modeDummy != dressed.Dummy
                || !dressed.Rig.gameObject.activeInHierarchy)
            {
                ArmorVisibility.ClearButtons();
                Releases.Clear();
                ClientArmor.Held[0] = ClientArmor.Held[1] = null;
                ClientArmor.PendingSlot[0] = ClientArmor.PendingSlot[1] = -1;
                ClientArmor.Closed[0] = ClientArmor.Closed[1] = false;
                return;
            }
			ArmorVisibility.Show(dressed.Rig, delegate(int slot) { return Body.Anchor(dressed.Bones, slot); });
            int[] array = null;
			if (dressed != null && dressed.Player.UserInfo != null)
			{
				ClientArmor.Table.TryGetValue(dressed.Player.UserInfo.Identifier, out array);
			}
			for (int i = 0; i < 2; i++)
			{
				Interactor interactor = ClientArmor.HandInteractor(i);
				if (!(interactor == null))
				{
					Pickup pickup = interactor.IsInteracting ? (interactor.InteractingWith as Pickup) : null;
					if (pickup != null && ClientArmor.PieceOf(pickup) < 0)
					{
						pickup = null;
					}
					Pickup pickup2 = ClientArmor.Held[i];
					if (Settings.Debug && pickup != null && !object.ReferenceEquals(pickup2, pickup))
					{
						Mod.Log.Msg(string.Concat(new string[]
						{
							"holding ",
							Catalog.Title(ClientArmor.PieceOf(pickup)),
							" in the ",
							ClientArmor.HandName(i),
							" hand"
						}));
					}
					if (Settings.Debug && interactor.IsInteracting && pickup == null && interactor.InteractingWith != null && ClientArmor.Debugged[i] != interactor.InteractingWith)
					{
						Mod.Log.Msg(string.Concat(new string[]
						{
							ClientArmor.HandName(i),
							" hand holds ",
							interactor.InteractingWith.GetType().Name,
							" ",
							interactor.InteractingWith.name
						}));
					}
					ClientArmor.Debugged[i] = (interactor.IsInteracting ? interactor.InteractingWith : null);
					if (pickup2 != null && !object.ReferenceEquals(pickup2, pickup) && dressed != null)
					{
						Releases.Released(pickup2, dressed.Rig);
					}
					ClientArmor.Held[i] = pickup;
                    Releases.Holding(pickup);
					bool flag = false;
					try
					{
						flag = (interactor.Controller != null && interactor.Controller.PlayerInput != null && interactor.Controller.PlayerInput.RawInput.GrabAxis > 0.8f);
					}
					catch (Exception)
					{
					}
					if (ArmorVisibility.Hand(i, interactor.transform.position, flag, !interactor.IsInteracting))
                    {
                        ClientArmor.PendingSlot[i] = -1;
                        ClientArmor.Closed[i] = flag;
                        continue;
                    }
                    bool flag2 = flag && !ClientArmor.Closed[i];
					ClientArmor.Closed[i] = flag;
					if (!flag || interactor.IsInteracting)
					{
						ClientArmor.PendingSlot[i] = -1;
					}
					else if (array != null && dressed != null && Time.unscaledTime >= ClientArmor._cooldown)
					{
						if (flag2 || ClientArmor.PendingSlot[i] < 0)
						{
							ClientArmor.PendingSlot[i] = ClientArmor.TouchedSlot(dressed, array, interactor.transform.position, i);
							ClientArmor.PendingAt[i] = Time.unscaledTime;
							if (Settings.Debug)
							{
								Mod.Log.Msg(string.Concat(new string[]
								{
									ClientArmor.HandName(i),
									" hand closed empty ",
									(ClientArmor.PendingSlot[i] >= 0) ? ("on " + Catalog.Title(array[ClientArmor.PendingSlot[i]])) : "on no worn piece",
									" (",
									ClientArmor.Distances(dressed, array, interactor.transform.position),
									")"
								}));
							}
						}
						else
						{
							int num = ClientArmor.PendingSlot[i];
							if (num >= 0 && Time.unscaledTime - ClientArmor.PendingAt[i] >= 0.35f)
							{
								ClientArmor.PendingSlot[i] = -1;
								if (array[num] >= 0 && ClientArmor.TouchedSlot(dressed, array, interactor.transform.position, i) == num)
								{
									ClientArmor._cooldown = Time.unscaledTime + 1f;
									Net.SendUnequip(num, i);
									Mod.Log.Msg(string.Concat(new string[]
									{
										"take off ",
										Catalog.Title(array[num]),
										" with the ",
										ClientArmor.HandName(i),
										" hand"
									}));
								}
							}
						}
					}
				}
			}
            Transform target = dressed == null ? null : dressed.Rig;
            bool dummy;
            Transform live = EquipmentMode.Resolve(target, out dummy);
            Releases.Tick(EquipmentMode.Same(target, live) ? target : null,
                delegate(Pickup item) { if (dressed != null) ClientArmor.LetGo(dressed, item); });
		}

		// Token: 0x0600002A RID: 42 RVA: 0x00003EE4 File Offset: 0x000020E4
		private static string HandName(int hand)
		{
			if (hand != 0)
			{
				return "right";
			}
			return "left";
		}

		// Token: 0x0600002B RID: 43 RVA: 0x00003EF4 File Offset: 0x000020F4
		private static string Distances(ClientArmor.Dressed me, int[] worn, Vector3 hand)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < 8; i++)
			{
				if (worn[i] >= 0)
				{
					float num = Vector3.Distance(hand, ClientArmor.SlotPoint(me, i));
					list.Add(Catalog.Slots[i].Id + " " + num.ToString("0.00"));
				}
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray()) + " m";
			}
			return "nothing worn";
		}

		// Token: 0x0600002C RID: 44 RVA: 0x00003F78 File Offset: 0x00002178
		private static Vector3 FromHead(Vector3 p)
		{
			PlayerController playerController = PlayerController.Current;
			if (playerController == null || playerController.Head == null)
			{
				return p;
			}
			return Quaternion.Inverse(Quaternion.Euler(0f, playerController.Head.eulerAngles.y, 0f)) * (p - playerController.Head.position);
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00003FE0 File Offset: 0x000021E0
		private static Vector3 SlotPoint(ClientArmor.Dressed me, int s)
		{
            // Grab the anatomical slot, not a potentially offset renderer bound.
			return Body.Anchor(me.Bones, s);
		}

		// Token: 0x0600002E RID: 46 RVA: 0x00004030 File Offset: 0x00002230
		private static int PieceOf(Pickup p)
		{
			if (p == null || p.Entity == null || p.Entity.Prefab == null)
			{
				return -1;
			}
			uint hash = p.Entity.Prefab.Hash;
			if (!Prefabs.IsArmor(hash))
			{
				return -1;
			}
			return Catalog.PieceOf(hash);
		}

		// Token: 0x0600002F RID: 47 RVA: 0x0000408C File Offset: 0x0000228C
		private static void LetGo(ClientArmor.Dressed me, Pickup was)
		{
            bool dummy;
            Transform live = EquipmentMode.Resolve(me == null ? null : me.Rig, out dummy);
            if (me == null || !me.Dummy || !dummy || !EquipmentMode.Same(live, me.Rig)) return;
            if (ArmorStand.NearSocket(was)) return;
            if (was == null || was.Dock != null || was.IsInteractedWith || was.Interactors.Count != 0) return;
			if (was == null || was.Entity == null)
			{
				return;
			}
			int num = ClientArmor.PieceOf(was);
			if (num < 0)
			{
				return;
			}
			int slot = num % 8;
			int[] worn;
            if (me.Dummy && (!ClientArmor.Table.TryGetValue(me.Player.UserInfo.Identifier, out worn) || worn[slot] >= 0)) return;
            float num2 = Vector3.Distance(me.Dummy ? was.transform.position : ClientArmor.Center(was.transform), Body.Anchor(me.Bones, slot));
			if (num2 > (me.Dummy ? 0.24f : ClientArmor.Reach(slot)))
			{
				if (Settings.Debug)
				{
					Mod.Log.Msg(string.Concat(new string[]
					{
						"let go of ",
						Catalog.Title(num),
						" ",
						num2.ToString("0.00"),
						" m from its place (needs ",
						ClientArmor.Reach(slot).ToString("0.0"),
						")"
					}));
				}
				return;
			}
			Net.SendEquip(was.Entity.Identifier);
			ClientArmor.Sound(true);
			Mod.Log.Msg(string.Concat(new string[]
			{
				"put on ",
				Catalog.Title(num),
				" (",
				num2.ToString("0.00"),
				" m from its place)"
			}));
		}

		// Token: 0x06000030 RID: 48 RVA: 0x000041BC File Offset: 0x000023BC
		private static Vector3 Center(Transform t)
		{
			Renderer[] componentsInChildren = t.GetComponentsInChildren<Renderer>();
			if (componentsInChildren.Length == 0)
			{
				return t.position;
			}
			Bounds bounds = componentsInChildren[0].bounds;
			for (int i = 1; i < componentsInChildren.Length; i++)
			{
				bounds.Encapsulate(componentsInChildren[i].bounds);
			}
			return bounds.center;
		}

		// Token: 0x06000031 RID: 49 RVA: 0x0000420C File Offset: 0x0000240C
		private static int TouchedSlot(ClientArmor.Dressed me, int[] worn, Vector3 hand, int handIndex)
		{
			int result = -1;
			float num = 0.2f;
			for (int i = 0; i < 8; i++)
			{
				if (worn[i] >= 0)
				{
					int removedBy = Catalog.Slots[i].RemovedBy;
					if ((removedBy != 1 || handIndex == 0) && (removedBy != 2 || handIndex == 1))
					{
						float num2 = Vector3.Distance(hand, ClientArmor.SlotPoint(me, i));
						if (num2 < num)
						{
							num = num2;
							result = i;
						}
					}
				}
			}
			return result;
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00004269 File Offset: 0x00002469
		private static bool NoRenderers(GameObject g)
		{
			return g.GetComponentsInChildren<Renderer>().Length == 0;
		}

		// Token: 0x06000033 RID: 51 RVA: 0x00004278 File Offset: 0x00002478
		private static void GrabTaken()
		{
			if (Time.unscaledTime > ClientArmor._tookUntil)
			{
				ClientArmor._tookId = 0U;
				return;
			}
			NetworkEntity networkEntity = (NetworkSceneManager.Current != null) ? NetworkSceneManager.Current.GetEntity(ClientArmor._tookId) : null;
			if (networkEntity == null)
			{
				return;
			}
			Pickup component = networkEntity.GetComponent<Pickup>();
			Interactor interactor = ClientArmor.HandInteractor(ClientArmor._tookHand);
			ClientArmor._tookId = 0U;
			if (component == null || interactor == null)
			{
				return;
			}
			try
			{
				interactor.SwapInteract(component, true);
			}
			catch (Exception ex)
			{
				Mod.Log.Warning("could not put the piece in the hand: " + ex.Message);
			}
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00004320 File Offset: 0x00002520
		private static void Sound(bool on)
		{
			try
			{
				if (!ClientArmor._soundsLooked)
				{
					ClientArmor._soundsLooked = true;
					FieldInfo field = typeof(SoundEffectSet).GetField("soundEffectType", BindingFlags.Instance | BindingFlags.NonPublic);
					foreach (SoundEffectSet soundEffectSet in Resources.FindObjectsOfTypeAll<SoundEffectSet>())
					{
						string a = (field != null) ? Convert.ToString(field.GetValue(soundEffectSet)) : soundEffectSet.name;
						if (a == "EquipmentApplied")
						{
							ClientArmor._soundOn = soundEffectSet;
						}
						if (a == "EquipmentTakenOff")
						{
							ClientArmor._soundOff = soundEffectSet;
						}
					}
					Mod.Log.Msg("equipment sounds: " + ((ClientArmor._soundOn != null) ? "on" : "-") + "/" + ((ClientArmor._soundOff != null) ? "off" : "-"));
				}
				SoundEffectSet soundEffectSet2 = (on ? ClientArmor._soundOn : ClientArmor._soundOff) as SoundEffectSet;
				PlayerController playerController = PlayerController.Current;
				if (!(soundEffectSet2 == null) && !(playerController == null) && !(SingletonBehaviour<AudioManager>.Instance == null))
				{
					GameObject gameObject = (playerController.Head != null) ? playerController.Head.gameObject : playerController.gameObject;
					SingletonBehaviour<AudioManager>.Instance.PlaySoundEffectOnObject(soundEffectSet2, gameObject, gameObject, new ISoundProperty[0]);
				}
			}
			catch (Exception ex)
			{
				Mod.Log.Warning("sound: " + ex.Message);
			}
		}

		// Token: 0x04000020 RID: 32
		private const float HoldToRemove = 0.35f;

		// Token: 0x04000021 RID: 33
		private static readonly Dictionary<int, int[]> Table = new Dictionary<int, int[]>();

		// Token: 0x04000022 RID: 34
		private static readonly Dictionary<int, ClientArmor.Dressed> Bodies = new Dictionary<int, ClientArmor.Dressed>();

		// Token: 0x04000023 RID: 35
		private static readonly Pickup[] Held = new Pickup[2];

		// Token: 0x04000024 RID: 36
		private static readonly bool[] Closed = new bool[2];

		// Token: 0x04000025 RID: 37
		private static float _nextDress;

		// Token: 0x04000026 RID: 38
		private static float _cooldown;

		// Token: 0x04000027 RID: 39
		private static float _tookUntil;

		// Token: 0x04000028 RID: 40
		private static uint _tookId;

		// Token: 0x04000029 RID: 41
		private static int _tookHand;

		// Token: 0x0400002A RID: 42
		private static bool _tableDirty;

		// Token: 0x0400002B RID: 43
		private static object _soundOn;

		// Token: 0x0400002C RID: 44
		private static object _soundOff;

		// Token: 0x0400002D RID: 45
		private static bool _soundsLooked;

		// Token: 0x0400002E RID: 46
		private static float _nextCensus;

		// Token: 0x0400002F RID: 47
		private static string _lastCensus;

		// Token: 0x04000030 RID: 48
		private static readonly int[] PendingSlot = new int[]
		{
			-1,
			-1
		};

		// Token: 0x04000031 RID: 49
		private static readonly float[] PendingAt = new float[2];

		// Token: 0x04000032 RID: 50
		private static readonly Interactable[] Debugged = new Interactable[2];

		// Token: 0x02000008 RID: 8
		private sealed class Dressed
		{
			// Token: 0x04000033 RID: 51
			public Player Player;

			// Token: 0x04000034 RID: 52
			public Transform Root;

			// Token: 0x04000035 RID: 53
			public Transform Rig;
            public bool Dummy;

			// Token: 0x04000036 RID: 54
			public Wiring Bones;

			// Token: 0x04000037 RID: 55
			public int[] Shown;
			public uint[] ShownMaterials = new uint[8];
            public CosmeticCoverage Coverage = new CosmeticCoverage();

			// Token: 0x04000038 RID: 56
			public GameObject[] Slots = new GameObject[8];
		}

		// Token: 0x02000009 RID: 9
		private sealed class Owner : MonoBehaviour
		{
			// Token: 0x06000037 RID: 55 RVA: 0x00004526 File Offset: 0x00002726
			private void Update()
			{
				if (this.Holder == null)
				{
					Object.Destroy(base.gameObject);
				}
			}

			// Token: 0x04000039 RID: 57
			public GameObject Holder;
		}
	}
}
