using System;
using System.Collections.Generic;
using System.Reflection;
using Alta.Impact;
using Alta.Customization;
using UnityEngine;
using Object = UnityEngine.Object;

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
			return piece == 45 ? 62076 : 52830 + piece + (piece >= 24 ? 1 : 0);
		}

		// Token: 0x06000011 RID: 17 RVA: 0x00002845 File Offset: 0x00000A45
		internal static int Piece(int set, int slot)
		{
			return set * 8 + slot;
		}

		// Token: 0x06000012 RID: 18 RVA: 0x0000284C File Offset: 0x00000A4C
		internal static int PieceOf(uint number)
		{
			if (number == 62076U) return 45;
            if (number == 52876U) return -1; // Native Garlic Full Burnt, never armor.
			int num = (int)(number - 52830U - (number > 52854U ? 1U : 0U));
			if (number == 52854U || num < 0 || num >= Catalog.Count)
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
				(ImpactType)1,
				(ImpactType)2,
				(ImpactType)3,
				(ImpactType)5
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

        private static readonly Dictionary<float, ArmorDefinition> MetalDefinitions = new Dictionary<float, ArmorDefinition>();

        // Keep the native impact pipeline and set conductivity/elemental behavior.
        // Only physical damage multipliers scale with the material strength.
        internal static ArmorDefinition MaterialDefinition(int piece, uint material)
        {
            ArmorDefinition original = Definition(piece / 8);
            if (piece / 8 != 1 && piece / 8 != 3) return original;
            float strength = ArmorStats.Strength(material);
            if (strength == 1f) return original;
            ArmorDefinition result;
            if (MetalDefinitions.TryGetValue(strength, out result)) return result;
            FieldInfo entries = typeof(ArmorDefinition).GetField("attackTypeMultipliers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo kind = typeof(AttackTypeMultiplier).GetField("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo amount = typeof(AttackTypeMultiplier).GetField("multiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var values = new List<AttackTypeMultiplier>();
            foreach (AttackTypeMultiplier entry in (AttackTypeMultiplier[])entries.GetValue(original))
            {
                var copy = new AttackTypeMultiplier();
                ImpactType type = (ImpactType)kind.GetValue(entry);
                float multiplier = (float)amount.GetValue(entry);
                if ((int)type == 0 || (int)type == 1 || (int)type == 2)
                    multiplier = Mathf.Pow(Mathf.Clamp(multiplier, .01f, 1f), strength);
                kind.SetValue(copy, type);
                amount.SetValue(copy, multiplier);
                values.Add(copy);
            }
            result = Object.Instantiate(original);
            result.name = "Metal Armor material strength " + strength;
            entries.SetValue(result, values.ToArray());
            Object.DontDestroyOnLoad(result);
            MetalDefinitions.Add(strength, result);
            return result;
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
				LootRarity = "2. Uncommon",
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
			},
            new ArmorSet { Id="hebios", Title="Hebios", LootRarity="5. Legendary", Pierce=.5f, Slash=.4f, Blunt=.9f, Caustic=.6f, Explosive=.4f, Conductivity=1.2f },
            new ArmorSet { Id="wood", Title="Wood", LootRarity=null, Pierce=.8f, Slash=.7f, Blunt=.85f, Conductivity=.2f },
            new ArmorSet { Id="mushroom", Title="Mushroom", LootRarity="3. Rare", Pierce=.85f, Slash=.75f, Blunt=.85f, Conductivity=.2f }
		};

		// Token: 0x0400001F RID: 31
		internal static readonly Slot[] Slots = new Slot[]
		{
			new Slot
			{
				Id = "helmet",
				Title = "Helmet",
				Game = (EquipmentSlot)1,
				Anchor = "head_jnt"
			},
			new Slot
			{
				Id = "chest",
				Title = "Chest Plate",
				Game = (EquipmentSlot)6,
				Anchor = "chest_jnt"
			},
			new Slot
			{
				Id = "shoulder_l",
				Title = "Left Shoulder Pad",
				Game = (EquipmentSlot)2,
				Anchor = "shoulder_L_jnt"
			},
			new Slot
			{
				Id = "shoulder_r",
				Title = "Right Shoulder Pad",
				Game = (EquipmentSlot)3,
				Anchor = "shoulder_R_jnt"
			},
			new Slot
			{
				Id = "gauntlet_l",
				Title = "Left Gauntlet",
				Game = (EquipmentSlot)4,
				Anchor = "forearm_L_jnt",
				RemovedBy = 2
			},
			new Slot
			{
				Id = "gauntlet_r",
				Title = "Right Gauntlet",
				Game = (EquipmentSlot)5,
				Anchor = "forearm_R_jnt",
				RemovedBy = 1
			},
			new Slot
			{
				Id = "tasset_l",
				Title = "Left Tasset",
				Game = (EquipmentSlot)7,
				Anchor = "hipBase_jnt"
			},
			new Slot
			{
				Id = "tasset_r",
				Title = "Right Tasset",
				Game = (EquipmentSlot)8,
				Anchor = "hipBase_jnt"
			}
		};
	}
}
