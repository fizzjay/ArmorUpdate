using System;
using Alta.Networking;
using HarmonyLib;

namespace ArmorRestored
{
	// Token: 0x02000012 RID: 18
	[HarmonyPatch(typeof(JoinServerPipeline), "ConnectionApprovedByServer")]
	internal static class ClientConnectionSeam
	{
		// Token: 0x0600006B RID: 107 RVA: 0x0000703C File Offset: 0x0000523C
		private static void Postfix(Connection connection)
		{
			if (Mod.IsServer || connection == null)
			{
				return;
			}
			try
			{
				Gate.Listen(connection);
			}
			catch (Exception ex)
			{
				Mod.Log.Error("client handler: " + ex.Message);
			}
		}
	}
}
