using System;
using System.Collections.Generic;

namespace ArmorRestoredQuest
{
	// Token: 0x02000011 RID: 17
	internal sealed class PieceDef
	{
		// Token: 0x04000076 RID: 118
		public readonly List<PartDef> Worn = new List<PartDef>();

		// Token: 0x04000077 RID: 119
		public readonly List<LookPart> Look = new List<LookPart>();

		// Token: 0x04000078 RID: 120
		public readonly List<ColliderDef> Colliders = new List<ColliderDef>();
	}
}
