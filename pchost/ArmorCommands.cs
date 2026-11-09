using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Alta.Console;
using Alta.Impact;
using Alta.Networking;
using Alta.Networking.Servers;
using Alta.StatSystem;
using Alta.Utilities;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x0200000A RID: 10
	[Module("armor", "ArmorRestored: the 0.0.32 leather, metal and Wyrm armor")]
	public static class ArmorCommands
	{
		// Token: 0x06000039 RID: 57 RVA: 0x0000454C File Offset: 0x0000274C
		[Command("list", "Every armor piece, its number and whether it registered; the loot line")]
		public static string List()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("ArmorRestored ").Append("0.5.0").Append(", registered via ").Append(Prefabs.Seam ?? "nothing yet").Append('\n');
			for (int i = 0; i < Catalog.Count; i++)
			{
				stringBuilder.Append(Catalog.Number(i)).Append("  ").Append(Catalog.Id(i)).Append("  ").Append(Catalog.Title(i));
				if (Prefabs.Refused[i] != null)
				{
					stringBuilder.Append("  REFUSED ").Append(Prefabs.Refused[i]);
				}
				else if (Prefabs.ByPiece[i] == null)
				{
					stringBuilder.Append("  not registered");
				}
				stringBuilder.Append('\n');
			}
			stringBuilder.Append(ArmorStand.Number).Append("  ").Append(ArmorStand.Name).Append("  ").Append(ArmorStand.Status).Append('\n');
			stringBuilder.Append("crafting: ").Append(ArmorCrafting.Status).Append('\n');
            stringBuilder.Append("loot: ").Append(Loot.Report);
			return stringBuilder.ToString();
		}

		// Token: 0x0600003A RID: 58 RVA: 0x000047D0 File Offset: 0x000029D0
        [Command("stand", "Spawn a fixed eight-slot armor stand on the ground in front of a player: armor stand <player>")]
        public static string Stand(string player)
        {
            return ArmorCommands.Run(delegate
            {
                if (ArmorStand.Prefab == null) return "Armor stand unavailable: " + ArmorStand.Status;
                string problem;
                Player target = Players.Find(player, out problem);
                if (target == null) return problem;
                Vector3 front = ServerArmor.Front(target);
                front.y = 0f;
                if (front.sqrMagnitude < 0.001f) front = Vector3.forward;
                front.Normalize();
                Vector3 at = ServerArmor.Head(target) + front * 1.5f;
                // Terrain and built floors need not use the layer named Ground.
                // Ignore triggers and the target avatar; use the nearest solid surface.
                RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up, Vector3.down, 12f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
                RaycastHit hit = new RaycastHit();
                bool found = false;
                foreach (RaycastHit candidate in hits)
                {
                    if (candidate.collider == null) continue;
                    if (target.PlayerCharacter != null && candidate.collider.transform.IsChildOf(target.PlayerCharacter.transform)) continue;
                    if (target.PlayerController != null && candidate.collider.transform.IsChildOf(target.PlayerController.transform)) continue;
                    hit = candidate;
                    found = true;
                    break;
                }
                if (!found)
                    return "No solid surface within 12 m below placement point " + at.ToString("F2") + "; check server terrain collision/player position";
                if (hit.normal.y < 0.9f) return "Surface is too steep for a fixed armor stand: " + hit.collider.name;
                NetworkEntity stand = SpawnHelper.Spawn(ArmorStand.Prefab, SpawnData.Default, null,
                    hit.point, Quaternion.LookRotation(-front, Vector3.up), null);
                return stand == null ? "Could not spawn armor stand" : "Spawned " + ArmorStand.Name + " as " + stand.Identifier + " at " + hit.point.ToString("F2") + " on " + hit.collider.name;
            });
        }

        [Command("recipes", "Spawn the 16 Leather/Metal recipe pages near a player: armor recipes <player>")]
        public static string Recipes(string player)
        {
            return ArmorCommands.Run(delegate
            {
                if (!ArmorCrafting.Ready) return "Armor recipes unavailable: " + ArmorCrafting.Status;
                string error;
                Player target = Players.Find(player, out error);
                if (target == null) return error;
                Vector3 at = ServerArmor.Head(target) + ServerArmor.Front(target);
                int count = 0;
                for (int i=0;i<16;i++)
                {
                    var page = SpawnHelper.Spawn(ArmorCrafting.Pages[i], SpawnData.DontGenerate, null,
                        at + new Vector3((i%4)*.15f-.225f,(i/4)*.04f,0f), Quaternion.identity, null);
                    if (page != null) count++;
                }
                return "Spawned " + count + "/16 armor recipe pages. Use a native crafting workbench; no Wyrm recipes.";
            });
        }

        [Command("stats", "Inspect worn armor weights and material protection: armor stats <player>")]
        public static string Stats(string player)
        {
            return ArmorCommands.Run(delegate
            {
                string error;
                Player target = Players.Find(player, out error);
                if (target == null) return error;
                int[] worn = ServerArmor.WornBy(target);
                uint[] materials = ServerArmor.MaterialsFor(Players.Name(target));
                StringBuilder result = new StringBuilder("Armor material stats " + (ArmorStats.Enabled ? "enabled" : "DISABLED") + " for " + Players.Name(target));
                float total = 0f;
                for (int slot = 0; slot < 8; slot++)
                {
                    if (worn[slot] < 0) continue;
                    float weight = ArmorStats.Weight(worn[slot], materials[slot]);
                    total += weight;
                    ArmorDefinition definition = Catalog.MaterialDefinition(worn[slot], materials[slot]);
                    result.Append("\n").Append(Catalog.Title(worn[slot])).Append(" material=").Append(materials[slot])
                        .Append(" weight=").Append(weight.ToString("F2"))
                        .Append(" physical damage multipliers=").Append(definition.GetMultiplier((ImpactType)0).ToString("F3"))
                        .Append("/").Append(definition.GetMultiplier((ImpactType)1).ToString("F3"))
                        .Append("/").Append(definition.GetMultiplier((ImpactType)2).ToString("F3"));
                }
                return result.Append("\nTotal worn weight=").Append(total.ToString("F2")).ToString();
            });
        }

        [Command("standinfo", "Inspect a spawned stand: armor standinfo <entityId>")]
        public static string StandInfo(uint entityId)
        {
            return ArmorCommands.Run(delegate
            {
                NetworkEntity entity = NetworkSceneManager.Current == null ? null : NetworkSceneManager.Current.GetEntity(entityId);
                if (entity == null) return "No server entity " + entityId;
                GameObject root = entity.gameObject;
                NetworkPrefab attached = root.GetComponent<NetworkPrefab>();
                NetworkPrefab linked = entity.Prefab;
                string identity = "Entity " + entityId + " name=" + root.name
                    + " linkedPrefab=" + (linked == null ? "null" : linked.name + "/" + linked.Hash)
                    + " attachedPrefab=" + (attached == null ? "null" : attached.name + "/" + attached.Hash);
                // Keep reporting even when the native prefab link is broken.
                StringBuilder fields = new StringBuilder();
                for (Type type = entity.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                {
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.Name.IndexOf("prefab", StringComparison.OrdinalIgnoreCase) < 0
                            && field.Name.IndexOf("hash", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        object value = field.GetValue(entity);
                        fields.Append(" ").Append(field.Name).Append("=").Append(value == null ? "null" : value.ToString());
                    }
                }
                return identity + fields.ToString() + " at " + root.transform.position.ToString("F2")
                    + " active=" + root.activeSelf + "/" + root.activeInHierarchy
                    + " scale=" + root.transform.lossyScale.ToString("F2")
                    + " parent=" + (root.transform.parent == null ? "none" : root.transform.parent.name)
                    + " embedded=" + (attached == null ? "none" : attached.Embedded.Count.ToString());
            });
        }

		[Command("give", "Spawn armor in front of a player: armor give <player> <leather|metal|wyrm> <slot|all> (slots: helmet chest shoulder_l shoulder_r gauntlet_l gauntlet_r tasset_l tasset_r)")]
		public static string Give(string player, string set, string slot)
		{
			return ArmorCommands.Run(delegate
			{
				string result;
				Player player2 = Players.Find(player, out result);
				if (player2 == null)
				{
					return result;
				}
				List<int> list;
				if (!ArmorCommands.Pieces(set, slot, out list, out result))
				{
					return result;
				}
				Vector3 vector = ServerArmor.Head(player2) + ServerArmor.Front(player2) * 0.6f + Vector3.down * 0.5f;
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < list.Count; i++)
				{
					NetworkEntity networkEntity = ServerArmor.Spawn(list[i], vector + Vector3.up * 0.12f * (float)i, Quaternion.identity);
					stringBuilder.Append((networkEntity != null) ? "spawned " : "could not spawn ").Append(Catalog.Title(list[i])).Append(" for ").Append(Players.Name(player2));
					if (networkEntity != null)
					{
						stringBuilder.Append(" as ").Append(networkEntity.Identifier).Append(" at ").Append(networkEntity.transform.position.ToString("F2"));
					}
					stringBuilder.Append('\n');
				}
				return stringBuilder.ToString().TrimEnd(new char[]
				{
					'\n'
				});
			});
		}

		// Token: 0x0600003B RID: 59 RVA: 0x0000489C File Offset: 0x00002A9C
		[Command("wear", "Put armor straight on a player: armor wear <player> <leather|metal|wyrm> <slot|all>")]
		public static string Wear(string player, string set, string slot)
		{
			return ArmorCommands.Run(delegate
			{
				string result;
				Player player2 = Players.Find(player, out result);
				if (player2 == null)
				{
					return result;
				}
				List<int> list;
				if (!ArmorCommands.Pieces(set, slot, out list, out result))
				{
					return result;
				}
				foreach (int piece in list)
				{
					ServerArmor.Wear(player2, piece);
				}
				return ServerArmor.Status(player2);
			});
		}

		// Token: 0x0600003C RID: 60 RVA: 0x0000493C File Offset: 0x00002B3C
		[Command("strip", "Take off everything a player wears and drop it in front of them: armor strip <player>")]
		public static string Strip(string player)
		{
			return ArmorCommands.Run(delegate
			{
				string result;
				Player player2 = Players.Find(player, out result);
				if (player2 == null)
				{
					return result;
				}
				return string.Concat(new object[]
				{
					Players.Name(player2),
					": ",
					ServerArmor.Strip(player2, true),
					" piece(s) dropped"
				});
			});
		}

		// Token: 0x0600003D RID: 61 RVA: 0x0000499C File Offset: 0x00002B9C
		[Command("status", "What a player wears and which hit zones carry armor now: armor status <player>")]
		public static string Status(string player)
		{
			return ArmorCommands.Run(delegate
			{
				string result;
				Player player2 = Players.Find(player, out result);
				if (player2 == null)
				{
					return result;
				}
				return ServerArmor.Status(player2);
			});
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00004D38 File Offset: 0x00002F38
		[Command("testhit", "Hit one of a player's own hit zones through the game's damage code, report the damage, then give the health back: armor testhit <player> <head|chest|hips|leftshoulder|rightshoulder|leftarm|rightarm> <pierce|slash|blunt|caustic|explosive> <damage>")]
		public static string TestHit(string player, string zone, string type, float damage)
		{
			return ArmorCommands.Run(delegate
			{
				string result;
				Player player2 = Players.Find(player, out result);
				if (player2 == null)
				{
					return result;
				}
				Wiring wiring = ServerArmor.WiringOf(player2);
				if (wiring == null)
				{
					return Players.Name(player2) + " is not wired yet";
				}
				int num = Array.FindIndex<string>(Body.ZoneNames, (string n) => string.Equals(n, zone, StringComparison.OrdinalIgnoreCase));
				if (num < 0)
				{
					return "no zone '" + zone + "'";
				}
				ImpactType impactType;
				try
				{
					impactType = (ImpactType)Enum.Parse(typeof(ImpactType), type, true);
				}
				catch (Exception)
				{
					return "no damage type '" + type + "'";
				}
				if (wiring.ZoneDetectors[num] == null || wiring.ZoneDetectors[num].Count == 0)
				{
					return "zone " + zone + " has no hit zone on this body";
				}
				ImpactDetector impactDetector = wiring.ZoneDetectors[num][0];
				HealthObject healthTarget = impactDetector.HealthTarget;
				if (healthTarget == null)
				{
					return "that hit zone has no health target";
				}
				if (damage <= 0f || damage >= healthTarget.Health * 0.5f)
				{
					return string.Concat(new string[]
					{
						"damage must be above 0 and below half of ",
						Players.Name(player2),
						"'s health (",
						healthTarget.Health.ToString("0.00"),
						")"
					});
				}
				Collider component = impactDetector.GetComponent<Collider>();
				ImpactHit impactHit = new ImpactHit(damage, 0f, 1f, 1f, impactDetector, impactType, impactDetector.transform.position, Vector3.up, Vector3.down, component, null, null);
				if (!impactHit.IsValid)
				{
					return "the game calls this hit invalid";
				}
				string text = ArmorCommands.Gates(healthTarget, impactDetector);
				float health = healthTarget.Health;
				impactDetector.OnImpactHit(impactHit);
				float health2 = healthTarget.Health;
				healthTarget.Health = health;
				ArmorDefinition currentArmor = impactDetector.CurrentArmor;
				return string.Concat(new object[]
				{
					Players.Name(player2),
					" ",
					Body.ZoneNames[num],
					" (",
					impactDetector.name,
					") ",
					impactType,
					" ",
					damage,
					": health ",
					health.ToString("0.00"),
					" -> ",
					health2.ToString("0.00"),
					" = ",
					(health - health2).ToString("0.00"),
					" taken; armor ",
					(currentArmor == null) ? "none" : (currentArmor.name + " x" + currentArmor.GetMultiplier(impactType).ToString("0.00")),
					"; health restored to ",
					healthTarget.Health.ToString("0.00"),
					"\n",
					text
				});
			});
		}

		// Token: 0x0600003F RID: 63 RVA: 0x00004D78 File Offset: 0x00002F78
		private static string Gates(HealthObject h, ImpactDetector d)
		{
			StringBuilder stringBuilder = new StringBuilder("gates:");
			IList list = typeof(HealthObject).GetField("impactDetectors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(h) as IList;
			stringBuilder.Append(" listed=").Append(list != null && list.Contains(d));
			Delegate @delegate = typeof(ImpactDetector).GetField("Hit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(d) as Delegate;
			List<string> list2 = new List<string>();
			if (@delegate != null)
			{
				foreach (Delegate delegate2 in @delegate.GetInvocationList())
				{
					list2.Add(delegate2.Method.DeclaringType.Name + "." + delegate2.Method.Name);
				}
			}
			stringBuilder.Append(" hit_subs=[").Append(string.Join(",", list2.ToArray())).Append(']');
			Stat stat = typeof(HealthObject).GetProperty("HealthStat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(h, null) as Stat;
			if (stat != null)
			{
				stringBuilder.Append(" health base=").Append(stat.Base.ToString("0.00")).Append(" min=").Append(stat.Minimum.ToString("0.00")).Append(" max=").Append(stat.Maximum.ToString("0.00")).Append(" extra=").Append(stat.Extra.ToString("0.00"));
			}
			if ((bool)typeof(HealthObject).GetField("hasDamageProtection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(h))
			{
				Stat stat2 = typeof(HealthObject).GetField("damageProtectionStat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(h) as Stat;
				stringBuilder.Append(" protection=").Append((stat2 != null) ? stat2.Value.ToString("0.00") : "?");
			}
			PlayerHealth playerHealth = h as PlayerHealth;
			if (playerHealth == null)
			{
				return stringBuilder.Append(" not PlayerHealth (").Append(h.GetType().Name).Append(')').ToString();
			}
			stringBuilder.Append(" alive=").Append(playerHealth.Player.IsAlive).Append(" ignoring=").Append(playerHealth.Player.IsIgnoringDamage);
			stringBuilder.Append(" invincible=").Append(typeof(PlayerHealth).GetField("isInvincibleToImpacts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(playerHealth));
			IPlayerControllerMultiLockModel playerControllerMultiLockModel = typeof(PlayerHealth).GetField("playerControllerMultiLockModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(playerHealth) as IPlayerControllerMultiLockModel;
			stringBuilder.Append(" locked=").Append((playerControllerMultiLockModel == null) ? "null" : playerControllerMultiLockModel.IsLocked.ToString());
			stringBuilder.Append(" immortals=").Append(PlayerHealth.ArePlayersAvoidingDamage.Value);
			ServerSettings settings = ServerHandler.Current.ServerConfig.Settings;
			stringBuilder.Append(" taken_x=").Append(settings.PlayerDamageTakenMultiplier.ToString("0.00"));
			foreach (AttackTypeMultiplier attackTypeMultiplier in h.Multipliers)
			{
				stringBuilder.Append(' ').Append(attackTypeMultiplier.Type).Append("_x=").Append(attackTypeMultiplier.Multiplier.ToString("0.00"));
			}
			return stringBuilder.ToString();
		}

		// Token: 0x06000040 RID: 64 RVA: 0x00005190 File Offset: 0x00003390
		[Command("debug", "Log every hit a player takes, with its armor, until the server restarts: armor debug <on|off>")]
		public static string DebugCmd(string state)
		{
			return ArmorCommands.Run(delegate
			{
				Settings.Debug = string.Equals(state, "on", StringComparison.OrdinalIgnoreCase);
				return "hit log " + (Settings.Debug ? "on" : "off") + " until the server restarts (settings.txt debug = on keeps it)";
			});
		}

		// Token: 0x06000041 RID: 65 RVA: 0x000051E0 File Offset: 0x000033E0
		[Command("loot", "Roll the game's own loot code on every loot dock in the world and count the armor: armor loot <rolls per dock>")]
		public static string LootCheck(int rolls)
		{
			return ArmorCommands.Run(() => Loot.Check(Math.Max(1, Math.Min(rolls, 1000))));
		}

		// Token: 0x06000042 RID: 66 RVA: 0x0000520C File Offset: 0x0000340C
		private static bool Pieces(string set, string slot, out List<int> pieces, out string why)
		{
			pieces = new List<int>();
			why = null;
			int num = Catalog.FindSet(set);
			if (num < 0)
			{
				why = "no armor set '" + set + "' (leather, metal, wyrm)";
				return false;
			}
			if (string.Equals(slot, "all", StringComparison.OrdinalIgnoreCase))
			{
				for (int i = 0; i < 8; i++)
				{
					pieces.Add(Catalog.Piece(num, i));
				}
			}
			else
			{
				int num2 = Catalog.FindSlot(slot);
				if (num2 < 0)
				{
					why = "no slot '" + slot + "' (helmet chest shoulder_l shoulder_r gauntlet_l gauntlet_r tasset_l tasset_r, or all)";
					return false;
				}
				pieces.Add(Catalog.Piece(num, num2));
			}
			foreach (int num3 in pieces)
			{
				if (Prefabs.ByPiece[num3] == null)
				{
					why = Catalog.Title(num3) + " is not registered: " + (Prefabs.Refused[num3] ?? "not yet");
					return false;
				}
			}
			return true;
		}

		// Token: 0x06000043 RID: 67 RVA: 0x00005310 File Offset: 0x00003510
		private static string Run(Func<string> f)
		{
			if (!Mod.IsServer)
			{
				return "server only";
			}
			string result;
			try
			{
				result = f();
			}
			catch (Exception ex)
			{
				Mod.Log.Error("armor command: " + ex);
				result = "error: " + ex.Message;
			}
			return result;
		}

		// Token: 0x0400003A RID: 58
		private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
	}
}
