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
        private static string BoundedText(string value,int limit)
        {
            value=value ?? "";
            int length=Math.Min(value.Length,limit);
            var result=new char[length];
            for(int i=0;i<length;i++) result[i]=value[i]<128 ? value[i] : '?';
            return new string(result);
        }
        private static bool incomingFull;
        private static readonly Dictionary<int,int[]> completeRows=new Dictionary<int,int[]>();
        private static uint incomingSnapshot;
        private static int incomingCount;
        private static float incomingAt;
        private static readonly Dictionary<int,int[]> incomingRows=new Dictionary<int,int[]>();
        private static readonly HashSet<int> incomingParts=new HashSet<int>();
        internal static void ResetSnapshot()
        {incomingSnapshot=0;incomingCount=0;incomingRows.Clear();incomingParts.Clear();incomingAt=0f;completeRows.Clear();}
        private static void ReadSnapshot(Stream stream)
        {
            uint id=0,total=0,chunk=0,count=0;
            stream.SerializeBits(ref id,32);stream.SerializeBits(ref total,32);
            stream.SerializeBits(ref chunk,32);stream.SerializeBits(ref count,32);
            uint mode=0;stream.SerializeBits(ref mode,8);
            if(mode>1) throw new InvalidOperationException("Invalid armor snapshot mode");
            if(count>1024 || total!=Math.Max(1,(int)count*2) || chunk>=total)
                throw new InvalidOperationException("Invalid armor snapshot header");
            float now=UnityEngine.Time.unscaledTime;
            if(incomingCount!=0 && id!=incomingSnapshot && unchecked((int)(id-incomingSnapshot))<=0) return;
            if(incomingCount==0 || id!=incomingSnapshot || now-incomingAt>10f)
            {
                incomingSnapshot=id;incomingCount=(int)total;incomingAt=now;incomingFull=mode==1;
                incomingRows.Clear();incomingParts.Clear();
            }
            if(incomingCount!=(int)total || incomingFull!=(mode==1)) throw new InvalidOperationException("Armor snapshot size changed");
            if(incomingParts.Contains((int)chunk)) return;
            if(count>0)
            {
                uint key=0;stream.SerializeBits(ref key,32);
                int[] row;
                if(!incomingRows.TryGetValue(unchecked((int)key),out row))
                {row=new int[272];for(int i=0;i<8;i++) row[i]=-1;incomingRows[unchecked((int)key)]=row;}
                int start=((int)chunk%2)*4;
                for(int slot=start;slot<start+4;slot++)
                {
                    uint piece=0,material=0;stream.SerializeBits(ref piece,8);stream.SerializeBits(ref material,32);
                    row[slot]=piece==0 || piece>Catalog.Count ? -1 : (int)piece-1;
                    row[8+slot]=unchecked((int)material);
                    uint mask=0;stream.SerializeBits(ref mask,32);
                    for(int group=0;group<32;group++)
                    {uint input=material;if((mask&(1U<<group))!=0) stream.SerializeBits(ref input,32);row[16+slot*32+group]=unchecked((int)input);}
                }
            }
            incomingParts.Add((int)chunk);
            if(incomingParts.Count==incomingCount)
            {
                if(incomingRows.Count!=(int)count) throw new InvalidOperationException("Incomplete armor snapshot rows");
                if(incomingFull) completeRows.Clear();
                foreach(var pair in incomingRows) completeRows[pair.Key]=pair.Value;
                Draw.OnTable(new Dictionary<int,int[]>(completeRows));
            }
        }

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
			c.SetHandler((MessageType)137, Wire._handler);
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
					s.SerializeString(ref version, (Stream.StringEncoding)1);
					s.SerializeInteger(ref wire);
					Gate.Heard(c, version, wire);
				}
                else if(num2==14U && Gate.IsOpen) {ReadSnapshot(s);}
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
					s.SerializeString(ref str2, (Stream.StringEncoding)1);
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
				int num2 = 6;
				s.SerializeInteger(ref num2);
				string version2 = BoundedText(version,32);
				string refused2 = BoundedText(refused,160);
				s.SerializeString(ref version2, (Stream.StringEncoding)1);
				s.SerializeString(ref refused2, (Stream.StringEncoding)1);
                string signature = BoundedText(ArmorCrafting.Signature,256);
                s.SerializeString(ref signature, (Stream.StringEncoding)1);
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
			// IL2CPP delegates are wrapper classes: convert a managed Action explicitly.
			Wire._last = new Action<Connection, Stream>((conn, s) => body(s));
			bool flag = c.Send(Wire._channel, (MessageType)137, Wire._last);
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
		internal const int Number = 6;

		// Token: 0x04000094 RID: 148
		private const uint Equip = 2U;

		// Token: 0x04000095 RID: 149
		private const uint Unequip = 3U;

		// Token: 0x04000096 RID: 150
		private const uint Ack = 4U;

		// Token: 0x04000097 RID: 151
		private const uint Hello = 5U;

		// Token: 0x04000098 RID: 152
		private const uint Table = 14U;

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
