using System;
using System.Collections.Generic;

namespace ArmorRestored
{
	// Token: 0x02000019 RID: 25
	internal sealed class PieceDef
	{
		// Token: 0x0400008D RID: 141
		public string Id;

		// Token: 0x0400008E RID: 142
		public string Set;

		// Token: 0x0400008F RID: 143
		public string Slot;

		// Token: 0x04000090 RID: 144
		public string Source;

		// Token: 0x04000091 RID: 145
		public readonly List<WornPart> Worn = new List<WornPart>();

		// Token: 0x04000092 RID: 146
		public readonly List<PickupPart> Pickup = new List<PickupPart>();

		// Token: 0x04000093 RID: 147
		public readonly List<ColliderSpec> Colliders = new List<ColliderSpec>();
	}
}
