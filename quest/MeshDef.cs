using System;
using UnityEngine;

namespace ArmorRestoredQuest
{
	// Token: 0x0200000D RID: 13
	internal sealed class MeshDef
	{
		// Token: 0x04000056 RID: 86
		public string Name;

		// Token: 0x04000057 RID: 87
		public float[] V;

		// Token: 0x04000058 RID: 88
		public float[] N;

		// Token: 0x04000059 RID: 89
		public float[] T;

		// Token: 0x0400005A RID: 90
		public float[] UV;

		// Token: 0x0400005B RID: 91
		public float[] Weights;

		// Token: 0x0400005C RID: 92
		public float[] Bind;

		// Token: 0x0400005D RID: 93
		public int[] Indices;

		// Token: 0x0400005E RID: 94
		public int[][] Subs;

		// Token: 0x0400005F RID: 95
		public int Tris;

		// Token: 0x04000060 RID: 96
		public Mesh Made;
	}
}
