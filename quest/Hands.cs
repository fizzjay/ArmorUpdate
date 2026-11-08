using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppAlta.Utilities;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestoredQuest
{
	// Token: 0x02000008 RID: 8
	internal static class Hands
	{
		// Token: 0x06000030 RID: 48 RVA: 0x00003BB4 File Offset: 0x00001DB4
		internal static void OnTook(uint id, int hand)
		{
			Hands._heardId = id;
			Hands._heardHand = hand;
			Hands._heardPending = true;
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00003BC8 File Offset: 0x00001DC8
		internal static void Clear()
		{
			Hands.Held[0] = (Hands.Held[1] = null);
			Hands.Closed[0] = (Hands.Closed[1] = false);
			Hands.PendingSlot[0] = (Hands.PendingSlot[1] = -1);
			Hands._tookId = 0U;
			Hands._heardPending = false;
			Hands._gripFailed = false;
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00003C20 File Offset: 0x00001E20
		internal static void Tick()
		{
			if (!Gate.IsOpen || !Registry.Done || !Pack.Loaded)
			{
				return;
			}
			if (Hands._heardPending)
			{
				Hands._heardPending = false;
				Hands._tookId = Hands._heardId;
				Hands._tookHand = Hands._heardHand;
				Hands._tookUntil = Time.unscaledTime + 3f;
				Hands.Sound(false);
			}
			Dressed dressed = Draw.Local;
			if (dressed != null && dressed.Root == null)
			{
				dressed = null;
			}
			int[] worn = Draw.WornBy(dressed);
			for (int i = 0; i < 2; i++)
			{
				Hands.Hand(i, dressed, worn);
			}
			if (Hands._tookId != 0U)
			{
				Hands.GrabTaken();
			}
		}

		// Token: 0x06000033 RID: 51 RVA: 0x00003CBC File Offset: 0x00001EBC
		private static void Hand(int hand, Dressed me, int[] worn)
		{
			Interactor interactor = Hands.HandInteractor(hand);
			if (interactor == null)
			{
				return;
			}
			bool isInteracting = interactor.IsInteracting;
			Interactable interactable = isInteracting ? interactor.InteractingWith : null;
			Pickup pickup = (interactable == null) ? null : interactable.TryCast<Pickup>();
			if (pickup != null && Hands.PieceOf(pickup) < 0)
			{
				pickup = null;
			}
			Pickup pickup2 = Hands.Held[hand];
			bool flag = pickup2 != null && pickup != null && pickup2.Pointer == pickup.Pointer;
			if (Settings.Debug && pickup != null && !flag)
			{
				Mod.Log.Msg(string.Concat(new string[]
				{
					"holding ",
					Catalog.Title(Hands.PieceOf(pickup)),
					" in the ",
					Hands.HandName(hand),
					" hand"
				}));
			}
			if (pickup2 != null && !flag && me != null)
			{
				Hands.LetGo(me, pickup2);
			}
			Hands.Held[hand] = pickup;
			bool flag2 = Hands.Grip(interactor);
			bool flag3 = flag2 && !Hands.Closed[hand];
			Hands.Closed[hand] = flag2;
			if (!flag2 || isInteracting)
			{
				Hands.PendingSlot[hand] = -1;
				return;
			}
			if (worn == null || me == null || Time.unscaledTime < Hands._cooldown)
			{
				return;
			}
			Vector3 position = interactor.transform.position;
			if (flag3 || Hands.PendingSlot[hand] < 0)
			{
				Hands.PendingSlot[hand] = Hands.TouchedSlot(me, worn, position, hand);
				Hands.PendingAt[hand] = Time.unscaledTime;
				if (Settings.Debug)
				{
					Mod.Log.Msg(string.Concat(new string[]
					{
						Hands.HandName(hand),
						" hand closed empty ",
						(Hands.PendingSlot[hand] >= 0) ? ("on " + Catalog.Title(worn[Hands.PendingSlot[hand]])) : "on no worn piece",
						" (",
						Hands.Distances(me, worn, position),
						")"
					}));
				}
				return;
			}
			int num = Hands.PendingSlot[hand];
			if (num < 0 || Time.unscaledTime - Hands.PendingAt[hand] < 0.35f)
			{
				return;
			}
			Hands.PendingSlot[hand] = -1;
			if (worn[num] < 0 || Hands.TouchedSlot(me, worn, position, hand) != num)
			{
				return;
			}
			Hands._cooldown = Time.unscaledTime + 1f;
			Wire.SendUnequip(num, hand);
			Mod.Log.Msg(string.Concat(new string[]
			{
				"take off ",
				Catalog.Title(worn[num]),
				" with the ",
				Hands.HandName(hand),
				" hand"
			}));
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00003F4C File Offset: 0x0000214C
		private static bool Grip(Interactor it)
		{
			if (Hands._gripFailed)
			{
				return false;
			}
			bool result;
			try
			{
				Controller controller = it.Controller;
				PlayerInput playerInput = (controller == null) ? null : controller.PlayerInput;
				ControllerInput controllerInput = (playerInput == null) ? null : playerInput.RawInput;
				result = (controllerInput != null && controllerInput.GrabAxis > 0.8f);
			}
			catch (Exception ex)
			{
				Hands._gripFailed = true;
				Mod.Log.Error("the grip input cannot be read, so worn armor cannot be taken off by hand: " + ex.Message);
				result = false;
			}
			return result;
		}

		// Token: 0x06000035 RID: 53 RVA: 0x00003FE0 File Offset: 0x000021E0
		private static Interactor HandInteractor(int hand)
		{
			PlayerController playerController = PlayerController.Current;
			if (playerController == null)
			{
				return null;
			}
			Controller controller = (hand == 0) ? playerController.LeftController : playerController.RightController;
			if (!(controller == null))
			{
				return controller.Interactor;
			}
			return null;
		}

		// Token: 0x06000036 RID: 54 RVA: 0x00004021 File Offset: 0x00002221
		private static string HandName(int hand)
		{
			if (hand != 0)
			{
				return "right";
			}
			return "left";
		}

		// Token: 0x06000037 RID: 55 RVA: 0x00004034 File Offset: 0x00002234
		private static int PieceOf(Pickup p)
		{
			if (p == null)
			{
				return -1;
			}
			Item item = p.Item;
			if (item == null)
			{
				return -1;
			}
			uint hash = item.Hash;
			if (!Registry.IsOurs(hash))
			{
				return -1;
			}
			return Catalog.PieceOf(hash);
		}

		// Token: 0x06000038 RID: 56 RVA: 0x00004078 File Offset: 0x00002278
		private static void LetGo(Dressed me, Pickup was)
		{
			int num = Hands.PieceOf(was);
			if (num < 0)
			{
				return;
			}
			NetworkEntity entity = was.Entity;
			if (entity == null)
			{
				return;
			}
			int slot = num % 8;
			float num2 = Hands.Distance(Hands.Center(was.transform), Hands.Anchor(me, slot));
			if (num2 > Catalog.Reach(slot))
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
						Catalog.Reach(slot).ToString("0.0"),
						")"
					}));
				}
				return;
			}
			Wire.SendEquip(entity.Identifier);
			Hands.Sound(true);
			Mod.Log.Msg(string.Concat(new string[]
			{
				"put on ",
				Catalog.Title(num),
				" (",
				num2.ToString("0.00"),
				" m from its place)"
			}));
		}

		// Token: 0x06000039 RID: 57 RVA: 0x0000418C File Offset: 0x0000238C
		private static int TouchedSlot(Dressed me, int[] worn, Vector3 hand, int handIndex)
		{
			int result = -1;
			float num = 0.2f;
			for (int i = 0; i < 8; i++)
			{
				if (worn[i] >= 0)
				{
					int num2 = Catalog.RemovedBy[i];
					if ((num2 != 1 || handIndex == 0) && (num2 != 2 || handIndex == 1))
					{
						float num3 = Hands.Distance(hand, Hands.SlotPoint(me, i));
						if (num3 < num)
						{
							num = num3;
							result = i;
						}
					}
				}
			}
			return result;
		}

		// Token: 0x0600003A RID: 58 RVA: 0x000041E4 File Offset: 0x000023E4
		private static string Distances(Dressed me, int[] worn, Vector3 hand)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < 8; i++)
			{
				if (worn[i] >= 0)
				{
					list.Add(Catalog.SlotIds[i] + " " + Hands.Distance(hand, Hands.SlotPoint(me, i)).ToString("0.00"));
				}
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray()) + " m";
			}
			return "nothing worn";
		}

		// Token: 0x0600003B RID: 59 RVA: 0x00004264 File Offset: 0x00002464
		private static Vector3 SlotPoint(Dressed me, int s)
		{
            // Keep removal reachable even when the visual mesh bounds are offset.
            return Hands.Anchor(me, s);
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00004294 File Offset: 0x00002494
		private static Vector3 Anchor(Dressed me, int slot)
		{
			Transform transform = Draw.Bone(me, Catalog.Anchors[slot]);
			if (transform == null)
			{
				return me.Root.position;
			}
			Vector3 position = transform.position;
			if (slot == 0)
			{
				position.y += 0.08f;
			}
			if (slot == 6 || slot == 7)
			{
				Transform transform2 = Draw.Bone(me, "shoulder_L_jnt");
				Transform transform3 = Draw.Bone(me, "shoulder_R_jnt");
				if (transform2 != null && transform3 != null)
				{
					Vector3 position2 = transform2.position;
					Vector3 position3 = transform3.position;
					float num = position2.x - position3.x;
					float num2 = position2.z - position3.z;
					float num3 = (float)Math.Sqrt((double)(num * num + num2 * num2));
					if (num3 > 0.01f)
					{
						float num4 = ((slot == 6) ? 0.12f : -0.12f) / num3;
						position.x += num * num4;
						position.z += num2 * num4;
					}
				}
			}
			return position;
		}

		// Token: 0x0600003D RID: 61 RVA: 0x00004398 File Offset: 0x00002598
		private static Vector3 Center(Transform t)
		{
			List<Renderer> list = new List<Renderer>();
			foreach (Component component in t.GetComponentsInChildren(Il2CppType.Of<Renderer>(), false))
			{
				Renderer renderer = (component == null) ? null : component.TryCast<Renderer>();
				if (renderer != null)
				{
					list.Add(renderer);
				}
			}
			Vector3 result;
			if (!Hands.Center(list, out result))
			{
				return t.position;
			}
			return result;
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00004424 File Offset: 0x00002624
		private static bool Center(List<Renderer> rs, out Vector3 p)
		{
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MinValue;
			float num6 = float.MinValue;
			int num7 = 0;
			foreach (Renderer renderer in rs)
			{
				if (!(renderer == null))
				{
					Bounds bounds = renderer.bounds;
					Vector3 center = bounds.m_Center;
					Vector3 extents = bounds.m_Extents;
					num = Math.Min(num, center.x - extents.x);
					num2 = Math.Min(num2, center.y - extents.y);
					num3 = Math.Min(num3, center.z - extents.z);
					num4 = Math.Max(num4, center.x + extents.x);
					num5 = Math.Max(num5, center.y + extents.y);
					num6 = Math.Max(num6, center.z + extents.z);
					num7++;
				}
			}
			p = default(Vector3);
			if (num7 == 0)
			{
				return false;
			}
			p.x = (num + num4) * 0.5f;
			p.y = (num2 + num5) * 0.5f;
			p.z = (num3 + num6) * 0.5f;
			return true;
		}

		// Token: 0x0600003F RID: 63 RVA: 0x0000458C File Offset: 0x0000278C
		private static float Distance(Vector3 a, Vector3 b)
		{
			float num = a.x - b.x;
			float num2 = a.y - b.y;
			float num3 = a.z - b.z;
			return (float)Math.Sqrt((double)(num * num + num2 * num2 + num3 * num3));
		}

		// Token: 0x06000040 RID: 64 RVA: 0x000045D4 File Offset: 0x000027D4
		private static void GrabTaken()
		{
			if (Time.unscaledTime > Hands._tookUntil)
			{
				Hands._tookId = 0U;
				return;
			}
			INetworkScene networkScene = NetworkSceneManager.Current;
			NetworkEntity networkEntity = (networkScene == null) ? null : networkScene.GetEntity(Hands._tookId);
			if (networkEntity == null)
			{
				return;
			}
			Component component = networkEntity.GetComponent(Il2CppType.Of<Pickup>());
			Pickup pickup = (component == null) ? null : component.TryCast<Pickup>();
			Interactor interactor = Hands.HandInteractor(Hands._tookHand);
			Hands._tookId = 0U;
			if (pickup == null || interactor == null)
			{
				return;
			}
			try
			{
				Crumb.First("SwapInteract");
				interactor.SwapInteract(pickup, true);
			}
			catch (Exception ex)
			{
				Mod.Log.Warning("could not put the piece in the hand: " + ex.Message);
			}
		}

		// Token: 0x06000041 RID: 65 RVA: 0x000046A4 File Offset: 0x000028A4
		private static void Sound(bool on)
		{
			try
			{
				if (!Hands._soundsLooked)
				{
					Hands._soundsLooked = true;
					Crumb.At("sounds lookup");
					foreach (Object @object in Resources.FindObjectsOfTypeAll(Il2CppType.Of<SoundEffectSet>()))
					{
						SoundEffectSet soundEffectSet = (@object == null) ? null : @object.TryCast<SoundEffectSet>();
						if (!(soundEffectSet == null))
						{
							string a = soundEffectSet.soundEffectType.ToString();
							if (a == "EquipmentApplied")
							{
								Hands._soundOn = soundEffectSet;
							}
							if (a == "EquipmentTakenOff")
							{
								Hands._soundOff = soundEffectSet;
							}
						}
					}
					Hands._noProperties = new Il2CppReferenceArray<ISoundProperty>(Il2CppSystem.Array.CreateInstance(Il2CppType.Of<ISoundProperty>(), 0).Pointer);
					Mod.Log.Msg("equipment sounds: " + ((Hands._soundOn != null) ? "on" : "-") + "/" + ((Hands._soundOff != null) ? "off" : "-"));
				}
				SoundEffectSet soundEffectSet2 = on ? Hands._soundOn : Hands._soundOff;
				PlayerController playerController = PlayerController.Current;
				AudioManager instance = SingletonBehaviour<AudioManager>.Instance;
				if (!(soundEffectSet2 == null) && !(playerController == null) && !(instance == null))
				{
					GameObject gameObject = (playerController.Head != null) ? playerController.Head.gameObject : playerController.gameObject;
					Crumb.First("PlaySoundEffectOnObject");
					instance.PlaySoundEffectOnObject(soundEffectSet2, gameObject, gameObject, Hands._noProperties);
				}
			}
			catch (Exception ex)
			{
				Mod.Log.Warning("sound: " + ex.Message);
			}
		}

		// Token: 0x0400002C RID: 44
		private const float HoldToRemove = 0.35f;

		// Token: 0x0400002D RID: 45
		private const float TouchReach = 0.2f;

		// Token: 0x0400002E RID: 46
		private static readonly Pickup[] Held = new Pickup[2];

		// Token: 0x0400002F RID: 47
		private static readonly bool[] Closed = new bool[2];

		// Token: 0x04000030 RID: 48
		private static readonly int[] PendingSlot = new int[]
		{
			-1,
			-1
		};

		// Token: 0x04000031 RID: 49
		private static readonly float[] PendingAt = new float[2];

		// Token: 0x04000032 RID: 50
		private static float _cooldown;

		// Token: 0x04000033 RID: 51
		private static float _tookUntil;

		// Token: 0x04000034 RID: 52
		private static uint _tookId;

		// Token: 0x04000035 RID: 53
		private static uint _heardId;

		// Token: 0x04000036 RID: 54
		private static int _tookHand;

		// Token: 0x04000037 RID: 55
		private static int _heardHand;

		// Token: 0x04000038 RID: 56
		private static bool _heardPending;

		// Token: 0x04000039 RID: 57
		private static bool _soundsLooked;

		// Token: 0x0400003A RID: 58
		private static bool _gripFailed;

		// Token: 0x0400003B RID: 59
		private static SoundEffectSet _soundOn;

		// Token: 0x0400003C RID: 60
		private static SoundEffectSet _soundOff;

		// Token: 0x0400003D RID: 61
		private static Il2CppReferenceArray<ISoundProperty> _noProperties;
	}
}
