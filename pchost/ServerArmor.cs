using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Alta.Chunks;
using Alta.Impact;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x0200001F RID: 31
	internal static class ServerArmor
	{
		// Token: 0x17000004 RID: 4
		// (get) Token: 0x0600008F RID: 143 RVA: 0x00008C38 File Offset: 0x00006E38
		private static string File
		{
			get
			{
				return Path.Combine(Mod.DataDir, "worn.txt");
			}
		}

		// Token: 0x06000090 RID: 144 RVA: 0x00008C4C File Offset: 0x00006E4C
		internal static void Load()
		{
			if (!System.IO.File.Exists(ServerArmor.File))
			{
				return;
			}
			foreach (string text in System.IO.File.ReadAllLines(ServerArmor.File))
			{
				string[] array2 = text.Split(new char[]
				{
					'\t'
				});
				if (array2.Length == 2 && array2[0].Length != 0)
				{
					string[] array3 = array2[1].Split(new char[]
					{
						','
					});
					int[] array4 = ServerArmor.Empty();
					int num = 0;
					while (num < array3.Length && num < 8)
					{
						string[] saved = array3[num].Split('@');
						array4[num] = ServerArmor.PieceById(saved[0]);
						uint material;
						if (saved.Length == 2 && uint.TryParse(saved[1], out material))
							ServerArmor.MaterialsFor(array2[0])[num] = material;
						num++;
					}
					ServerArmor.Worn[array2[0]] = array4;
				}
			}
			Mod.Log.Msg("worn armor loaded for " + ServerArmor.Worn.Count + " player(s)");
		}

		// Token: 0x06000091 RID: 145 RVA: 0x00008D34 File Offset: 0x00006F34
		private static void Save()
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, int[]> keyValuePair in ServerArmor.Worn)
			{
				bool flag = false;
				foreach (int num in keyValuePair.Value)
				{
					if (num >= 0)
					{
						flag = true;
					}
				}
				if (flag)
				{
					stringBuilder.Append(keyValuePair.Key).Append('\t');
					for (int j = 0; j < 8; j++)
					{
						if (j > 0)
						{
							stringBuilder.Append(',');
						}
						stringBuilder.Append((keyValuePair.Value[j] >= 0) ? Catalog.Id(keyValuePair.Value[j]) : "-");
						uint material = ServerArmor.MaterialsFor(keyValuePair.Key)[j];
						if (keyValuePair.Value[j] >= 0 && material != 0U)
							stringBuilder.Append('@').Append(material);
					}
					stringBuilder.Append('\n');
				}
			}
			string text = ServerArmor.File + ".tmp";
			System.IO.File.WriteAllText(text, stringBuilder.ToString());
			if (System.IO.File.Exists(ServerArmor.File))
			{
				System.IO.File.Delete(ServerArmor.File);
			}
			System.IO.File.Move(text, ServerArmor.File);
		}

		// Token: 0x06000092 RID: 146 RVA: 0x00008E60 File Offset: 0x00007060
		private static int PieceById(string id)
		{
			for (int i = 0; i < Catalog.Count; i++)
			{
				if (Catalog.Id(i) == id)
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x06000093 RID: 147 RVA: 0x00008E90 File Offset: 0x00007090
		internal static int[] Empty()
		{
			int[] array = new int[8];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = -1;
			}
			return array;
		}

		// Token: 0x06000094 RID: 148 RVA: 0x00008EB8 File Offset: 0x000070B8
		internal static int[] WornBy(Player p)
		{
			string key = Players.Name(p);
			int[] result;
			if (!ServerArmor.Worn.TryGetValue(key, out result))
			{
				result = (ServerArmor.Worn[key] = ServerArmor.Empty());
			}
			return result;
		}

		// Token: 0x06000095 RID: 149 RVA: 0x00008EF0 File Offset: 0x000070F0
		internal static Wiring WiringOf(Player p)
		{
			if (p == null || p.PlayerCharacter == null)
			{
				return null;
			}
			Wiring result;
			if (!ServerArmor.Wired.TryGetValue(p.PlayerCharacter.GetInstanceID(), out result))
			{
				return null;
			}
			return result;
		}

		// Token: 0x06000096 RID: 150 RVA: 0x00008F4C File Offset: 0x0000714C
		internal static void Tick()
		{
			if (Time.unscaledTime < ServerArmor._next)
			{
				return;
			}
			ServerArmor._next = Time.unscaledTime + 0.5f;
			if (!Prefabs.Done)
			{
				return;
			}
			List<int> list = new List<int>();
			foreach (KeyValuePair<int, Wiring> keyValuePair in ServerArmor.Wired)
			{
				if (keyValuePair.Value.Root == null)
				{
					list.Add(keyValuePair.Key);
				}
			}
			foreach (int key in list)
			{
				ServerArmor.Wired.Remove(key);
			}
			foreach (IPlayer player in Player.AllPlayers)
			{
				Player player2 = player as Player;
				if (!(player2 == null) && player2.UserInfo != null && !(player2.PlayerCharacter == null))
				{
					int instanceID = player2.PlayerCharacter.GetInstanceID();
					if (!ServerArmor.Wired.ContainsKey(instanceID))
					{
						Wiring w = Body.Wire(player2.PlayerCharacter.transform, true);
						ServerArmor.Wired[instanceID] = w;
						Body.Apply(w, ServerArmor.WornBy(player2));
						HealthObject healthObject = Body.Health(w);
						if (healthObject != null)
						{
							healthObject.HealthChanged += delegate(HealthObject h, float change, DamageData data)
							{
								ServerArmor.Hurt(w, h, change, data);
							};
						}
						string text = Players.Name(player2) + ": " + Body.Describe(w);
						if (ServerArmor.Logged.Add(text))
						{
							Mod.Log.Msg("wired " + text);
						}
						ServerArmor._dirty = true;
					}
				}
			}
			if (ServerArmor._dirty)
			{
				ServerArmor._dirty = false;
				ServerArmor.Broadcast();
			}
		}

		// Token: 0x06000097 RID: 151 RVA: 0x000091AC File Offset: 0x000073AC
		private static void Hurt(Wiring w, HealthObject h, float change, DamageData data)
		{
			if (!Settings.Debug || change >= 0f)
			{
				return;
			}
			PlayerHealth playerHealth = h as PlayerHealth;
			string text = (playerHealth != null && playerHealth.Player != null && playerHealth.Player.NetworkPlayer != null) ? Players.Name(playerHealth.Player.NetworkPlayer) : h.name;
			ImpactHit impactHit = data as ImpactHit;
			string text2 = (data == null) ? "no hit" : data.GetType().Name;
			if (impactHit != null)
			{
				ArmorDefinition armorDefinition = (impactHit.ImpactDetector != null) ? impactHit.ImpactDetector.CurrentArmor : null;
				text2 = string.Concat(new object[]
				{
					impactHit.ImpactType,
					" ",
					impactHit.HealthDamage.ToString("0.000"),
					" on ",
					Body.ZoneOf(w, impactHit.ImpactDetector),
					", armor ",
					(armorDefinition == null) ? "none" : (armorDefinition.name + " x" + armorDefinition.GetMultiplier(impactHit.ImpactType).ToString("0.00")),
					(impactHit.Wielder != null) ? (", from " + impactHit.Wielder.name) : ""
				});
			}
			Mod.Log.Msg(string.Concat(new string[]
			{
				text,
				" lost ",
				(-change).ToString("0.000"),
				" health (",
				text2,
				"), now ",
				h.Health.ToString("0.00")
			}));
		}

		// Token: 0x06000098 RID: 152 RVA: 0x00009390 File Offset: 0x00007590
		private static void Broadcast()
		{
			List<KeyValuePair<int, int[]>> list = new List<KeyValuePair<int, int[]>>();
			List<Player> list2 = new List<Player>();
			foreach (IPlayer player in Player.AllPlayers)
			{
				Player player2 = player as Player;
				if (!(player2 == null) && player2.UserInfo != null)
				{
					list2.Add(player2);
					int[] row = new int[16];
					Array.Copy(ServerArmor.WornBy(player2), row, 8);
					uint[] materials = MaterialsFor(Players.Name(player2));
					for (int slot = 0; slot < 8; slot++) row[8 + slot] = unchecked((int)materials[slot]);
					list.Add(new KeyValuePair<int, int[]>(player2.UserInfo.Identifier, row));
				}
			}
			foreach (Player to in list2)
			{
				Net.SendTable(to, list);
			}
		}

		// Token: 0x06000099 RID: 153 RVA: 0x00009464 File Offset: 0x00007664
		private static void Changed(Player p)
		{
			ServerArmor.Save();
			Body.Apply(ServerArmor.WiringOf(p), ServerArmor.WornBy(p));
			ServerArmor._dirty = true;
		}

		// Token: 0x0600009A RID: 154 RVA: 0x00009484 File Offset: 0x00007684
		internal static void OnEquip(Player p, uint entityId)
		{
			NetworkEntity networkEntity = (NetworkSceneManager.Current != null) ? NetworkSceneManager.Current.GetEntity(entityId) : null;
			if (networkEntity == null || networkEntity.Prefab == null || !Prefabs.IsArmor(networkEntity.Prefab.Hash))
			{
				Net.SendNote(p, "equip " + entityId + ": not an armor piece");
				return;
			}
			int num = Catalog.PieceOf(networkEntity.Prefab.Hash);
			Vector3 position = networkEntity.transform.position;
			Vector3 vector = ServerArmor.Head(p);
			if ((position - vector).magnitude > 2f)
			{
				Net.SendNote(p, string.Concat(new string[]
				{
					"equip: ",
					Catalog.Title(num),
					" is ",
					(position - vector).magnitude.ToString("0.0"),
					" m away"
				}));
				return;
			}
			int num2 = num % 8;
			int[] array = ServerArmor.WornBy(p);
			int num3 = array[num2];
			uint material = ArmorMaterials.Read(networkEntity);
			uint[] materials = MaterialsFor(Players.Name(p));
            if (Catalog.SetOf(num).Id == "metal" && material == 0U)
            {
                Net.SendNote(p, "equip cancelled: could not read this item's metal material; item was not consumed");
                Mod.Log.Error("metal equip refused: PhysicalMaterialPart has no readable material on entity " + entityId);
                return;
            }
            // Do not consume the incoming item if the replaced piece cannot be returned.
            if (num3 >= 0 && ServerArmor.Spawn(num3, position, Quaternion.identity, materials[num2]) == null)
            {
                Net.SendNote(p, "equip cancelled: could not return the previously worn piece");
                return;
            }
            networkEntity.SceneDestroy();
			array[num2] = num;
			materials[num2] = material;
			Mod.Log.Msg(Players.Name(p) + " put on " + Catalog.Title(num) + ((num3 >= 0) ? (" (dropped " + Catalog.Title(num3) + ")") : ""));
			ServerArmor.Changed(p);
		}

		// Token: 0x0600009B RID: 155 RVA: 0x00009600 File Offset: 0x00007800
		internal static void OnUnequip(Player p, int slot, int hand)
		{
			if (slot < 0 || slot >= 8)
			{
				return;
			}
			int[] array = ServerArmor.WornBy(p);
			int num = array[slot];
			if (num < 0)
			{
				return;
			}
			int removedBy = Catalog.Slots[slot].RemovedBy;
			if ((removedBy == 1 && hand != 0) || (removedBy == 2 && hand != 1))
			{
				Net.SendNote(p, "the " + Catalog.Slots[slot].Title + " comes off with the other hand");
				return;
			}
			Vector3 at = ServerArmor.Hand(p, hand);
			NetworkEntity networkEntity = ServerArmor.Spawn(num, at, Quaternion.identity, MaterialsFor(Players.Name(p))[slot]);
			if (networkEntity == null)
			{
				Net.SendNote(p, "could not spawn " + Catalog.Title(num));
				return;
			}
			MaterialsFor(Players.Name(p))[slot] = 0U;
			array[slot] = -1;
			Mod.Log.Msg(Players.Name(p) + " took off " + Catalog.Title(num));
			ServerArmor.Changed(p);
			Net.SendTook(p, networkEntity.Identifier, hand);
		}

		// Token: 0x0600009C RID: 156 RVA: 0x000096D4 File Offset: 0x000078D4
		internal static NetworkEntity Spawn(int piece, Vector3 at, Quaternion rot, uint material = 0U)
		{
			NetworkPrefab networkPrefab = Prefabs.ByPiece[piece];
			if (networkPrefab == null)
			{
				return null;
			}
			// Set the template before cloning so the native material behaviour sees the
			// selected value during initialization and initial network serialization.
			NetworkEntity template = networkPrefab.GetComponent<NetworkEntity>();
			uint previous = template == null ? 0U : ArmorMaterials.Read(template);
			NetworkEntity networkEntity;
			try
			{
				ArmorMaterials.Restore(template, material);
				networkEntity = SpawnHelper.Spawn(networkPrefab, SpawnData.Default, null, at, rot, null);
			}
			finally
			{
				ArmorMaterials.Restore(template, previous);
			}
			if (networkEntity != null)
			{
                // Spawn initialization can overwrite the cloned template's material.
                // Restore the live entity too, rather than relying solely on the prefab.
                ArmorMaterials.Restore(networkEntity, material);
                if (material != 0U && ArmorMaterials.Read(networkEntity) != material)
                    throw new InvalidOperationException("spawned armor did not retain material " + material);
				networkEntity.Chunk = LocationChunkHelper.GetBestContaining(at, null);
			}
			return networkEntity;
		}

		// Token: 0x0600009D RID: 157 RVA: 0x0000971B File Offset: 0x0000791B
		internal static void Wear(Player p, int piece)
		{
			ServerArmor.WornBy(p)[piece % 8] = piece;
			MaterialsFor(Players.Name(p))[piece % 8] = 0U;
			ServerArmor.Changed(p);
		}

		// Token: 0x0600009E RID: 158 RVA: 0x00009730 File Offset: 0x00007930
		internal static int Strip(Player p, bool drop)
		{
			int[] array = ServerArmor.WornBy(p);
			int num = 0;
			Vector3 vector = ServerArmor.Head(p) + ServerArmor.Front(p) * 0.6f + Vector3.down * 0.6f;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] >= 0)
				{
					if (drop)
					{
						ServerArmor.Spawn(array[i], vector + Vector3.up * 0.15f * (float)num, Quaternion.identity, MaterialsFor(Players.Name(p))[i]);
					}
					MaterialsFor(Players.Name(p))[i] = 0U;
					array[i] = -1;
					num++;
				}
			}
			if (num > 0)
			{
				ServerArmor.Changed(p);
			}
			return num;
		}

		// Token: 0x0600009F RID: 159 RVA: 0x000097CC File Offset: 0x000079CC
		internal static string Status(Player p)
		{
			int[] array = ServerArmor.WornBy(p);
			List<string> list = new List<string>();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] >= 0)
				{
					list.Add(Catalog.Title(array[i]));
				}
			}
			return string.Concat(new string[]
			{
				Players.Name(p),
				" wears: ",
				(list.Count == 0) ? "nothing" : string.Join(", ", list.ToArray()),
				"\n",
				Body.Describe(ServerArmor.WiringOf(p))
			});
		}

		// Token: 0x060000A0 RID: 160 RVA: 0x00009860 File Offset: 0x00007A60
		internal static Vector3 Head(Player p)
		{
			if (p.PlayerController != null && p.PlayerController.Head != null)
			{
				return p.PlayerController.Head.position;
			}
			if (!(p.PlayerCharacter != null))
			{
				return Vector3.zero;
			}
			return p.PlayerCharacter.transform.position + Vector3.up * 1.6f;
		}

		// Token: 0x060000A1 RID: 161 RVA: 0x000098D8 File Offset: 0x00007AD8
		internal static Vector3 Front(Player p)
		{
			Vector3 vector = (p.PlayerController != null && p.PlayerController.Head != null) ? p.PlayerController.Head.forward : Vector3.forward;
			vector.y = 0f;
			if (vector.sqrMagnitude <= 0.0001f)
			{
				return Vector3.forward;
			}
			return vector.normalized;
		}

		// Token: 0x060000A2 RID: 162 RVA: 0x00009948 File Offset: 0x00007B48
		private static Vector3 Hand(Player p, int hand)
		{
			PlayerController playerController = p.PlayerController;
			Controller controller = (playerController == null) ? null : ((hand == 0) ? playerController.LeftController : playerController.RightController);
			if (controller != null)
			{
				return controller.transform.position;
			}
			return ServerArmor.Head(p) + ServerArmor.Front(p) * 0.4f + Vector3.down * 0.3f;
		}

		// Token: 0x040000A6 RID: 166
		private const float EquipReach = 2f;

		// Token: 0x040000A7 RID: 167
		private static readonly Dictionary<string, uint[]> Materials = new Dictionary<string, uint[]>(StringComparer.OrdinalIgnoreCase);

		private static uint[] MaterialsFor(string player)
		{
			uint[] values;
			if (!Materials.TryGetValue(player, out values))
			{
				values = new uint[8];
				Materials[player] = values;
			}
			return values;
		}

		private static readonly Dictionary<string, int[]> Worn = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

		// Token: 0x040000A8 RID: 168
		private static readonly Dictionary<int, Wiring> Wired = new Dictionary<int, Wiring>();

		// Token: 0x040000A9 RID: 169
		private static readonly HashSet<string> Logged = new HashSet<string>();

		// Token: 0x040000AA RID: 170
		private static float _next;

		// Token: 0x040000AB RID: 171
		private static bool _dirty;
	}
}
