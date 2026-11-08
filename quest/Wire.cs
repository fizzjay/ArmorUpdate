using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppAlta.Global;
using Il2CppAlta.Networking;
using Il2CppAlta.Serialization;

namespace ArmorRestoredQuest
{
	// Token: 0x02000015 RID: 21
	internal static class Wire
	{
		// Token: 0x06000070 RID: 112 RVA: 0x00007974 File Offset: 0x00005B74
		internal static void Prepare()
		{
			Wire._handler = new Action<Connection, Stream>(Wire.Receive);
			Wire._onDisconnected = new Action<Connection>(Gate.Disconnected);
		}

		// Token: 0x06000071 RID: 113 RVA: 0x000079CB File Offset: 0x00005BCB
		internal static void Listen(Connection c)
		{
			Crumb.At("SetHandler 137");
			c.SetHandler(137, Wire._handler);
			Crumb.At("add_Disconnected");
			c.Disconnected += Wire._onDisconnected;
		}

		// Token: 0x06000072 RID: 114 RVA: 0x000079FC File Offset: 0x00005BFC
		private static void Receive(Connection c, Stream s)
		{
			try
			{
				if (Wire._reads < 3)
				{
					string str = "read 137 #";
					int num = ++Wire._reads;
					Crumb.At(str + num.ToString());
				}
				uint num2 = 0U;
				s.SerializeBits(ref num2, 8);
				if (num2 == 20U)
				{
					string version = null;
					int wire = 0;
					s.SerializeString(ref version, 1);
					s.SerializeInteger(ref wire);
					Gate.Heard(c, version, wire);
				}
				else if (num2 == 13U && Gate.IsOpen)
				{
					int num3 = 0;
					s.SerializeInteger(ref num3);
					Dictionary<int, int[]> dictionary = new Dictionary<int, int[]>();
					int num4 = 0;
					while (num4 < num3 && num4 < 1024)
					{
						int key = 0;
						s.SerializeInteger(ref key);
						int[] array = new int[16];
						for (int i = 0; i < 8; i++)
						{
							uint num5 = 0U;
							s.SerializeBits(ref num5, 8);
							array[i] = (int)((num5 == 0U || num5 > 24U) ? uint.MaxValue : (num5 - 1U));
							uint material = 0U;
							s.SerializeBits(ref material, 32);
							array[8 + i] = unchecked((int)material);
						}
						dictionary[key] = array;
						num4++;
					}
					Draw.OnTable(dictionary);
				}
				else if (num2 == 11U)
				{
					uint id = 0U;
					uint hand = 0U;
					s.SerializeBits(ref id, 32);
					s.SerializeBits(ref hand, 8);
					Hands.OnTook(id, (int)hand);
				}
				else if (num2 == 12U)
				{
					string str2 = null;
					s.SerializeString(ref str2, 1);
					Mod.Log.Msg("server: " + str2);
				}
				else
				{
					Mod.Log.Warning("message 137 of an unknown kind " + num2.ToString() + " ignored.");
				}
			}
			catch (Exception ex)
			{
				Mod.Log.Error("reading message 137 failed: " + ex.Message);
			}
		}

		// Token: 0x06000073 RID: 115 RVA: 0x00007BA8 File Offset: 0x00005DA8
		internal static bool SendAck(Connection c)
		{
			return Wire.Send(c, "ack", delegate(Stream s)
			{
				uint num = 4U;
				s.SerializeBits(ref num, 8);
			});
		}

		// Token: 0x06000074 RID: 116 RVA: 0x00007BD4 File Offset: 0x00005DD4
		internal static bool SendHello(Connection c, string version, string refused)
		{
			return Wire.Send(c, "hello", delegate(Stream s)
			{
				uint num = 5U;
				s.SerializeBits(ref num, 8);
				int num2 = 2;
				s.SerializeInteger(ref num2);
				string version2 = version;
				string refused2 = refused;
				s.SerializeString(ref version2, 1);
				s.SerializeString(ref refused2, 1);
			});
		}

		// Token: 0x06000075 RID: 117 RVA: 0x00007C0C File Offset: 0x00005E0C
		internal static bool SendEquip(uint entity)
		{
			return Wire.Send(Gate.Server, "equip", delegate(Stream s)
			{
				uint num = 2U;
				uint entity2 = entity;
				s.SerializeBits(ref num, 8);
				s.SerializeBits(ref entity2, 32);
			});
		}

		// Token: 0x06000076 RID: 118 RVA: 0x00007C44 File Offset: 0x00005E44
		internal static bool SendUnequip(int slot, int hand)
		{
			return Wire.Send(Gate.Server, "unequip", delegate(Stream s)
			{
				uint num = 3U;
				uint slot2 = (uint)slot;
				uint hand2 = (uint)hand;
				s.SerializeBits(ref num, 8);
				s.SerializeBits(ref slot2, 8);
				s.SerializeBits(ref hand2, 8);
			});
		}

		// Token: 0x06000077 RID: 119 RVA: 0x00007C80 File Offset: 0x00005E80
		private static bool Send(Connection c, string what, Action<Stream> body)
		{
			if (c == null)
			{
				return false;
			}
			Wire.ResolveChannel();
			Crumb.First("send " + what);
			Wire._last = delegate(Connection conn, Stream s)
			{
				body(s);
			};
			bool flag = c.Send(Wire._channel, 137, Wire._last);
			if (!flag)
			{
				Crumb.At("send " + what + " returned false");
			}
			return flag;
		}

		// Token: 0x06000078 RID: 120 RVA: 0x00007CF8 File Offset: 0x00005EF8
		private static void ResolveChannel()
		{
			if (Wire._channelResolved)
			{
				return;
			}
			Wire._channelResolved = true;
			try
			{
				ConnectionChannel connectionChannel = null;
				foreach (ConnectionChannel connectionChannel2 in GlobalSettings<NetworkingSettings>.Instance.Channels)
				{
					if (!(connectionChannel2 == null) && connectionChannel2.IsReliable && (connectionChannel == null || connectionChannel2.MaximumSendInterval < connectionChannel.MaximumSendInterval))
					{
						connectionChannel = connectionChannel2;
					}
				}
				Wire._channel = connectionChannel;
				if (connectionChannel == null)
				{
					Mod.Log.Warning("no reliable channel found; using the connection's default.");
				}
			}
			catch (Exception ex)
			{
				Mod.Log.Warning("channel lookup failed, using the connection's default: " + ex.Message);
			}
		}

		// Token: 0x04000092 RID: 146
		internal const int Type = 137;

		// Token: 0x04000093 RID: 147
		internal const int Number = 1;

		// Token: 0x04000094 RID: 148
		private const uint Equip = 2U;

		// Token: 0x04000095 RID: 149
		private const uint Unequip = 3U;

		// Token: 0x04000096 RID: 150
		private const uint Ack = 4U;

		// Token: 0x04000097 RID: 151
		private const uint Hello = 5U;

		// Token: 0x04000098 RID: 152
		private const uint Table = 13U;

		// Token: 0x04000099 RID: 153
		private const uint Took = 11U;

		// Token: 0x0400009A RID: 154
		private const uint Note = 12U;

		// Token: 0x0400009B RID: 155
		private const uint Here = 20U;

		// Token: 0x0400009C RID: 156
		private static SerializeConnectionMethod _handler;

		// Token: 0x0400009D RID: 157
		private static ConnectionEventHandler _onDisconnected;

		// Token: 0x0400009E RID: 158
		private static SerializeConnectionMethod _last;

		// Token: 0x0400009F RID: 159
		private static ConnectionChannel _channel;

		// Token: 0x040000A0 RID: 160
		private static bool _channelResolved;

		// Token: 0x040000A1 RID: 161
		private static int _reads;

	}
}
