using System;
using System.Collections.Generic;
using System.Reflection;
using Alta.Impact;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x02000006 RID: 6
	internal static class Catalog
	{
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x0600000F RID: 15 RVA: 0x00002831 File Offset: 0x00000A31
		internal static int Count
		{
			get
			{
				return Catalog.Sets.Length * 8;
			}
		}

		// Token: 0x06000010 RID: 16 RVA: 0x0000283C File Offset: 0x00000A3C
		internal static int Number(int piece)
		{
			return 52830 + piece;
		}

		// Token: 0x06000011 RID: 17 RVA: 0x00002845 File Offset: 0x00000A45
		internal static int Piece(int set, int slot)
		{
			return set * 8 + slot;
		}

		// Token: 0x06000012 RID: 18 RVA: 0x0000284C File Offset: 0x00000A4C
		internal static int PieceOf(uint number)
		{
			int num = (int)(number - 52830U);
			if (num < 0 || num >= Catalog.Count)
			{
				return -1;
			}
			return num;
		}

		// Token: 0x06000013 RID: 19 RVA: 0x00002870 File Offset: 0x00000A70
		internal static ArmorSet SetOf(int piece)
		{
			return Catalog.Sets[piece / 8];
		}

		// Token: 0x06000014 RID: 20 RVA: 0x0000287B File Offset: 0x00000A7B
		internal static Slot SlotOf(int piece)
		{
			return Catalog.Slots[piece % 8];
		}

		// Token: 0x06000015 RID: 21 RVA: 0x00002886 File Offset: 0x00000A86
		internal static string Title(int piece)
		{
			return Catalog.SetOf(piece).Title + " " + Catalog.SlotOf(piece).Title;
		}

		// Token: 0x06000016 RID: 22 RVA: 0x000028A8 File Offset: 0x00000AA8
		internal static string Id(int piece)
		{
			return Catalog.SetOf(piece).Id + "." + Catalog.SlotOf(piece).Id;
		}

		// Token: 0x06000017 RID: 23 RVA: 0x000028CC File Offset: 0x00000ACC
		internal static int FindSet(string s)
		{
			for (int i = 0; i < Catalog.Sets.Length; i++)
			{
				if (string.Equals(Catalog.Sets[i].Id, s, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x06000018 RID: 24 RVA: 0x00002904 File Offset: 0x00000B04
		internal static int FindSlot(string s)
		{
			for (int i = 0; i < Catalog.Slots.Length; i++)
			{
				if (string.Equals(Catalog.Slots[i].Id, s, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x06000019 RID: 25 RVA: 0x0000293C File Offset: 0x00000B3C
		internal static void Check()
		{
			if (Pack.Pieces.Count != Catalog.Count)
			{
				throw new InvalidOperationException(string.Concat(new object[]
				{
					"the armor pack has ",
					Pack.Pieces.Count,
					" pieces, expected ",
					Catalog.Count
				}));
			}
			for (int i = 0; i < Catalog.Count; i++)
			{
				if (Pack.Pieces[i].Id != Catalog.Id(i) || Pack.Pieces[i].Slot != Catalog.SlotOf(i).Game.ToString())
				{
					throw new InvalidOperationException(string.Concat(new object[]
					{
						"armor pack piece ",
						i,
						" is ",
						Pack.Pieces[i].Id,
						"/",
						Pack.Pieces[i].Slot,
						", expected ",
						Catalog.Id(i)
					}));
				}
			}
		}

		// Token: 0x0600001A RID: 26 RVA: 0x00002A68 File Offset: 0x00000C68
		internal static ArmorDefinition Definition(int set)
		{
			ArmorSet armorSet = Catalog.Sets[set];
			if (armorSet.Definition != null)
			{
				return armorSet.Definition;
			}
			FieldInfo field = typeof(ArmorDefinition).GetField("attackTypeMultipliers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo field2 = typeof(ArmorDefinition).GetField("conductivityMultiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo field3 = typeof(AttackTypeMultiplier).GetField("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo field4 = typeof(AttackTypeMultiplier).GetField("multiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null || field2 == null || field3 == null || field4 == null)
			{
				throw new MissingFieldException("ArmorDefinition or AttackTypeMultiplier lost a field");
			}
			ArmorDefinition armorDefinition = ScriptableObject.CreateInstance<ArmorDefinition>();
			armorDefinition.name = armorSet.Title + "Armor";
			Object.DontDestroyOnLoad(armorDefinition);
			ImpactType[] array = new ImpactType[]
			{
				default(ImpactType),
				1,
				2,
				3,
				5
			};
			float[] array2 = new float[]
			{
				armorSet.Pierce,
				armorSet.Slash,
				armorSet.Blunt,
				armorSet.Caustic,
				armorSet.Explosive
			};
			List<AttackTypeMultiplier> list = new List<AttackTypeMultiplier>();
			for (int i = 0; i < array.Length; i++)
			{
				if (array2[i] != 1f)
				{
					AttackTypeMultiplier attackTypeMultiplier = new AttackTypeMultiplier();
					field3.SetValue(attackTypeMultiplier, array[i]);
					field4.SetValue(attackTypeMultiplier, array2[i]);
					list.Add(attackTypeMultiplier);
				}
			}
			field.SetValue(armorDefinition, list.ToArray());
			field2.SetValue(armorDefinition, armorSet.Conductivity);
			armorSet.Definition = armorDefinition;
			return armorDefinition;
		}

		// Token: 0x0400001C RID: 28
		internal const int FirstNumber = 52830;

		// Token: 0x0400001D RID: 29
		internal const int SlotCount = 8;

		// Token: 0x0400001E RID: 30
		internal static readonly ArmorSet[] Sets = new ArmorSet[]
		{
			new ArmorSet
			{
				Id = "leather",
				Title = "Leather",
				LootRarity = "3. Rare",
				Pierce = 0.7f,
				Slash = 0.7f,
				Blunt = 0.85f,
				Conductivity = 0.2f
			},
			new ArmorSet
			{
				Id = "metal",
				Title = "Metal",
				LootRarity = "4. Epic",
				Pierce = 0.5f,
				Slash = 0.4f,
				Blunt = 0.9f,
				Caustic = 0.6f,
				Explosive = 0.4f,
				Conductivity = 1.2f
			},
			new ArmorSet
			{
				Id = "wyrm",
				Title = "Wyrm",
				LootRarity = null,
				Pierce = 1f,
				Slash = 0.8f,
				Blunt = 0.6f,
				Caustic = 0.1f,
				Conductivity = 0.8f
			}
		};

		// Token: 0x0400001F RID: 31
		internal static readonly Slot[] Slots = new Slot[]
		{
			new Slot
			{
				Id = "helmet",
				Title = "Helmet",
				Game = 1,
				Anchor = "head_jnt"
			},
			new Slot
			{
				Id = "chest",
				Title = "Chest Plate",
				Game = 6,
				Anchor = "chest_jnt"
			},
			new Slot
			{
				Id = "shoulder_l",
				Title = "Left Shoulder Pad",
				Game = 2,
				Anchor = "shoulder_L_jnt"
			},
			new Slot
			{
				Id = "shoulder_r",
				Title = "Right Shoulder Pad",
				Game = 3,
				Anchor = "shoulder_R_jnt"
			},
			new Slot
			{
				Id = "gauntlet_l",
				Title = "Left Gauntlet",
				Game = 4,
				Anchor = "forearm_L_jnt",
				RemovedBy = 2
			},
			new Slot
			{
				Id = "gauntlet_r",
				Title = "Right Gauntlet",
				Game = 5,
				Anchor = "forearm_R_jnt",
				RemovedBy = 1
			},
			new Slot
			{
				Id = "tasset_l",
				Title = "Left Tasset",
				Game = 7,
				Anchor = "hipBase_jnt"
			},
			new Slot
			{
				Id = "tasset_r",
				Title = "Right Tasset",
				Game = 8,
				Anchor = "hipBase_jnt"
			}
		};
	}
}
