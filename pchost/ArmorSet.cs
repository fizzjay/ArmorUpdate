using System;
using Alta.Impact;

namespace ArmorRestored
{
	// Token: 0x02000004 RID: 4
	internal sealed class ArmorSet
	{
		// Token: 0x0400000D RID: 13
		public string Id;

		// Token: 0x0400000E RID: 14
		public string Title;

		// Token: 0x0400000F RID: 15
		public string LootRarity;

		// Token: 0x04000010 RID: 16
		public float Pierce = 1f;

		// Token: 0x04000011 RID: 17
		public float Slash = 1f;

		// Token: 0x04000012 RID: 18
		public float Blunt = 1f;

		// Token: 0x04000013 RID: 19
		public float Caustic = 1f;

		// Token: 0x04000014 RID: 20
		public float Explosive = 1f;

		// Token: 0x04000015 RID: 21
		public float Conductivity = 1f;

		// Token: 0x04000016 RID: 22
		public ArmorDefinition Definition;
	}
}
