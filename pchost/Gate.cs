using System;
using Alta.Networking;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x0200000B RID: 11
	internal static class Gate
	{
		// Token: 0x06000044 RID: 68 RVA: 0x00005370 File Offset: 0x00003570
		internal static void Listen(Connection connection)
		{
            Net.ResetSnapshot();
			if (Gate.IsOpen)
			{
				Gate.Close("a new server connection");
			}
			Gate.Unregister();
			Gate._listening = connection;
			Net.ToServer = connection;
			connection.SetHandler(Net.Message, new SerializeConnectionMethod(Net.ClientRead));
			connection.Disconnected += new ConnectionEventHandler(Gate.Disconnected);
		}

		// Token: 0x06000045 RID: 69 RVA: 0x000053C8 File Offset: 0x000035C8
		private static void Disconnected(Connection connection)
		{
			if (object.ReferenceEquals(connection, Gate._listening))
			{
				Gate.Close("left the server");
			}
		}

		// Token: 0x06000046 RID: 70 RVA: 0x000053E4 File Offset: 0x000035E4
		internal static void Open(Connection connection, string version, int wire)
		{
			if (Gate.IsOpen || !object.ReferenceEquals(connection, Gate._listening))
			{
				return;
			}
			Gate.ServerVersion = (version ?? "");
			if (wire != 6)
			{
				Mod.Log.Warning("this server runs ArmorRestored " + Gate.ServerVersion + ", which does not play with 0.5.0; the server decides and armor stays off.");
				Net.SendHello(connection);
				return;
			}
			Gate.IsOpen = true;
			Mod.Log.Msg("this server runs ArmorRestored " + Gate.ServerVersion + ": armor is on until you leave.");
			Net.SendAck(connection);
			Gate._loadFrame = Time.frameCount + 2;
			Gate._loadTime = Time.unscaledTime + 0.2f;
			Gate._loading = true;
		}

		// Token: 0x06000047 RID: 71 RVA: 0x0000548C File Offset: 0x0000368C
		private static void Load()
		{
			Gate._loading = false;
			Connection listening = Gate._listening;
			if (!Gate.IsOpen || listening == null || listening.IsDisposed)
			{
				return;
			}
			if (!Pack.Loaded)
			{
				Pack.Load(true);
				Catalog.Check();
			}
			Prefabs.Register("announce");
			Net.SendHello(listening);
			Mod.Log.Msg(string.Concat(new object[]
			{
				"hello sent on message ",
				137,
				"; refused: ",
				(Prefabs.RefusedLine().Length == 0) ? "none" : Prefabs.RefusedLine()
			}));
		}

		// Token: 0x06000048 RID: 72 RVA: 0x0000552C File Offset: 0x0000372C
		internal static void Close(string why)
		{
            Net.ResetSnapshot();
			bool isOpen = Gate.IsOpen;
			Gate.IsOpen = false;
			Gate._loading = false;
			Gate._listening = null;
			Net.ToServer = null;
			ClientArmor.Clear();
			Gate._clearPending = Prefabs.Done;
			if (isOpen)
			{
				Mod.Log.Msg("armor off: " + why + ".");
			}
		}

		// Token: 0x06000049 RID: 73 RVA: 0x00005584 File Offset: 0x00003784
		internal static void Tick()
		{
			if (Gate._listening != null && Gate._listening.IsDisposed)
			{
				Gate.Close("left the server");
			}
			if (Gate._loading && Time.frameCount >= Gate._loadFrame && Time.unscaledTime >= Gate._loadTime)
			{
				Gate.Load();
			}
		}

		// Token: 0x0600004A RID: 74 RVA: 0x000055D2 File Offset: 0x000037D2
		internal static void SceneLoaded(string scene)
		{
			if (!Gate.IsOpen && Gate._listening == null)
			{
				Gate.Unregister();
			}
		}

		// Token: 0x0600004B RID: 75 RVA: 0x000055E8 File Offset: 0x000037E8
		private static void Unregister()
		{
			if (!Gate._clearPending && !Prefabs.Done)
			{
				return;
			}
			Gate._clearPending = false;
			try
			{
				Mod.Log.Msg(Prefabs.Unregister());
			}
			catch (Exception arg)
			{
				Mod.Log.Error("could not remove the armor pieces: " + arg);
			}
		}

		// Token: 0x0400003B RID: 59
		internal static bool IsOpen;

		// Token: 0x0400003C RID: 60
		internal static string ServerVersion;

		// Token: 0x0400003D RID: 61
		private static bool _clearPending;

		// Token: 0x0400003E RID: 62
		private static Connection _listening;

		// Token: 0x0400003F RID: 63
		private static bool _loading;

		// Token: 0x04000040 RID: 64
		private static int _loadFrame;

		// Token: 0x04000041 RID: 65
		private static float _loadTime;
	}
}
