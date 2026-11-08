using System;
using System.IO;
using Alta.Console;
using MelonLoader;
using MelonLoader.Utils;

namespace ArmorRestored
{
	// Token: 0x0200000E RID: 14
	public class Main : MelonMod
	{
		// Token: 0x06000050 RID: 80 RVA: 0x00005EE4 File Offset: 0x000040E4
		public override void OnInitializeMelon()
		{
			Mod.Log = base.LoggerInstance;
			Mod.IsServer = Main.IsServer();
			Mod.DataDir = Path.Combine(MelonEnvironment.UserDataDirectory, "ArmorRestored");
			try
			{
				Directory.CreateDirectory(Mod.DataDir);
				Settings.Load();
				if (Mod.IsServer)
				{
					Pack.Load(false);
					Catalog.Check();
					ServerArmor.Load();
				}
				this._running = true;
				if (Mod.IsServer)
				{
					Mod.Log.Msg(string.Concat(new object[]
					{
						"0.3.0 started on the server: ",
						Pack.Pieces.Count,
						" pieces, join check ",
						Settings.Join,
						", loot ",
						Settings.Loot ? "on" : "off",
						"."
					}));
				}
				else
				{
					Mod.Log.Msg("0.3.0 started on the game client: idle until a server says it runs ArmorRestored.");
				}
			}
			catch (Exception arg)
			{
				Mod.Log.Error("failed to start, staying off: " + arg);
			}
		}

		// Token: 0x06000051 RID: 81 RVA: 0x00005FF4 File Offset: 0x000041F4
		public override void OnUpdate()
		{
			if (!this._running)
			{
				return;
			}
			try
			{
				if (Mod.IsServer)
				{
					ServerArmor.Tick();
				}
				else
				{
					Gate.Tick();
					ClientArmor.Tick();
				}
			}
			catch (Exception e)
			{
				this.Report(e);
			}
		}

		// Token: 0x06000052 RID: 82 RVA: 0x00006040 File Offset: 0x00004240
		public override void OnSceneWasLoaded(int buildIndex, string sceneName)
		{
			if (!this._running || Mod.IsServer)
			{
				return;
			}
			try
			{
				Gate.SceneLoaded(sceneName);
			}
			catch (Exception e)
			{
				this.Report(e);
			}
		}

		// Token: 0x06000053 RID: 83 RVA: 0x00006080 File Offset: 0x00004280
		private void Report(Exception e)
		{
			string text = e.GetType().Name + ": " + e.Message;
			if (text == this._lastTickError)
			{
				return;
			}
			this._lastTickError = text;
			Mod.Log.Error("tick threw: " + e);
		}

		// Token: 0x06000054 RID: 84 RVA: 0x000060D4 File Offset: 0x000042D4
		internal static void RegisterCommands()
		{
			try
			{
				CommandService.CommandCollection.Collect(typeof(Main).Assembly);
				Mod.Log.Msg("console commands registered: armor.");
			}
			catch (Exception ex)
			{
				Mod.Log.Error("console command registration failed: " + ex.Message);
			}
		}

		// Token: 0x06000055 RID: 85 RVA: 0x0000613C File Offset: 0x0000433C
		private static bool IsServer()
		{
			return ApplicationManager.IsHeadless || CommandLineArguments.Contains("/start_server");
		}

		// Token: 0x04000049 RID: 73
		private bool _running;

		// Token: 0x0400004A RID: 74
		private string _lastTickError;
	}
}
