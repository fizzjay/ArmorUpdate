using System;
using Il2CppAlta.Networking;
using MelonLoader;
using UnityEngine;

namespace ArmorRestoredQuest
{
	// Token: 0x02000007 RID: 7
	internal static class Gate
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000026 RID: 38 RVA: 0x000037EF File Offset: 0x000019EF
		internal static Connection Server
		{
			get
			{
				if (!Gate.IsOpen)
				{
					return null;
				}
				return Gate._listening;
			}
		}

		// Token: 0x06000027 RID: 39 RVA: 0x00003800 File Offset: 0x00001A00
		internal static void Listen(Connection c)
		{
			if (Gate.IsOpen)
			{
				Gate.Close("a new server connection");
			}
			Gate.Unregister();
			Gate._listening = c;
			Gate._listeningPtr = c.Pointer;
			Gate._heardPending = (Gate._leftPending = false);
			Wire.Listen(c);
			if (Settings.Debug)
			{
				Mod.Log.Msg("new server connection: listening on message " + 137.ToString() + ", idle until the server says ArmorRestored is here.");
			}
		}

		// Token: 0x06000028 RID: 40 RVA: 0x00003873 File Offset: 0x00001A73
		internal static void Heard(Connection c, string version, int wire)
		{
			if (c == null || c.Pointer != Gate._listeningPtr || Gate.IsOpen)
			{
				return;
			}
			Gate._heard = (version ?? "");
			Gate._heardWire = wire;
			Gate._heardPending = true;
		}

		// Token: 0x06000029 RID: 41 RVA: 0x000038A8 File Offset: 0x00001AA8
		internal static void Disconnected(Connection c)
		{
			if (c != null && c.Pointer == Gate._listeningPtr)
			{
				Gate._leftPending = true;
			}
		}

		// Token: 0x0600002A RID: 42 RVA: 0x000038C0 File Offset: 0x00001AC0
		internal static void Tick()
		{
			if (Gate._listening != null && Gate._listening.IsDisposed)
			{
				Gate._leftPending = true;
			}
			if (Gate._leftPending)
			{
				Gate._leftPending = false;
				Gate.Close("left the server");
			}
			if (Gate._heardPending)
			{
				Gate._heardPending = false;
				Gate.Open();
			}
			if (Gate._loading && Time.frameCount >= Gate._loadFrame && Time.unscaledTime >= Gate._loadTime)
			{
				Gate.Load();
			}
		}

		// Token: 0x0600002B RID: 43 RVA: 0x00003934 File Offset: 0x00001B34
		private static void Open()
		{
			if (Gate._listening == null)
			{
				return;
			}
			if (Gate._heardWire != 3)
			{
				Mod.Log.Warning("this server runs ArmorRestored " + Gate._heard + ", which does not play with 0.4.0; the server decides and armor stays off.");
				Crumb.At("gate stays shut, server " + Gate._heard + " wire " + Gate._heardWire.ToString());
				Wire.SendHello(Gate._listening, "0.4.0", "not registered");
				return;
			}
			Gate.IsOpen = true;
			Mod.Log.Msg("this server runs ArmorRestored " + Gate._heard + ": armor is on until you leave.");
			Crumb.At("gate open, server " + Gate._heard);
			Wire.SendAck(Gate._listening);
			Gate._loadFrame = Time.frameCount + 2;
			Gate._loadTime = Time.unscaledTime + 0.2f;
			Gate._loading = true;
		}

		// Token: 0x0600002C RID: 44 RVA: 0x00003A10 File Offset: 0x00001C10
		private static void Load()
		{
			Gate._loading = false;
			if (!Gate.IsOpen || Gate._listening == null)
			{
				return;
			}
			if (Gate._listening.IsDisposed)
			{
				Gate._leftPending = true;
				return;
			}
			string text = Registry.Register();
			bool flag = Wire.SendHello(Gate._listening, "0.4.0", text);
			Mod.Log.Msg(string.Concat(new string[]
			{
				"hello ",
				flag ? "sent" : "NOT sent",
				" on message ",
				137.ToString(),
				"; refused: ",
				(text.Length == 0) ? "none" : text
			}));
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00003AC0 File Offset: 0x00001CC0
		internal static void Close(string why)
		{
			bool isOpen = Gate.IsOpen;
			Gate.IsOpen = false;
			Gate._loading = false;
			Gate._heardPending = false;
			Gate._listening = null;
			Gate._listeningPtr = IntPtr.Zero;
			Hands.Clear();
			Draw.Clear();
			Gate._clearPending = Registry.Done;
			if (isOpen)
			{
				Mod.Log.Msg("armor off: " + why + ".");
			}
			Crumb.At("gate closed: " + why);
		}

		// Token: 0x0600002E RID: 46 RVA: 0x00003B34 File Offset: 0x00001D34
		internal static void SceneLoaded()
		{
			if (!Gate.IsOpen && Gate._listening == null)
			{
				Gate.Unregister();
			}
		}

		// Token: 0x0600002F RID: 47 RVA: 0x00003B4C File Offset: 0x00001D4C
		private static void Unregister()
		{
			if (!Gate._clearPending && !Registry.Done)
			{
				return;
			}
			Gate._clearPending = false;
			try
			{
				Mod.Log.Msg(Registry.Unregister());
			}
			catch (Exception ex)
			{
				MelonLogger.Instance log = Mod.Log;
				string str = "could not remove the armor pieces: ";
				Exception ex2 = ex;
				log.Error(str + ((ex2 != null) ? ex2.ToString() : null));
			}
		}

		// Token: 0x04000021 RID: 33
		internal static bool IsOpen;

		// Token: 0x04000022 RID: 34
		private static Connection _listening;

		// Token: 0x04000023 RID: 35
		private static IntPtr _listeningPtr;

		// Token: 0x04000024 RID: 36
		private static string _heard;

		// Token: 0x04000025 RID: 37
		private static int _heardWire;

		// Token: 0x04000026 RID: 38
		private static bool _heardPending;

		// Token: 0x04000027 RID: 39
		private static bool _leftPending;

		// Token: 0x04000028 RID: 40
		private static bool _loading;

		// Token: 0x04000029 RID: 41
		private static bool _clearPending;

		// Token: 0x0400002A RID: 42
		private static int _loadFrame;

		// Token: 0x0400002B RID: 43
		private static float _loadTime;
	}
}
