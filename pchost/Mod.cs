using System;
using MelonLoader;

namespace ArmorRestored
{
	// Token: 0x0200000D RID: 13
	internal static class Mod
	{
		// Token: 0x04000044 RID: 68
		internal const string Name = "ArmorRestored";

		// Token: 0x04000045 RID: 69
		internal const string Version = "0.2.1";

		// Token: 0x04000046 RID: 70
		internal static bool IsServer;

		// Token: 0x04000047 RID: 71
		internal static MelonLogger.Instance Log = new MelonLogger.Instance("ArmorRestored");

		// Token: 0x04000048 RID: 72
		internal static string DataDir;
	}
}
