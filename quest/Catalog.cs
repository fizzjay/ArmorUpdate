using System;

namespace ArmorRestoredQuest
{
	// Token: 0x02000002 RID: 2
	internal static class Catalog
	{
		// Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		internal static uint Number(int piece)
		{
			return (uint)(52830 + piece + (piece >= 24 ? 1 : 0));
		}

		// Token: 0x06000002 RID: 2 RVA: 0x0000205C File Offset: 0x0000025C
		internal static int PieceOf(uint number)
		{
			int num = (int)(number - 52830U - (number > 52854U ? 1U : 0U));
			if (number == 52854U || num < 0 || num >= Catalog.Count)
			{
				return -1;
			}
			return num;
		}

		// Token: 0x06000003 RID: 3 RVA: 0x0000207D File Offset: 0x0000027D
		internal static string Id(int piece)
		{
			return Catalog.SetIds[piece / 8] + "." + Catalog.SlotIds[piece % 8];
		}

		// Token: 0x06000004 RID: 4 RVA: 0x0000209B File Offset: 0x0000029B
		internal static string Title(int piece)
		{
			return Catalog.SetTitles[piece / 8] + " " + Catalog.SlotTitles[piece % 8];
		}

		// Token: 0x06000005 RID: 5 RVA: 0x000020B9 File Offset: 0x000002B9
		internal static float Reach(int slot)
		{
			if (slot != 1 && slot < 6)
			{
				return 0.3f;
			}
			return 0.4f;
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000020D0 File Offset: 0x000002D0
		internal static int[] Empty()
		{
			int[] array = new int[8];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = -1;
			}
			return array;
		}

		// Token: 0x06000007 RID: 7 RVA: 0x000020F8 File Offset: 0x000002F8
		// Note: this type is marked as 'beforefieldinit'.
		static Catalog()
		{
			int[] array = new int[8];
			array[4] = 2;
			array[5] = 1;
			Catalog.RemovedBy = array;
		}

		// Token: 0x04000001 RID: 1
		internal const int FirstNumber = 52830;

		// Token: 0x04000002 RID: 2
		internal const int SlotCount = 8;

		// Token: 0x04000003 RID: 3
		internal const int Count = 32;

		// Token: 0x04000004 RID: 4
		private static readonly string[] SetIds = new string[]
		{
			"leather",
			"metal",
			"wyrm", "hebios"
		};

		// Token: 0x04000005 RID: 5
		private static readonly string[] SetTitles = new string[]
		{
			"Leather",
			"Metal",
			"Wyrm", "Hebios"
		};

		// Token: 0x04000006 RID: 6
		internal static readonly string[] SlotIds = new string[]
		{
			"helmet",
			"chest",
			"shoulder_l",
			"shoulder_r",
			"gauntlet_l",
			"gauntlet_r",
			"tasset_l",
			"tasset_r"
		};

		// Token: 0x04000007 RID: 7
		private static readonly string[] SlotTitles = new string[]
		{
			"Helmet",
			"Chest Plate",
			"Left Shoulder Pad",
			"Right Shoulder Pad",
			"Left Gauntlet",
			"Right Gauntlet",
			"Left Tasset",
			"Right Tasset"
		};

		// Token: 0x04000008 RID: 8
		internal static readonly string[] Anchors = new string[]
		{
			"head_jnt",
			"chest_jnt",
			"shoulder_L_jnt",
			"shoulder_R_jnt",
			"forearm_L_jnt",
			"forearm_R_jnt",
			"hipBase_jnt",
			"hipBase_jnt"
		};

		// Token: 0x04000009 RID: 9
		internal static readonly int[] RemovedBy;
	}
}
