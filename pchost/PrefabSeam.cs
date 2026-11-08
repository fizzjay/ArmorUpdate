using System;
using Alta.Networking;
using HarmonyLib;

namespace ArmorRestored
{
	// Token: 0x0200001D RID: 29
	[HarmonyPatch(typeof(PrefabManager), "PrepareSpawnSetups")]
	internal static class PrefabSeam
	{
		// Token: 0x0600008D RID: 141 RVA: 0x00008B78 File Offset: 0x00006D78
		[HarmonyPriority(800)]
		private static void Postfix()
		{
			if (!Mod.IsServer && !Gate.IsOpen)
			{
				return;
			}
			try
			{
				Prefabs.Register("PrepareSpawnSetups");
			}
			catch (Exception ex)
			{
				Mod.Log.Error("prefab seam: " + ex.Message);
			}
		}
	}
}
