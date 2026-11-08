using System;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x02000014 RID: 20
	internal sealed class MatDef
	{
		// Token: 0x0600006E RID: 110 RVA: 0x00007160 File Offset: 0x00005360
		public Material Get()
		{
			if (this._mat != null)
			{
				return this._mat;
			}
			Shader shader = Shader.Find("Standard");
			if (shader == null)
			{
				shader = Shader.Find("Township Tale/Cheap/SimpleStandard");
			}
			this._mat = new Material(shader);
			this._mat.name = "ArmorRestored " + this.Name;
			this._mat.SetColor("_Color", this.Color);
			this._mat.SetFloat("_Metallic", this.Metallic);
			this._mat.SetFloat("_Glossiness", this.Gloss);
			this._mat.SetFloat("_GlossMapScale", this.Gloss);
			Texture2D texture2D;
			if ((texture2D = MatDef.Tex(this.Main)) != null)
			{
				this._mat.SetTexture("_MainTex", texture2D);
			}
			if (this.Name == "Metal Armor")
			{
				this._mat.SetFloat("_Metallic", 0.45f);
				this._mat.SetFloat("_Glossiness", 0.4f);
			}
			else if ((texture2D = MatDef.Tex(this.Metal)) != null)
			{
				this._mat.SetTexture("_MetallicGlossMap", texture2D);
				this._mat.EnableKeyword("_METALLICGLOSSMAP");
			}
			if ((texture2D = MatDef.Tex(this.Normal)) != null)
			{
				this._mat.SetTexture("_BumpMap", texture2D);
				this._mat.EnableKeyword("_NORMALMAP");
			}
			if ((texture2D = MatDef.Tex(this.Occlusion)) != null)
			{
				this._mat.SetTexture("_OcclusionMap", texture2D);
			}
			Object.DontDestroyOnLoad(this._mat);
			return this._mat;
		}

		// Token: 0x0600006F RID: 111 RVA: 0x00007327 File Offset: 0x00005527
		private static Texture2D Tex(int i)
		{
			if (i < 0 || i >= Pack.Textures.Count)
			{
				return null;
			}
			return Pack.Textures[i].Get();
		}

		// Token: 0x0400006A RID: 106
		public string Name;

		// Token: 0x0400006B RID: 107
		public int Main;

		// Token: 0x0400006C RID: 108
		public int Normal;

		// Token: 0x0400006D RID: 109
		public int Metal;

		// Token: 0x0400006E RID: 110
		public int Occlusion;

		// Token: 0x0400006F RID: 111
		public Color Color;

		// Token: 0x04000070 RID: 112
		public float Metallic;

		// Token: 0x04000071 RID: 113
		public float Gloss;

		// Token: 0x04000072 RID: 114
		private Material _mat;
	}
}
