using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArmorRestored
{
	// Token: 0x0200001A RID: 26
	internal static class Pack
	{
		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000077 RID: 119 RVA: 0x0000751B File Offset: 0x0000571B
		internal static bool Loaded
		{
			get
			{
				return Pack.Pieces.Count == Catalog.Count;
			}
		}

		// Token: 0x06000078 RID: 120 RVA: 0x0000752C File Offset: 0x0000572C
		internal static void Load(bool visuals)
		{
			if (Pack.Loaded)
			{
				return;
			}
			Stream manifestResourceStream = typeof(Pack).Assembly.GetManifestResourceStream("ArmorRestored.armor.pack");
			if (manifestResourceStream == null)
			{
				throw new FileNotFoundException("Missing embedded resource ArmorRestored.armor.pack in "
                        + typeof(Pack).Assembly.FullName + ". Embedded resources: ["
                        + string.Join(", ", typeof(Pack).Assembly.GetManifestResourceNames())
                        + "]. Set the pack Build Action to Embedded Resource and LogicalName to ArmorRestored.armor.pack, then rebuild.");
			}
			using (BinaryReader binaryReader = new BinaryReader(manifestResourceStream))
			{
				if (new string(binaryReader.ReadChars(4)) != "ARMR" || binaryReader.ReadInt32() != 1)
				{
					throw new InvalidDataException("armor.pack is not version 1");
				}
				int num = binaryReader.ReadInt32();
				for (int i = 0; i < num; i++)
				{
					TexDef texDef = new TexDef
					{
						Name = Pack.Str(binaryReader),
						Format = binaryReader.ReadInt32(),
						W = binaryReader.ReadInt32(),
						H = binaryReader.ReadInt32(),
						Mips = binaryReader.ReadInt32()
					};
					int num2 = binaryReader.ReadInt32();
					if (visuals)
					{
						texDef.Data = binaryReader.ReadBytes(num2);
					}
					else
					{
						binaryReader.BaseStream.Seek((long)num2, SeekOrigin.Current);
					}
					Pack.Textures.Add(texDef);
				}
				num = binaryReader.ReadInt32();
				for (int j = 0; j < num; j++)
				{
					MatDef matDef = new MatDef
					{
						Name = Pack.Str(binaryReader),
						Main = binaryReader.ReadInt32(),
						Normal = binaryReader.ReadInt32(),
						Metal = binaryReader.ReadInt32(),
						Occlusion = binaryReader.ReadInt32()
					};
					matDef.Color = new Color(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
					matDef.Metallic = binaryReader.ReadSingle();
					matDef.Gloss = binaryReader.ReadSingle();
					if (matDef.Normal >= 0)
					{
						Pack.Textures[matDef.Normal].Linear = true;
					}
					if (matDef.Metal >= 0)
					{
						Pack.Textures[matDef.Metal].Linear = true;
					}
					Pack.Materials.Add(matDef);
				}
				num = binaryReader.ReadInt32();
				for (int k = 0; k < num; k++)
				{
					Pack.Meshes.Add(Pack.ReadMesh(binaryReader, visuals));
				}
				num = binaryReader.ReadInt32();
				if (num != Catalog.Count)
                {
                    throw new InvalidDataException("armor.pack has " + num + " pieces; expected " + Catalog.Count);
                }
				for (int l = 0; l < num; l++)
				{
					Pack.Pieces.Add(Pack.ReadPiece(binaryReader));
				}
			}
		}

		// Token: 0x06000079 RID: 121 RVA: 0x000077A8 File Offset: 0x000059A8
		private static MeshDef ReadMesh(BinaryReader r, bool keep)
		{
			MeshDef meshDef = new MeshDef
			{
				Name = Pack.Str(r)
			};
			int num = r.ReadInt32();
			meshDef.V = new Vector3[num];
			for (int i = 0; i < num; i++)
			{
				meshDef.V[i] = Pack.V3(r);
			}
			num = r.ReadInt32();
			meshDef.N = new Vector3[num];
			for (int j = 0; j < num; j++)
			{
				meshDef.N[j] = Pack.V3(r);
			}
			num = r.ReadInt32();
			meshDef.T = new Vector4[num];
			for (int k = 0; k < num; k++)
			{
				meshDef.T[k] = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
			}
			num = r.ReadInt32();
			meshDef.UV = new Vector2[num];
			for (int l = 0; l < num; l++)
			{
				meshDef.UV[l] = new Vector2(r.ReadSingle(), r.ReadSingle());
			}
			num = r.ReadInt32();
			meshDef.W = new BoneWeight[num];
			for (int m = 0; m < num; m++)
			{
				BoneWeight boneWeight = default(BoneWeight);
				boneWeight.boneIndex0 = r.ReadInt32();
				boneWeight.boneIndex1 = r.ReadInt32();
				boneWeight.boneIndex2 = r.ReadInt32();
				boneWeight.boneIndex3 = r.ReadInt32();
				boneWeight.weight0 = r.ReadSingle();
				boneWeight.weight1 = r.ReadSingle();
				boneWeight.weight2 = r.ReadSingle();
				boneWeight.weight3 = r.ReadSingle();
				meshDef.W[m] = boneWeight;
			}
			num = r.ReadInt32();
			meshDef.Bind = new Matrix4x4[num];
			for (int n = 0; n < num; n++)
			{
				meshDef.Bind[n] = Pack.RowMajor(r);
			}
			num = r.ReadInt32();
			meshDef.Subs = new int[num][];
			for (int num2 = 0; num2 < num; num2++)
			{
				int num3 = r.ReadInt32();
				int[] array = new int[num3];
				for (int num4 = 0; num4 < num3; num4++)
				{
					array[num4] = r.ReadInt32();
				}
				meshDef.Subs[num2] = array;
			}
			if (!keep)
			{
				meshDef.V = null;
				meshDef.N = null;
				meshDef.T = null;
				meshDef.UV = null;
				meshDef.W = null;
				meshDef.Subs = null;
			}
			return meshDef;
		}

		// Token: 0x0600007A RID: 122 RVA: 0x00007A44 File Offset: 0x00005C44
		private static PieceDef ReadPiece(BinaryReader r)
		{
			PieceDef pieceDef = new PieceDef
			{
				Id = Pack.Str(r),
				Set = Pack.Str(r),
				Slot = Pack.Str(r),
				Source = Pack.Str(r)
			};
			int num = r.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				WornPart wornPart = new WornPart
				{
					Skinned = (r.ReadInt32() == 1),
					Mesh = r.ReadInt32()
				};
				wornPart.Mats = Pack.Ints(r);
				if (!wornPart.Skinned)
				{
					wornPart.Bone = Pack.Str(r);
					wornPart.Local = Pack.ColumnMajor(r);
				}
				else
				{
					wornPart.Root = Pack.Str(r);
					int num2 = r.ReadInt32();
					wornPart.Bones = new string[num2];
					for (int j = 0; j < num2; j++)
					{
						wornPart.Bones[j] = Pack.Str(r);
					}
				}
				pieceDef.Worn.Add(wornPart);
			}
			num = r.ReadInt32();
			for (int k = 0; k < num; k++)
			{
				PickupPart pickupPart = new PickupPart
				{
					Mesh = r.ReadInt32()
				};
				pickupPart.Mats = Pack.Ints(r);
				pickupPart.Local = Pack.ColumnMajor(r);
				pieceDef.Pickup.Add(pickupPart);
			}
			num = r.ReadInt32();
			for (int l = 0; l < num; l++)
			{
				ColliderSpec colliderSpec = new ColliderSpec
				{
					Shape = r.ReadInt32()
				};
				colliderSpec.Local = Pack.ColumnMajor(r);
				colliderSpec.Center = Pack.V3(r);
				if (colliderSpec.Shape == 0)
				{
					colliderSpec.Size = Pack.V3(r);
				}
				else if (colliderSpec.Shape == 1)
				{
					colliderSpec.Radius = r.ReadSingle();
					colliderSpec.Height = r.ReadSingle();
					colliderSpec.Direction = r.ReadInt32();
				}
				else
				{
					colliderSpec.Radius = r.ReadSingle();
				}
				pieceDef.Colliders.Add(colliderSpec);
			}
			return pieceDef;
		}

		// Token: 0x0600007B RID: 123 RVA: 0x00007C58 File Offset: 0x00005E58
		private static int[] Ints(BinaryReader r)
		{
			int num = r.ReadInt32();
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = r.ReadInt32();
			}
			return array;
		}

		// Token: 0x0600007C RID: 124 RVA: 0x00007C8C File Offset: 0x00005E8C
		private static string Str(BinaryReader r)
		{
			int count = r.ReadInt32();
			return Encoding.UTF8.GetString(r.ReadBytes(count));
		}

		// Token: 0x0600007D RID: 125 RVA: 0x00007CB1 File Offset: 0x00005EB1
		private static Vector3 V3(BinaryReader r)
		{
			return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
		}

		// Token: 0x0600007E RID: 126 RVA: 0x00007CCC File Offset: 0x00005ECC
		private static Matrix4x4 ColumnMajor(BinaryReader r)
		{
			Matrix4x4 result = default(Matrix4x4);
			for (int i = 0; i < 16; i++)
			{
				result[i] = r.ReadSingle();
			}
			return result;
		}

		// Token: 0x0600007F RID: 127 RVA: 0x00007D00 File Offset: 0x00005F00
		private static Matrix4x4 RowMajor(BinaryReader r)
		{
			Matrix4x4 result = default(Matrix4x4);
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					result[i, j] = r.ReadSingle();
				}
			}
			return result;
		}

		// Token: 0x06000080 RID: 128 RVA: 0x00007D40 File Offset: 0x00005F40
		internal static Material[] Mats(int[] idx)
		{
			Material[] array = new Material[idx.Length];
			for (int i = 0; i < idx.Length; i++)
			{
				array[i] = Pack.Materials[idx[i]].Get();
			}
			return array;
		}

		// Token: 0x04000094 RID: 148
		internal static readonly List<TexDef> Textures = new List<TexDef>();

		// Token: 0x04000095 RID: 149
		internal static readonly List<MatDef> Materials = new List<MatDef>();

		// Token: 0x04000096 RID: 150
		internal static readonly List<MeshDef> Meshes = new List<MeshDef>();

		// Token: 0x04000097 RID: 151
		internal static readonly List<PieceDef> Pieces = new List<PieceDef>();
	}
}
