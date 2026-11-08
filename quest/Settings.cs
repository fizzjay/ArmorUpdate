using System;
using System.Collections.Generic;
using System.IO;

namespace ArmorRestoredQuest
{
	// Token: 0x02000014 RID: 20
	internal static class Settings
	{
		// Token: 0x0600006F RID: 111 RVA: 0x00007820 File Offset: 0x00005A20
		internal static void Load()
		{
			string path = Path.Combine(Mod.DataDir, "settings.txt");
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i].Trim();
					if (text.Length != 0 && text[0] != '#')
					{
						int num = text.IndexOf('=');
						if (num > 0)
						{
							dictionary[text.Substring(0, num).Trim()] = text.Substring(num + 1).Trim();
						}
					}
				}
			}
			string a;
			if (dictionary.TryGetValue("own_helmet", out a))
			{
				Settings.OwnHelmet = (a == "show");
			}
			if (dictionary.TryGetValue("debug", out a))
			{
				Settings.Debug = (a == "on");
			}
			if (dictionary.ContainsKey("own_helmet") && dictionary.ContainsKey("debug"))
			{
				return;
			}
			File.WriteAllLines(path, new string[]
			{
				"# own_helmet = hide | show     draw your own helmet; hidden by default so it never blocks your view",
				"# debug = off | on             log every armor grab, let go and near miss, every piece drawn, and the armor near you",
				"own_helmet = " + (Settings.OwnHelmet ? "show" : "hide"),
				"debug = " + (Settings.Debug ? "on" : "off")
			});
		}

		// Token: 0x04000090 RID: 144
		internal static bool OwnHelmet;

		// Token: 0x04000091 RID: 145
		internal static bool Debug;
	}
}
