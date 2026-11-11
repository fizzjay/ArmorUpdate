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
                ClientArmor.OnTable(new Dictionary<int,int[]>(completeRows));
            }
        }

		// Token: 0x06000057 RID: 87 RVA: 0x0000618D File Offset: 0x0000438D
		internal static void SendHere(Connection c)
		{
			c.Send(null, Net.Message, delegate(Connection conn, Stream s)
			{
				byte b = 20;
				s.SerializeByte(ref b);
				string text = "0.5.0";
				s.SerializeString(ref text, (Stream.StringEncoding)1);
				int num = 6;
				s.SerializeInteger(ref num);
			});
		}

		// Token: 0x06000058 RID: 88 RVA: 0x000061FB File Offset: 0x000043FB
		internal static void SendHello(Connection c)
		{
			c.Send(null, Net.Message, delegate(Connection conn, Stream s)
			{
				byte b = 5;
				s.SerializeByte(ref b);
				int num = 6;
				s.SerializeInteger(ref num);
				string text = "0.5.0";
				string text2 = BoundedText(Prefabs.RefusedLine(),160);
				s.SerializeString(ref text, (Stream.StringEncoding)1);
				s.SerializeString(ref text2, (Stream.StringEncoding)1);
                string signature = BoundedText(ArmorCrafting.Signature,256);
                s.SerializeString(ref signature, (Stream.StringEncoding)1);
			});
		}

		// Token: 0x06000059 RID: 89 RVA: 0x0000623F File Offset: 0x0000443F
		internal static void SendAck(Connection c)
		{
			c.Send(null, Net.Message, delegate(Connection conn, Stream s)
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
			toServer.Send(null, Net.Message, delegate(Connection conn, Stream s)
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
			toServer.Send(null, Net.Message, delegate(Connection conn, Stream s)
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
				s.SerializeString(ref text, (Stream.StringEncoding)1);
				s.SerializeString(ref text2, (Stream.StringEncoding)1);
                string signature = "";
                if (wire >= 4) s.SerializeString(ref signature, (Stream.StringEncoding)1);
				lock (Net.Hellos)
				{
					Net.Hellos[connection.Identifier] = new Net.Greeting
					{
						Wire = wire,
						Version = (text ?? ""),
						Refused = (text2 ?? ""),
                        Signature = signature ?? ""
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
        private sealed class RecipientState
        {
            internal Connection Connection;
            internal Dictionary<int,int[]> Rows=new Dictionary<int,int[]>();
            internal float FullAt;
        }
        private static readonly Dictionary<int,RecipientState> recipients=new Dictionary<int,RecipientState>();
        internal static bool NeedsTable(Player player)
        {
            if(player==null || player.PlayerCharacter==null || player.ConnectionToRemotePlayer==null) return false;
            var connection=player.ConnectionToRemotePlayer;RecipientState state;
            return !recipients.TryGetValue(connection.Identifier,out state) || !object.ReferenceEquals(state.Connection,connection) || UnityEngine.Time.unscaledTime>=state.FullAt;
        }
        internal static void PruneTables(List<Player> players)
        {
            var live=new HashSet<int>();
            foreach(var player in players) if(player.ConnectionToRemotePlayer!=null) live.Add(player.ConnectionToRemotePlayer.Identifier);
            foreach(var key in new List<int>(recipients.Keys)) if(!live.Contains(key)) recipients.Remove(key);
        }
        private static bool EqualRow(int[] left,int[] right)
        {if(left==null || right==null || left.Length!=right.Length) return false;for(int i=0;i<left.Length;i++) if(left[i]!=right[i]) return false;return true;}
        private static uint nextSnapshot;
        internal static void SendTable(Player to,List<KeyValuePair<int,int[]>> rows)
        {
            if(to==null || to.PlayerCharacter==null || to.ConnectionToRemotePlayer==null) return;
            if(rows.Count>1024) throw new ArgumentException("Too many armor rows");
            var connection=to.ConnectionToRemotePlayer;RecipientState state;
            bool fresh=!recipients.TryGetValue(connection.Identifier,out state) || !object.ReferenceEquals(state.Connection,connection);
            if(fresh) {state=new RecipientState {Connection=connection};recipients[connection.Identifier]=state;}
            bool full=fresh || UnityEngine.Time.unscaledTime>=state.FullAt;
            if(!full)
            {
                var currentIds=new HashSet<int>();foreach(var row in rows) currentIds.Add(row.Key);
                foreach(var previous in state.Rows.Keys) if(!currentIds.Contains(previous)) {full=true;break;}
            }
            var selected=new List<KeyValuePair<int,int[]>>();
            foreach(var row in rows)
            {
                int[] previous;
                if(full || !state.Rows.TryGetValue(row.Key,out previous) || !EqualRow(previous,row.Value)) selected.Add(row);
            }
            if(!full && selected.Count==0) return;
            var current=new Dictionary<int,int[]>();
            foreach(var row in rows) current[row.Key]=(int[])row.Value.Clone();
            bool sent=true;
            uint snapshot=++nextSnapshot;
            int chunks=Math.Max(1,selected.Count*2);
            for(int part=0;part<chunks;part++)
            {
                int index=part;
                // Sparse palettes: 58 bytes for four uniform slots; at most
                // 570 bytes when all 128 material overrides differ.
                sent=connection.Send(null,Net.Message,delegate(Connection conn,Stream stream)
                {
                    byte kind=14;stream.SerializeByte(ref kind);
                    uint id=snapshot,total=(uint)chunks,chunk=(uint)index,count=(uint)selected.Count;
                    stream.SerializeBits(ref id,32);stream.SerializeBits(ref total,32);
                    stream.SerializeBits(ref chunk,32);stream.SerializeBits(ref count,32);
                    uint mode=full ? 1U : 0U;stream.SerializeBits(ref mode,8);
                    if(selected.Count==0) return;
                    var row=selected[index/2];uint key=unchecked((uint)row.Key);stream.SerializeBits(ref key,32);
                    for(int slot=(index%2)*4;slot<(index%2)*4+4;slot++)
                    {
                        uint piece=(uint)(row.Value[slot]+1);stream.SerializeBits(ref piece,8);
                        uint material=unchecked((uint)row.Value[8+slot]);stream.SerializeBits(ref material,32);
                        uint mask=0;
                        for(int group=0;group<32;group++) if(unchecked((uint)row.Value[16+slot*32+group])!=material) mask|=1U<<group;
                        stream.SerializeBits(ref mask,32);
                        for(int group=0;group<32;group++) if((mask&(1U<<group))!=0)
                        {uint input=unchecked((uint)row.Value[16+slot*32+group]);stream.SerializeBits(ref input,32);}
                    }
                }) && sent;
            }
            if(sent) {state.Rows=current;if(full) state.FullAt=UnityEngine.Time.unscaledTime+30f;}
            else {state.Rows.Clear();state.FullAt=0f;}
        }

		// Token: 0x0600005E RID: 94 RVA: 0x0000660C File Offset: 0x0000480C
		internal static void SendTook(Player to, uint entity, int hand)
		{
			if (to == null || to.ConnectionToRemotePlayer == null)
			{
				return;
			}
			to.ConnectionToRemotePlayer.Send(null, Net.Message, delegate(Connection conn, Stream s)
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
			to.ConnectionToRemotePlayer.Send(null, Net.Message, delegate(Connection conn, Stream s)
			{
				byte b = 12;
				s.SerializeByte(ref b);
				string text2 = BoundedText(text,160);
				s.SerializeString(ref text2, (Stream.StringEncoding)1);
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
				s.SerializeString(ref version, (Stream.StringEncoding)1);
				s.SerializeInteger(ref wire);
				Gate.Open(connection, version, wire);
				return;
			}
			if (!Gate.IsOpen)
			{
				return;
			}
            if(b==14) {ReadSnapshot(s);return;}
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
				s.SerializeString(ref str, (Stream.StringEncoding)1);
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
			if (!(Settings.Join == "off") || ArmorCrafting.Ready)
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
					problem = "This server needs the ArmorRestored mod 0.5.0. Install it, then join again.";
				}
				else if (hello.Wire < 6)
				{
					problem = "Your ArmorRestored " + hello.Version + " is too old for this server's 0.5.0. Update it, then join again.";
				}
				else if (hello.Wire > 6)
				{
					problem = "Your ArmorRestored " + hello.Version + " is newer than this server's 0.5.0. The server needs an update first.";
				}
                else if (ArmorCrafting.Ready && string.IsNullOrEmpty(hello.Signature))
                {
                    problem = "Client armor crafting registration failed. Check the client log for armor crafting disabled at; join is blocked to protect native recipes.";
                }
				else if (hello.Signature != ArmorCrafting.Signature)
                {
                    problem = "Armor construction data differs between server and client. Install the same ArmorRestored build.";
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
					if (!(Settings.Join != "refuse") || ArmorCrafting.Ready)
					{
						progress(Net.Ascii(problem));
						await Task.Delay(1500);
						connection.Send(null, (MessageType)10, delegate(Connection c, Stream s)
						{
							bool flag3 = false;
							bool flag4 = false;
							int num = 0;
							string text = BoundedText(Net.Ascii(problem),160);
							s.SerializeBool(ref flag3);
							s.SerializeBool(ref flag4);
							s.SerializeInteger(ref num);
							s.SerializeString(ref text, (Stream.StringEncoding)0);
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
		internal const MessageType Message = (MessageType)137;

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
		internal const byte Table = 14;

		// Token: 0x04000052 RID: 82
		internal const byte Took = 11;

		// Token: 0x04000053 RID: 83
		internal const byte Note = 12;

		// Token: 0x04000054 RID: 84
		internal const byte Here = 20;

		// Token: 0x04000055 RID: 85
		internal const int Wire = 6;

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
            public string Signature;
		}
	}
}
