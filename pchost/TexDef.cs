using System;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x02000013 RID: 19
	internal sealed class TexDef
	{
		// Token: 0x0600006C RID: 108 RVA: 0x0000708C File Offset: 0x0000528C
		public Texture2D Get()
		{
			if (this._tex != null)
			{
				return this._tex;
			}
			if (this.Format < 0)
			{
				Mod.Log.Warning("texture " + this.Name + " is not DXT; left out");
				return null;
			}
			this._tex = new Texture2D(this.W, this.H, this.Format, this.Mips > 1, this.Linear);
			this._tex.name = "ArmorRestored " + this.Name;
			this._tex.LoadRawTextureData(this.Data);
			this._tex.Apply(false, true);
			Object.DontDestroyOnLoad(this._tex);
			this.Data = null;
			return this._tex;
		}

		// Token: 0x04000062 RID: 98
		public string Name;

		// Token: 0x04000063 RID: 99
		public int Format;

		// Token: 0x04000064 RID: 100
		public int W;

		// Token: 0x04000065 RID: 101
		public int H;

		// Token: 0x04000066 RID: 102
		public int Mips;

		// Token: 0x04000067 RID: 103
		public byte[] Data;

		// Token: 0x04000068 RID: 104
		public bool Linear;

		// Token: 0x04000069 RID: 105
		private Texture2D _tex;
	}
}
