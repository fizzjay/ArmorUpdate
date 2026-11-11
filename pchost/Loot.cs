using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Alta.Inventory;
using Alta.Loot;
using Alta.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestored
{
	// Token: 0x0200000C RID: 12
	internal static class Loot
	{
		// Token: 0x0600004C RID: 76 RVA: 0x00005644 File Offset: 0x00003844
		internal static void Add()
		{
			FieldInfo field = typeof(LootTable.RarityList).GetField("entries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo field2 = typeof(LootTable.Entry).GetField("item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null || field2 == null)
			{
				Loot.Report = "LootTable.RarityList.entries or LootTable.Entry.item moved";
				Mod.Log.Error("loot: " + Loot.Report);
				return;
			}
			LootTable lootTable = null;
			List<string> list = new List<string>();
			foreach (LootTable lootTable2 in Resources.LoadAll<LootTable>("Items/Loot Tables/"))
			{
				list.Add(lootTable2.name);
				if (lootTable2.name == "[Global]")
				{
					lootTable = lootTable2;
				}
			}
			if (lootTable == null)
			{
				Loot.Report = "no [Global] loot table (found: " + string.Join(", ", list.ToArray()) + ")";
				Mod.Log.Warning("loot: " + Loot.Report);
				return;
			}
			List<string> list2 = new List<string>();
			// Wyrm is kill-only. Never insert its items into the global chest/loot table.
			for (int j = 0; j < Catalog.Sets.Length; j++)
			{
				string text = Catalog.Sets[j].LootRarity;
				if (text != null)
				{
					LootTable.RarityList rarityList = null;
					foreach (LootTable.RarityList rarityList2 in lootTable.Entries)
					{
						if (rarityList2 != null && rarityList2.Rarity != null && rarityList2.Rarity.name == text)
						{
							rarityList = rarityList2;
						}
					}
					if (rarityList == null)
					{
						list2.Add(Catalog.Sets[j].Title + ": no " + text + " list");
					}
					else
					{
						List<LootTable.Entry> list3 = new List<LootTable.Entry>(rarityList.Entries ?? new LootTable.Entry[0]);
						int num = 0;
						for (int k = 0; k < 8; k++)
						{
							int num2 = Catalog.Piece(j, k);
							if (!(Prefabs.Items[num2] == null))
							{
								bool flag = false;
								foreach (LootTable.Entry entry in list3)
								{
									if (entry != null && object.ReferenceEquals(entry.Item, Prefabs.Items[num2]))
									{
										flag = true;
									}
								}
								if (!flag)
								{
									LootTable.Entry entry2 = new LootTable.Entry();
									field2.SetValue(entry2, Prefabs.Items[num2]);
									list3.Add(entry2);
									num++;
								}
							}
						}
                        // Keep every armor slot, but give existing non-armor entries
                        // more tickets instead of changing the rarity tiers globally.
                        // Snapshot once so repeated registration cannot compound it.
                        LootTable.Entry[] baseline;
                        if(!originalPools.TryGetValue(rarityList,out baseline))
                        {baseline=(rarityList.Entries ?? new LootTable.Entry[0]);originalPools[rarityList]=baseline;}
                        var ordinary=new List<LootTable.Entry>();
                        foreach(var candidate in baseline)
                            if(candidate!=null && candidate.Item!=null && !Prefabs.IsArmor(candidate.Item.Hash)) ordinary.Add(candidate);
                        if(ordinary.Count>0)
                        {
                            int armorCount=0;
                            foreach(var candidate in list3) if(candidate!=null && candidate.Item!=null && Prefabs.IsArmor(candidate.Item.Hash)) armorCount++;
                            // (N+A)/(factor*N+A) <= 1/reduction for equal entry tickets.
                            int reduction=Catalog.Sets[j].Id=="hebios" ? 4 : 2;
                            int factor=reduction+(int)Math.Ceiling((double)((reduction-1)*armorCount)/ordinary.Count);
                            list3.RemoveAll(candidate=>candidate!=null && candidate.Item!=null && !Prefabs.IsArmor(candidate.Item.Hash));
                            for(int ticket=0;ticket<factor;ticket++) list3.AddRange(ordinary);
                            list2.Add(Catalog.Sets[j].Title+" chest dilution: vanilla tickets x"+factor+", target armor /"+reduction);
                        }
                        else Mod.Log.Warning("loot: no ordinary entries to dilute "+Catalog.Sets[j].Title+"; armor rate unchanged");
						field.SetValue(rarityList, list3.ToArray());
						list2.Add(string.Concat(new object[]
						{
							Catalog.Sets[j].Title,
							" +",
							num,
							" in ",
							text,
							" (",
							list3.Count,
							" entries)"
						}));
					}
				}
			}
			Loot.Report = "[Global]: " + string.Join("; ", list2.ToArray());
			Mod.Log.Msg("loot: " + Loot.Report);
		}

		// Token: 0x0600004D RID: 77 RVA: 0x000059C0 File Offset: 0x00003BC0
		internal static string Check(int rolls)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < Catalog.Sets.Length; i++)
			{
				Item item = Prefabs.Items[Catalog.Piece(i, 1)];
				if (!(item == null))
				{
					List<string> list = new List<string>();
					foreach (PickupTag pickupTag in item.Tags)
					{
						if (pickupTag != null)
						{
							list.Add(pickupTag.name);
						}
					}
					stringBuilder.Append(Catalog.Sets[i].Title).Append(": category ").Append((item.LootCategory != null) ? (item.LootCategory.name + " x" + item.LootCategory.Weighting) : "none").Append(", tags ").Append(string.Join(",", list.ToArray())).Append('\n');
				}
			}
			FieldInfo field = typeof(LootModule).GetField("prosperity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
			int num = 0;
			int num2 = 0;
			int num4 = 0;
			int num5 = 0;
			foreach (LootModule lootModule in Object.FindObjectsOfType<LootModule>())
			{
				if (!(lootModule.Group == null))
				{
					num++;
					float num6 = (float)field.GetValue(lootModule);
					foreach (PickupDock pickupDock in lootModule.Group)
					{
						if (!(pickupDock == null))
						{
							num2++;
							PickupDock.AllowedResult allowedResult = pickupDock.CheckAllowed(Prefabs.Items[Catalog.Piece(0, 1)]);
							// AllowedResult is an enum, not a nullable rejection object.
							// Report the game's named results without guessing its success value.
							string key = allowedResult.ToString();
							dictionary2[key] = dictionary2.ContainsKey(key) ? dictionary2[key] + 1 : 1;
							for (int k = 0; k < rolls; k++)
							{
								int num7;
								Item loot = LootTable.GetLoot(pickupDock, new LootTable[0], num6, out num7);
								num4++;
								if (!(loot == null) && Prefabs.IsArmor(loot.Hash))
								{
									num5++;
									string key2 = loot.name + " x" + num7;
									dictionary[key2] = (dictionary.ContainsKey(key2) ? (dictionary[key2] + 1) : 1);
								}
							}
						}
					}
				}
			}
			if (num == 0)
			{
				List<string> list2 = new List<string>();
				foreach (NetworkPrefab networkPrefab in PrefabManager.AllPrefabs)
				{
					if (networkPrefab != null && networkPrefab.GetComponentInChildren<LootModule>(true) != null && list2.Count < 20)
					{
						list2.Add(networkPrefab.name + " " + networkPrefab.Hash);
					}
				}
				stringBuilder.Append("prefabs with a loot module: ").Append(string.Join(", ", list2.ToArray())).Append('\n');
			}
			stringBuilder.Append(num).Append(" loot modules, ").Append(num2).Append(" docks; leather allowance results");
			foreach (KeyValuePair<string, int> keyValuePair in dictionary2)
			{
				stringBuilder.Append("; ").Append(keyValuePair.Value).Append(' ').Append(keyValuePair.Key);
			}
			stringBuilder.Append('\n').Append(num4).Append(" rolls, ").Append(num5).Append(" armor");
			foreach (KeyValuePair<string, int> keyValuePair2 in dictionary)
			{
				stringBuilder.Append("; ").Append(keyValuePair2.Key).Append(" ").Append(keyValuePair2.Value).Append(" times");
			}
			return stringBuilder.ToString();
		}

		// Token: 0x04000042 RID: 66
		private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		// Token: 0x04000043 RID: 67
        private static readonly Dictionary<LootTable.RarityList,LootTable.Entry[]> originalPools = new Dictionary<LootTable.RarityList,LootTable.Entry[]>();
		internal static string Report = "not added";
	}
}
