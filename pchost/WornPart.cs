using System;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x02000016 RID: 22
	internal sealed class WornPart
	{
		// Token: 0x0400007C RID: 124
		public bool Skinned;

		// Token: 0x0400007D RID: 125
		public int Mesh;

		// Token: 0x0400007E RID: 126
		public int[] Mats;

		// Token: 0x0400007F RID: 127
		public string Bone;

		// Token: 0x04000080 RID: 128
		public Matrix4x4 Local;

		// Token: 0x04000081 RID: 129
		public string Root;

		// Token: 0x04000082 RID: 130
		public string[] Bones;
	}
}
