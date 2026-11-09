using System;
using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;

namespace ArmorRestored
{
	// Token: 0x02000011 RID: 17
	[HarmonyPatch(typeof(ServerHandler), "ConnectionCreated")]
	internal static class ServerConnectionSeam
	{
		// Token: 0x0600006A RID: 106 RVA: 0x00006FDC File Offset: 0x000051DC
		private static void Postfix(Connection connection)
		{
			if (connection == null || !Mod.IsServer)
			{
				return;
			}
			try
			{
				connection.SetHandler(Net.Message, new SerializeConnectionMethod(Net.ServerRead));
			}
			catch (Exception ex)
			{
				Mod.Log.Error("server handler: " + ex.Message);
			}
		}
	}
}
