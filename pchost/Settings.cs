using System;
using System.Collections.Generic;
using System.IO;

namespace ArmorRestored
{
	// Token: 0x02000020 RID: 32
	internal static class Settings
	{
		// Token: 0x060000A4 RID: 164 RVA: 0x000099E4 File Offset: 0x00007BE4
		internal static void Load()
		{
			string path = Path.Combine(Mod.DataDir, "settings.txt");
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (File.Exists(path))
			{
				foreach (string text in File.ReadAllLines(path))
				{
					string text2 = text.Trim();
					if (text2.Length != 0 && text2[0] != '#')
					{
						int num = text2.IndexOf('=');
						if (num > 0)
						{
							dictionary[text2.Substring(0, num).Trim()] = text2.Substring(num + 1).Trim();
						}
					}
				}
			}
			string text3;
			if (dictionary.TryGetValue("join", out text3) && (text3 == "refuse" || text3 == "log" || text3 == "off"))
			{
				Settings.Join = text3;
			}
			if (dictionary.TryGetValue("loot", out text3))
			{
				Settings.Loot = (text3 != "off");
			}
			if (dictionary.TryGetValue("wyrm_loot", out text3))
			{
				Settings.WyrmLoot = (text3 == "on");
			}
			if (dictionary.TryGetValue("own_helmet", out text3))
			{
				Settings.OwnHelmet = (text3 == "show");
			}
			if (dictionary.TryGetValue("debug", out text3))
			{
				Settings.Debug = (text3 == "on");
			}
			if (dictionary.ContainsKey("join") && dictionary.ContainsKey("loot") && dictionary.ContainsKey("wyrm_loot") && dictionary.ContainsKey("own_helmet") && dictionary.ContainsKey("debug"))
			{
				return;
			}
			File.WriteAllLines(path, new List<string>(Settings.Lines)
			{
				"join = " + Settings.Join,
				"loot = " + (Settings.Loot ? "on" : "off"),
				"wyrm_loot = " + (Settings.WyrmLoot ? "on" : "off"),
				"own_helmet = " + (Settings.OwnHelmet ? "show" : "hide"),
				"debug = " + (Settings.Debug ? "on" : "off")
			}.ToArray());
		}

		// Token: 0x040000AC RID: 172
		internal static string Join = "refuse";

		// Token: 0x040000AD RID: 173
		internal static bool Loot = true;

		// Token: 0x040000AE RID: 174
		internal static bool WyrmLoot;

		// Token: 0x040000AF RID: 175
		internal static bool OwnHelmet;

		// Token: 0x040000B0 RID: 176
		internal static bool Debug;

		// Token: 0x040000B1 RID: 177
		private static readonly string[] Lines = new string[]
		{
			"# own_helmet = hide | show     (client) draw your own helmet; hidden by default so it never blocks a headset view",
			"# join = refuse | log | off   what the server does when a player joins without an ArmorRestored that plays with this one",
			"# loot = on | off              leather (Rare) and metal (Epic) armor in the global loot table, as in 0.0.32",
			"# wyrm_loot = on | off         also put Wyrm armor in the loot table (Epic); 0.0.32 never dropped it",
			"# debug = off | on             client: log every armor grab, let go and near miss; server: log every hit a player takes, with its armor"
		};
	}
}
