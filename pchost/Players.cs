using System;
using Alta.Networking.Scripts.Player;

namespace ArmorRestored
{
	// Token: 0x0200001B RID: 27
	internal static class Players
	{
		// Token: 0x06000082 RID: 130 RVA: 0x00007DA4 File Offset: 0x00005FA4
		internal static Player Find(string who, out string why)
		{
			why = null;
			string text = (who ?? "").Trim();
			if (text.Length == 0)
			{
				why = "name a player";
				return null;
			}
			uint num;
			bool flag = uint.TryParse(text, out num);
			foreach (IPlayer player in Player.AllPlayers)
			{
				Player player2 = player as Player;
				if (!(player2 == null) && player2.UserInfo != null)
				{
					if (flag && player2.UserInfo.Identifier == (int)num)
					{
						return player2;
					}
					if (string.Equals(player2.UserInfo.Username, text, StringComparison.OrdinalIgnoreCase))
					{
						return player2;
					}
				}
			}
			why = "no connected player called " + text;
			return null;
		}

		// Token: 0x06000083 RID: 131 RVA: 0x00007E7C File Offset: 0x0000607C
		internal static string Name(Player p)
		{
			if (p == null)
			{
				return "?";
			}
			if (p.UserInfo != null)
			{
				return p.UserInfo.Username;
			}
			return "player " + p.Identifier;
		}
	}
}
