using System;
using System.Collections.Generic;
using Alta.Impact;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x02000002 RID: 2
	internal sealed class Wiring
	{
		// Token: 0x04000001 RID: 1
		public Transform Root;

		// Token: 0x04000002 RID: 2
		public EquipmentReference[] Zones;

		// Token: 0x04000003 RID: 3
		public int Detectors;

		// Token: 0x04000004 RID: 4
		public int Heat;

		// Token: 0x04000005 RID: 5
		public readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();

		// Token: 0x04000006 RID: 6
		public readonly List<ImpactDetector>[] ZoneDetectors = new List<ImpactDetector>[7];
	}
}
