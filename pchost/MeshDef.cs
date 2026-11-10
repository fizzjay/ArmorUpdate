using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestored
{
	// Token: 0x02000015 RID: 21
	internal sealed class MeshDef
	{
		// Token: 0x06000071 RID: 113 RVA: 0x00007354 File Offset: 0x00005554
		public Mesh Get()
		{
			if (this._mesh != null)
			{
				return this._mesh;
			}
			Mesh mesh = new Mesh();
			mesh.name = "ArmorRestored " + this.Name;
			if (this.V.Length > 65000)
			{
				mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			}
			mesh.vertices = this.V;
			if (this.N.Length == this.V.Length)
			{
				mesh.normals = this.N;
			}
			if (this.T.Length == this.V.Length)
			{
				mesh.tangents = this.T;
			}
			if (this.UV.Length == this.V.Length)
			{
				mesh.uv = this.UV;
			}
			if (this.W.Length == this.V.Length)
			{
				mesh.boneWeights = this.W;
			}
			if (this.Bind.Length > 0)
			{
				mesh.bindposes = this.Bind;
			}
			mesh.subMeshCount = this.Subs.Length;
			for (int i = 0; i < this.Subs.Length; i++)
			{
				mesh.SetTriangles(this.Subs[i], i);
			}
			if (this.N.Length != this.V.Length)
			{
				mesh.RecalculateNormals();
			}
			mesh.RecalculateBounds();
			mesh.UploadMeshData(false);
			Object.DontDestroyOnLoad(mesh);
			this._mesh = mesh;
			this.V = (this.N = null);
			this.T = null;
			this.UV = null;
			this.W = null;
			this.Subs = null;
			return mesh;
		}

		// Token: 0x04000073 RID: 115
		public string Name;

		// Token: 0x04000074 RID: 116
		public Vector3[] V;

		// Token: 0x04000075 RID: 117
		public Vector3[] N;

		// Token: 0x04000076 RID: 118
		public Vector4[] T;

		// Token: 0x04000077 RID: 119
		public Vector2[] UV;

		// Token: 0x04000078 RID: 120
		public BoneWeight[] W;

		// Token: 0x04000079 RID: 121
		public Matrix4x4[] Bind;

		// Token: 0x0400007A RID: 122
		public int[][] Subs;

		// Token: 0x0400007B RID: 123
		private Mesh _mesh;
	}
}
