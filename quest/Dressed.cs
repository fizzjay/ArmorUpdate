using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmorRestoredQuest
{
	// Token: 0x02000004 RID: 4
	internal sealed class Dressed
	{
		// Token: 0x0400000E RID: 14
		public string Name;

		// Token: 0x0400000F RID: 15
		public int User;

		// Token: 0x04000010 RID: 16
		public Transform Root;

		// Token: 0x04000011 RID: 17
		public Transform Rig;
        public bool Dummy;
        public int BakedBindings;
        public float NextBindingRetry;

		// Token: 0x04000012 RID: 18
		public Dictionary<string, Transform> Bones;

		// Token: 0x04000013 RID: 19
		public int Layer;

		// Token: 0x04000014 RID: 20
		public int[] Shown = Catalog.Empty();
		public uint[] ShownMaterials = new uint[8];
        public string[] ShownPalettes = new string[8];
        public CosmeticCoverage Coverage = new CosmeticCoverage();

		// Token: 0x04000015 RID: 21
		public List<GameObject>[] Slots = new List<GameObject>[8];

		// Token: 0x04000016 RID: 22
		public List<Renderer>[] Renderers = new List<Renderer>[8];

		// Token: 0x04000017 RID: 23
		public List<Renderer>[] Skinned = new List<Renderer>[8];

		// Token: 0x04000018 RID: 24
		public int[] Tris = new int[8];

		// Token: 0x04000019 RID: 25
		public string Cost;
	}
}
