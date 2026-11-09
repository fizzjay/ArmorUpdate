using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppAlta.Networking;
using MelonLoader;
using MelonLoader.Utils;

namespace ArmorRestoredQuest
{
	// Token: 0x0200000A RID: 10
	public class Main : MelonMod
	{
		// Token: 0x06000043 RID: 67 RVA: 0x000048C0 File Offset: 0x00002AC0
		public override void OnInitializeMelon()
		{
			Mod.Log = base.LoggerInstance;
			Crumb.WatchExit();
			try
			{
				Mod.DataDir = Path.Combine(MelonEnvironment.UserDataDirectory, "ArmorRestored");
				Directory.CreateDirectory(Mod.DataDir);
				Settings.Load();
                EquipmentMenu.Install();
				Wire.Prepare();
				MethodInfo methodInfo = AccessTools.Method(typeof(JoinServerPipeline), "ConnectionApprovedByServer", null, null);
				if (methodInfo == null)
				{
					Mod.Log.Error("JoinServerPipeline.ConnectionApprovedByServer is missing; ArmorRestored stays off.");
				}
				else
				{
					new HarmonyLib.Harmony("armorrestored.client").Patch(methodInfo, null, new HarmonyMethod(typeof(Main), "ApprovedPostfix", null), null, null, null);
					this._running = true;
					Mod.Log.Msg(string.Concat(new string[]
					{
						"0.4.0 started: idle until a server says it runs ArmorRestored (own helmet ",
						Settings.OwnHelmet ? "shown" : "hidden",
						", debug ",
						Settings.Debug ? "on" : "off",
						")."
					}));
					Crumb.At("started 0.4.0");
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Instance log = Mod.Log;
				string str = "failed to start, staying off: ";
				Exception ex2 = ex;
				log.Error(str + ((ex2 != null) ? ex2.ToString() : null));
			}
		}

		// Token: 0x06000044 RID: 68 RVA: 0x00004A04 File Offset: 0x00002C04
		private static void ApprovedPostfix(Connection connection)
		{
			if (connection == null)
			{
				return;
			}
			try
			{
				Gate.Listen(connection);
			}
			catch (Exception ex)
			{
				MelonLogger.Instance log = Mod.Log;
				string str = "new server connection: ";
				Exception ex2 = ex;
				log.Error(str + ((ex2 != null) ? ex2.ToString() : null));
			}
		}

		// Token: 0x06000045 RID: 69 RVA: 0x00004A54 File Offset: 0x00002C54
		public override void OnUpdate()
		{
			if (!this._running)
			{
				return;
			}
			try
			{
				Gate.Tick();
                EquipmentMenu.Tick();
			}
			catch (Exception e)
			{
				this.Report(0, e);
			}
			try
			{
				Draw.Tick();
			}
			catch (Exception e2)
			{
				this.Report(1, e2);
			}
			try
			{
				Hands.Tick();
				ArmorStand.TickDiagnostics();
			}
			catch (Exception e3)
			{
				this.Report(2, e3);
			}
		}

		// Token: 0x06000046 RID: 70 RVA: 0x00004ACC File Offset: 0x00002CCC
		public override void OnSceneWasLoaded(int buildIndex, string sceneName)
		{
			if (!this._running)
			{
				return;
			}
			try
			{
				Gate.SceneLoaded();
			}
			catch (Exception e)
			{
				this.Report(0, e);
			}
		}

		// Token: 0x06000047 RID: 71 RVA: 0x00004B08 File Offset: 0x00002D08
		private void Report(int tick, Exception e)
		{
			string text = e.GetType().Name + ": " + e.Message;
			if (text == this._lastError[tick])
			{
				return;
			}
			this._lastError[tick] = text;
			Mod.Log.Error("tick threw: " + ((e != null) ? e.ToString() : null));
			Crumb.At("tick threw " + text);
		}

		// Token: 0x04000041 RID: 65
		private bool _running;

		// Token: 0x04000042 RID: 66
		private readonly string[] _lastError = new string[3];
	}
}
