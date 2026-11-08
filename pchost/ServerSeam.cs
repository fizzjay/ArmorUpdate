using System;
using Alta.Networking;
using HarmonyLib;

namespace ArmorRestored
{
	// Token: 0x0200001E RID: 30
	[HarmonyPatch(typeof(NetworkScene), "FirstInitialize", new Type[]
	{
		typeof(bool)
	})]
	internal static class ServerSeam
	{
		// Token: 0x0600008E RID: 142 RVA: 0x00008BD0 File Offset: 0x00006DD0
		[HarmonyPriority(800)]
		private static void Postfix(bool isServer)
		{
			if (!isServer || !Pack.Loaded)
			{
				return;
			}
			try
			{
				Prefabs.Register("FirstInitialize");
				if (!ServerSeam._once && Mod.IsServer)
				{
					ServerSeam._once = true;
					Main.RegisterCommands();
					Net.InstallJoinStep();
				}
			}
			catch (Exception arg)
			{
				Mod.Log.Error("server seam: " + arg);
			}
		}

		// Token: 0x040000A5 RID: 165
		private static bool _once;
	}
}
