using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Alta.Networking;
using Alta.Networking.Servers;
using Alta.Serialization;

namespace ArmorRestored
{
	// Token: 0x0200000F RID: 15
	internal static class Net
	{
		// Token: 0x06000057 RID: 87 RVA: 0x0000618D File Offset: 0x0000438D
		internal static void SendHere(Connection c)
		{
			c.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 20;
				s.SerializeByte(ref b);
				string text = "0.3.0";
				s.SerializeString(ref text, 1);
				int num = 2;
				s.SerializeInteger(ref num);
			});
		}

		// Token: 0x06000058 RID: 88 RVA: 0x000061FB File Offset: 0x000043FB
		internal static void SendHello(Connection c)
		{
			c.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 5;
				s.SerializeByte(ref b);
				int num = 2;
				s.SerializeInteger(ref num);
				string text = "0.3.0";
				string text2 = Prefabs.RefusedLine();
				s.SerializeString(ref text, 1);
				s.SerializeString(ref text2, 1);
			});
		}

		// Token: 0x06000059 RID: 89 RVA: 0x0000623F File Offset: 0x0000443F
		internal static void SendAck(Connection c)
		{
			c.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 4;
				s.SerializeByte(ref b);
			});
		}

		// Token: 0x0600005A RID: 90 RVA: 0x0000629C File Offset: 0x0000449C
		internal static void SendEquip(uint entity)
		{
			Connection toServer = Net.ToServer;
			if (toServer == null || toServer.IsDisposed)
			{
				return;
			}
			toServer.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 2;
				s.SerializeByte(ref b);
				uint entity2 = entity;
				s.SerializeBits(ref entity2, 32);
			});
		}

		// Token: 0x0600005B RID: 91 RVA: 0x00006324 File Offset: 0x00004524
		internal static void SendUnequip(int slot, int hand)
		{
			Connection toServer = Net.ToServer;
			if (toServer == null || toServer.IsDisposed)
			{
				return;
			}
			toServer.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 3;
				byte b2 = (byte)slot;
				byte b3 = (byte)hand;
				s.SerializeByte(ref b);
				s.SerializeByte(ref b2);
				s.SerializeByte(ref b3);
			});
		}

		// Token: 0x0600005C RID: 92 RVA: 0x00006370 File Offset: 0x00004570
		internal static void ServerRead(Connection connection, Stream s)
		{
			byte b = 0;
			s.SerializeByte(ref b);
			if (b == 5 || b == 1)
			{
				int wire = 1;
				if (b == 5)
				{
					s.SerializeInteger(ref wire);
				}
				string text = null;
				string text2 = null;
				s.SerializeString(ref text, 1);
				s.SerializeString(ref text2, 1);
				lock (Net.Hellos)
				{
					Net.Hellos[connection.Identifier] = new Net.Greeting
					{
						Wire = wire,
						Version = (text ?? ""),
						Refused = (text2 ?? "")
					};
				}
				return;
			}
			if (b == 4)
			{
				lock (Net.Hellos)
				{
					Net.Acks.Add(connection.Identifier);
				}
				return;
			}
			Player player = connection.Player as Player;
			if (b == 2)
			{
				uint entityId = 0U;
				s.SerializeBits(ref entityId, 32);
				if (player != null)
				{
					ServerArmor.OnEquip(player, entityId);
					return;
				}
			}
			else if (b == 3)
			{
				byte slot = 0;
				byte hand = 0;
				s.SerializeByte(ref slot);
				s.SerializeByte(ref hand);
				if (player != null)
				{
					ServerArmor.OnUnequip(player, (int)slot, (int)hand);
				}
			}
		}

		// Token: 0x0600005D RID: 93 RVA: 0x0000657C File Offset: 0x0000477C
		internal static void SendTable(Player to, List<KeyValuePair<int, int[]>> rows)
		{
			if (to == null || to.ConnectionToRemotePlayer == null)
			{
				return;
			}
			to.ConnectionToRemotePlayer.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 13;
				s.SerializeByte(ref b);
				int count = rows.Count;
				s.SerializeInteger(ref count);
				foreach (KeyValuePair<int, int[]> keyValuePair in rows)
				{
					int key = keyValuePair.Key;
					s.SerializeInteger(ref key);
					for (int i = 0; i < 8; i++)
					{
						byte b2 = (byte)(keyValuePair.Value[i] + 1);
						s.SerializeByte(ref b2);
						uint material = unchecked((uint)keyValuePair.Value[8 + i]);
						s.SerializeBits(ref material, 32);
					}
				}
			});
		}

		// Token: 0x0600005E RID: 94 RVA: 0x0000660C File Offset: 0x0000480C
		internal static void SendTook(Player to, uint entity, int hand)
		{
			if (to == null || to.ConnectionToRemotePlayer == null)
			{
				return;
			}
			to.ConnectionToRemotePlayer.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 11;
				byte b2 = (byte)hand;
				s.SerializeByte(ref b);
				uint entity2 = entity;
				s.SerializeBits(ref entity2, 32);
				s.SerializeByte(ref b2);
			});
		}

		// Token: 0x0600005F RID: 95 RVA: 0x00006690 File Offset: 0x00004890
		internal static void SendNote(Player to, string text)
		{
			if (to == null || to.ConnectionToRemotePlayer == null)
			{
				return;
			}
			to.ConnectionToRemotePlayer.Send(null, 137, delegate(Connection conn, Stream s)
			{
				byte b = 12;
				s.SerializeByte(ref b);
				string text2 = text;
				s.SerializeString(ref text2, 1);
			});
		}

		// Token: 0x06000060 RID: 96 RVA: 0x000066DC File Offset: 0x000048DC
		internal static void ClientRead(Connection connection, Stream s)
		{
			byte b = 0;
			s.SerializeByte(ref b);
			if (b == 20)
			{
				string version = null;
				int wire = 0;
				s.SerializeString(ref version, 1);
				s.SerializeInteger(ref wire);
				Gate.Open(connection, version, wire);
				return;
			}
			if (!Gate.IsOpen)
			{
				return;
			}
			if (b == 13)
			{
				int num = 0;
				s.SerializeInteger(ref num);
				Dictionary<int, int[]> dictionary = new Dictionary<int, int[]>();
				int num2 = 0;
				while (num2 < num && num2 < 1024)
				{
					int key = 0;
					s.SerializeInteger(ref key);
					int[] array = new int[16];
					for (int i = 0; i < 8; i++)
					{
						byte b2 = 0;
						s.SerializeByte(ref b2);
						array[i] = ((b2 == 0 || (int)b2 > Catalog.Count) ? -1 : ((int)(b2 - 1)));
						uint material = 0U;
						s.SerializeBits(ref material, 32);
						array[8 + i] = unchecked((int)material);
					}
					dictionary[key] = array;
					num2++;
				}
				ClientArmor.OnTable(dictionary);
				return;
			}
			if (b == 11)
			{
				uint id = 0U;
				byte hand = 0;
				s.SerializeBits(ref id, 32);
				s.SerializeByte(ref hand);
				ClientArmor.OnTook(id, (int)hand);
				return;
			}
			if (b == 12)
			{
				string str = null;
				s.SerializeString(ref str, 1);
				Mod.Log.Msg("server: " + str);
			}
		}

		// Token: 0x06000061 RID: 97 RVA: 0x000067F8 File Offset: 0x000049F8
		internal static void InstallJoinStep()
		{
			if (Net._stepInstalled)
			{
				return;
			}
			ServerHandler serverHandler = ServerHandler.Current;
			if (serverHandler == null)
			{
				Mod.Log.Warning("join step not installed: no ServerHandler yet, so no player gets armor");
				return;
			}
			Net._stepInstalled = true;
			serverHandler.AddConnectionStep(new ServerHandler.ConnectionStep(Net.Step));
			Mod.Log.Msg(string.Concat(new object[]
			{
				"join step installed on message ",
				137,
				": every joining player hears that armor is here; join check ",
				Settings.Join
			}));
		}

		// Token: 0x06000062 RID: 98 RVA: 0x00006F10 File Offset: 0x00005110
		private static async Task Step(Connection connection, Action<string> progress)
		{
			Net.SendHere(connection);
			if (!(Settings.Join == "off"))
			{
				Net.Greeting hello = null;
				bool acked = false;
				int waited = 0;
				while (!connection.IsDisposed)
				{
					lock (Net.Hellos)
					{
						Net.Hellos.TryGetValue(connection.Identifier, out hello);
						acked = (acked || Net.Acks.Contains(connection.Identifier));
					}
					if (hello != null || waited >= (acked ? 60000 : 5000))
					{
						break;
					}
					await Task.Delay(100);
					waited += 100;
				}
				lock (Net.Hellos)
				{
					Net.Hellos.Remove(connection.Identifier);
					Net.Acks.Remove(connection.Identifier);
				}
				string problem = null;
				if (hello == null && acked)
				{
					problem = "Your ArmorRestored did not finish loading in " + 60 + " s. Join again.";
				}
				else if (hello == null)
				{
					problem = "This server needs the ArmorRestored mod 0.3.0. Install it, then join again.";
				}
				else if (hello.Wire < 2)
				{
					problem = "Your ArmorRestored " + hello.Version + " is too old for this server's 0.3.0. Update it, then join again.";
				}
				else if (hello.Wire > 2)
				{
					problem = "Your ArmorRestored " + hello.Version + " is newer than this server's 0.3.0. The server needs an update first.";
				}
				else if (hello.Refused != Prefabs.RefusedLine())
				{
					problem = string.Concat(new string[]
					{
						"ArmorRestored loaded differently here (server refused: ",
						Net.Or(Prefabs.RefusedLine()),
						"; you refused: ",
						Net.Or(hello.Refused),
						"). Check both logs."
					});
				}
				string who = string.Concat(new object[]
				{
					connection.PlayerName,
					" (connection ",
					connection.Identifier,
					")"
				});
				if (problem == null)
				{
					Mod.Log.Msg(string.Concat(new object[]
					{
						"join check passed for ",
						who,
						" after ",
						waited,
						" ms (client ",
						hello.Version,
						", wire ",
						hello.Wire,
						")"
					}));
				}
				else
				{
					Mod.Log.Warning(string.Concat(new string[]
					{
						"join check: ",
						who,
						": ",
						problem,
						(Settings.Join == "refuse") ? " -> refused" : " (log only)"
					}));
					if (!(Settings.Join != "refuse"))
					{
						progress(Net.Ascii(problem));
						await Task.Delay(1500);
						connection.Send(null, 10, delegate(Connection c, Stream s)
						{
							bool flag3 = false;
							bool flag4 = false;
							int num = 0;
							string text = Net.Ascii(problem);
							s.SerializeBool(ref flag3);
							s.SerializeBool(ref flag4);
							s.SerializeInteger(ref num);
							s.SerializeString(ref text, 0);
						});
						await Task.Delay(500);
						throw new InvalidOperationException("ArmorRestored refused the join: " + problem);
					}
				}
			}
		}

		// Token: 0x06000063 RID: 99 RVA: 0x00006F5E File Offset: 0x0000515E
		private static string Or(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return s;
			}
			return "none";
		}

		// Token: 0x06000064 RID: 100 RVA: 0x00006F70 File Offset: 0x00005170
		private static string Ascii(string s)
		{
			StringBuilder stringBuilder = new StringBuilder(s.Length);
			foreach (char c in s)
			{
				stringBuilder.Append((c < '\u0080') ? c : '?');
			}
			return stringBuilder.ToString();
		}

		// Token: 0x0400004B RID: 75
		internal const MessageType Message = 137;

		// Token: 0x0400004C RID: 76
		internal const byte OldHello = 1;

		// Token: 0x0400004D RID: 77
		internal const byte Equip = 2;

		// Token: 0x0400004E RID: 78
		internal const byte Unequip = 3;

		// Token: 0x0400004F RID: 79
		internal const byte Ack = 4;

		// Token: 0x04000050 RID: 80
		internal const byte Hello = 5;

		// Token: 0x04000051 RID: 81
		internal const byte Table = 13;

		// Token: 0x04000052 RID: 82
		internal const byte Took = 11;

		// Token: 0x04000053 RID: 83
		internal const byte Note = 12;

		// Token: 0x04000054 RID: 84
		internal const byte Here = 20;

		// Token: 0x04000055 RID: 85
		internal const int Wire = 2;

		// Token: 0x04000056 RID: 86
		private const int AnswerWait = 5000;

		// Token: 0x04000057 RID: 87
		private const int LoadWait = 60000;

		// Token: 0x04000058 RID: 88
		internal static Connection ToServer;

		// Token: 0x04000059 RID: 89
		private static readonly Dictionary<int, Net.Greeting> Hellos = new Dictionary<int, Net.Greeting>();

		// Token: 0x0400005A RID: 90
		private static readonly HashSet<int> Acks = new HashSet<int>();

		// Token: 0x0400005B RID: 91
		private static bool _stepInstalled;

		// Token: 0x02000010 RID: 16
		private sealed class Greeting
		{
			// Token: 0x0400005F RID: 95
			public int Wire;

			// Token: 0x04000060 RID: 96
			public string Version;

			// Token: 0x04000061 RID: 97
			public string Refused;
		}
	}
}
