using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArmorRestoredQuest
{
	// Token: 0x02000012 RID: 18
	internal static class Pack
	{
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000050 RID: 80 RVA: 0x00004BE9 File Offset: 0x00002DE9
		internal static bool Loaded
		{
			get
			{
				return Pack.Pieces.Count == 24;
			}
		}

		// Token: 0x06000051 RID: 81 RVA: 0x00004BFC File Offset: 0x00002DFC
		internal static void Load()
		{
			if (Pack.Loaded)
			{
				return;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			Crumb.At("pack: load");
			Pack.Textures.Clear();
			Pack.Materials.Clear();
			Pack.Meshes.Clear();
			Pack.Pieces.Clear();
			using (Stream manifestResourceStream = typeof(Pack).Assembly.GetManifestResourceStream("ArmorRestored.armor.pack"))
			{
				if (manifestResourceStream == null)
				{
					throw new FileNotFoundException("Missing embedded resource ArmorRestored.armor.pack in "
                        + typeof(Pack).Assembly.FullName + ". Embedded resources: ["
                        + string.Join(", ", typeof(Pack).Assembly.GetManifestResourceNames())
                        + "]. Set the pack Build Action to Embedded Resource and LogicalName to ArmorRestored.armor.pack, then rebuild.");
				}
				BinaryReader binaryReader = new BinaryReader(manifestResourceStream);
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
					texDef.Data = binaryReader.ReadBytes(binaryReader.ReadInt32());
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
						Metal = binaryReader.ReadInt32()
					};
					binaryReader.ReadInt32();
					matDef.R = binaryReader.ReadSingle();
					matDef.G = binaryReader.ReadSingle();
					matDef.B = binaryReader.ReadSingle();
					matDef.A = binaryReader.ReadSingle();
					matDef.Metallic = binaryReader.ReadSingle();
					matDef.Gloss = binaryReader.ReadSingle();
					Pack.Materials.Add(matDef);
				}
				num = binaryReader.ReadInt32();
				for (int k = 0; k < num; k++)
				{
					Pack.Meshes.Add(Pack.ReadMesh(binaryReader));
				}
				num = binaryReader.ReadInt32();
				List<PieceDef> list = new List<PieceDef>();
				for (int l = 0; l < num; l++)
				{
					list.Add(Pack.ReadPiece(binaryReader));
				}
				if (list.Count != 24)
				{
					throw new InvalidDataException("armor.pack has " + list.Count.ToString() + " pieces, expected " + 24.ToString());
				}
				Pack.Pieces.AddRange(list);
			}
			Mod.Log.Msg(string.Concat(new string[]
			{
				"armor pack read: ",
				Pack.Textures.Count.ToString(),
				" textures, ",
				Pack.Materials.Count.ToString(),
				" materials, ",
				Pack.Meshes.Count.ToString(),
				" meshes in ",
				stopwatch.ElapsedMilliseconds.ToString(),
				" ms."
			}));
		}

		// Token: 0x06000052 RID: 82 RVA: 0x00004F28 File Offset: 0x00003128
		private static float[] Floats(BinaryReader r, int count)
		{
			float[] array = new float[count];
			for (int i = 0; i < count; i++)
			{
				array[i] = r.ReadSingle();
			}
			return array;
		}

		// Token: 0x06000053 RID: 83 RVA: 0x00004F54 File Offset: 0x00003154
		private static MeshDef ReadMesh(BinaryReader r)
		{
			MeshDef meshDef = new MeshDef
			{
				Name = Pack.Str(r)
			};
			meshDef.V = Pack.Floats(r, r.ReadInt32() * 3);
			meshDef.N = Pack.Floats(r, r.ReadInt32() * 3);
			meshDef.T = Pack.Floats(r, r.ReadInt32() * 4);
			meshDef.UV = Pack.Floats(r, r.ReadInt32() * 2);
			int num = r.ReadInt32();
			meshDef.Indices = new int[num * 4];
			meshDef.Weights = new float[num * 4];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					meshDef.Indices[i * 4 + j] = r.ReadInt32();
				}
				for (int k = 0; k < 4; k++)
				{
					meshDef.Weights[i * 4 + k] = r.ReadSingle();
				}
			}
			meshDef.Bind = Pack.Floats(r, r.ReadInt32() * 16);
			num = r.ReadInt32();
			meshDef.Subs = new int[num][];
			for (int l = 0; l < num; l++)
			{
				int num2 = r.ReadInt32();
				int[] array = new int[num2];
				for (int m = 0; m < num2; m++)
				{
					array[m] = r.ReadInt32();
				}
				meshDef.Subs[l] = array;
				meshDef.Tris += num2 / 3;
			}
			return meshDef;
		}

		// Token: 0x06000054 RID: 84 RVA: 0x000050B8 File Offset: 0x000032B8
		private static int FirstMat(BinaryReader r)
		{
			int num = r.ReadInt32();
			int result = (num > 0) ? r.ReadInt32() : -1;
			for (int i = 1; i < num; i++)
			{
				r.ReadInt32();
			}
			return result;
		}

		// Token: 0x06000055 RID: 85 RVA: 0x000050F0 File Offset: 0x000032F0
		private static PieceDef ReadPiece(BinaryReader r)
		{
			Pack.Str(r);
			Pack.Str(r);
			Pack.Str(r);
			Pack.Str(r);
			PieceDef pieceDef = new PieceDef();
			int num = r.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				PartDef partDef = new PartDef
				{
					Skinned = (r.ReadInt32() == 1),
					Mesh = r.ReadInt32()
				};
				partDef.Mat = Pack.FirstMat(r);
				if (!partDef.Skinned)
				{
					partDef.Bone = Pack.Str(r);
					partDef.Local = Pack.Floats(r, 16);
				}
				else
				{
					partDef.Root = Pack.Str(r);
					int num2 = r.ReadInt32();
					partDef.Bones = new string[num2];
					for (int j = 0; j < num2; j++)
					{
						partDef.Bones[j] = Pack.Str(r);
					}
				}
				pieceDef.Worn.Add(partDef);
			}
			num = r.ReadInt32();
			for (int k = 0; k < num; k++)
			{
				LookPart lookPart = new LookPart
				{
					Mesh = r.ReadInt32()
				};
				lookPart.Mat = Pack.FirstMat(r);
				lookPart.Local = Pack.Floats(r, 16);
				pieceDef.Look.Add(lookPart);
			}
			num = r.ReadInt32();
			for (int l = 0; l < num; l++)
			{
				ColliderDef colliderDef = new ColliderDef
				{
					Shape = r.ReadInt32()
				};
				colliderDef.Local = Pack.Floats(r, 16);
				colliderDef.CX = r.ReadSingle();
				colliderDef.CY = r.ReadSingle();
				colliderDef.CZ = r.ReadSingle();
				if (colliderDef.Shape == 0)
				{
					colliderDef.SX = r.ReadSingle();
					colliderDef.SY = r.ReadSingle();
					colliderDef.SZ = r.ReadSingle();
				}
				else if (colliderDef.Shape == 1)
				{
					colliderDef.Radius = r.ReadSingle();
					colliderDef.Height = r.ReadSingle();
					colliderDef.Direction = r.ReadInt32();
				}
				else
				{
					colliderDef.Radius = r.ReadSingle();
				}
				pieceDef.Colliders.Add(colliderDef);
			}
			return pieceDef;
		}

		// Token: 0x06000056 RID: 86 RVA: 0x00005310 File Offset: 0x00003510
		private static string Str(BinaryReader r)
		{
			int count = r.ReadInt32();
			return Encoding.UTF8.GetString(r.ReadBytes(count));
		}

		// Token: 0x06000057 RID: 87 RVA: 0x00005338 File Offset: 0x00003538
		internal static void Place(Transform t, float[] m)
		{
			Vector3 localPosition = default(Vector3);
			Vector3 vector = default(Vector3);
			Vector3 vector2 = default(Vector3);
			Vector3 vector3 = default(Vector3);
			vector.x = m[0];
			vector.y = m[1];
			vector.z = m[2];
			vector2.x = m[4];
			vector2.y = m[5];
			vector2.z = m[6];
			vector3.x = m[8];
			vector3.y = m[9];
			vector3.z = m[10];
			localPosition.x = m[12];
			localPosition.y = m[13];
			localPosition.z = m[14];
			t.localPosition = localPosition;
			Quaternion quaternion = (Pack.Mag(vector3) > 0.0001f && Pack.Mag(vector2) > 0.0001f) ? Quaternion.LookRotation(vector3, vector2) : Quaternion.identity;
			t.localRotation = quaternion;
			Vector3 vector4 = quaternion * Vector3.right;
			float num = vector.x * vector4.x + vector.y * vector4.y + vector.z * vector4.z;
			Vector3 localScale = default(Vector3);
			localScale.x = Pack.Mag(vector) * ((num < 0f) ? -1f : 1f);
			localScale.y = Pack.Mag(vector2);
			localScale.z = Pack.Mag(vector3);
			t.localScale = localScale;
		}

		// Token: 0x06000058 RID: 88 RVA: 0x000054A4 File Offset: 0x000036A4
		private static float Mag(Vector3 v)
		{
			return (float)Math.Sqrt((double)(v.x * v.x + v.y * v.y + v.z * v.z));
		}

		// Token: 0x06000059 RID: 89 RVA: 0x000054D8 File Offset: 0x000036D8
		internal static Mesh MeshOf(int i)
		{
			MeshDef meshDef = Pack.Meshes[i];
			if (meshDef.Made != null)
			{
				return meshDef.Made;
			}
			if (meshDef.V == null)
			{
				return null;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = meshDef.V.Length / 3;
			Crumb.First("new Mesh");
			Mesh mesh = new Mesh();
			mesh.name = "ArmorRestored " + meshDef.Name;
			Vector3[] array = new Vector3[num];
			for (int j = 0; j < num; j++)
			{
				array[j].x = meshDef.V[j * 3];
				array[j].y = meshDef.V[j * 3 + 1];
				array[j].z = meshDef.V[j * 3 + 2];
			}
			Crumb.First("Mesh.vertices");
			mesh.vertices = new Il2CppStructArray<Vector3>(array);
			if (meshDef.N.Length == meshDef.V.Length)
			{
				Vector3[] array2 = new Vector3[num];
				for (int k = 0; k < num; k++)
				{
					array2[k].x = meshDef.N[k * 3];
					array2[k].y = meshDef.N[k * 3 + 1];
					array2[k].z = meshDef.N[k * 3 + 2];
				}
				mesh.normals = new Il2CppStructArray<Vector3>(array2);
			}
			if (meshDef.T.Length == num * 4)
			{
				Vector4[] array3 = new Vector4[num];
				for (int l = 0; l < num; l++)
				{
					array3[l].x = meshDef.T[l * 4];
					array3[l].y = meshDef.T[l * 4 + 1];
					array3[l].z = meshDef.T[l * 4 + 2];
					array3[l].w = meshDef.T[l * 4 + 3];
				}
				mesh.tangents = new Il2CppStructArray<Vector4>(array3);
			}
			if (meshDef.UV.Length == num * 2)
			{
				Vector2[] array4 = new Vector2[num];
				for (int m = 0; m < num; m++)
				{
					array4[m].x = meshDef.UV[m * 2];
					array4[m].y = meshDef.UV[m * 2 + 1];
				}
				mesh.uv = new Il2CppStructArray<Vector2>(array4);
			}
			if (meshDef.Weights.Length == num * 4)
			{
				Crumb.First("Mesh.boneWeights");
				BoneWeight[] array5 = new BoneWeight[num];
				for (int n = 0; n < num; n++)
				{
					int num2 = n * 4;
					array5[n].m_BoneIndex0 = meshDef.Indices[num2];
					array5[n].m_BoneIndex1 = meshDef.Indices[num2 + 1];
					array5[n].m_BoneIndex2 = meshDef.Indices[num2 + 2];
					array5[n].m_BoneIndex3 = meshDef.Indices[num2 + 3];
					array5[n].m_Weight0 = meshDef.Weights[num2];
					array5[n].m_Weight1 = meshDef.Weights[num2 + 1];
					array5[n].m_Weight2 = meshDef.Weights[num2 + 2];
					array5[n].m_Weight3 = meshDef.Weights[num2 + 3];
				}
				mesh.boneWeights = new Il2CppStructArray<BoneWeight>(array5);
			}
			if (meshDef.Bind.Length != 0)
			{
				Crumb.First("Mesh.bindposes");
				int num3 = meshDef.Bind.Length / 16;
				Matrix4x4[] array6 = new Matrix4x4[num3];
				for (int num4 = 0; num4 < num3; num4++)
				{
					array6[num4] = Pack.RowMajor(meshDef.Bind, num4 * 16);
				}
				mesh.bindposes = new Il2CppStructArray<Matrix4x4>(array6);
			}
			mesh.subMeshCount = meshDef.Subs.Length;
			Crumb.First("Mesh.SetTriangles");
			for (int num5 = 0; num5 < meshDef.Subs.Length; num5++)
			{
				mesh.SetTriangles(new Il2CppStructArray<int>(meshDef.Subs[num5]), num5);
			}
			if (meshDef.N.Length != meshDef.V.Length)
			{
				mesh.RecalculateNormals();
			}
			mesh.RecalculateBounds();
			mesh.UploadMeshData(false);
			mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
			meshDef.Made = mesh;
			meshDef.V = (meshDef.N = (meshDef.T = (meshDef.UV = (meshDef.Weights = (meshDef.Bind = null)))));
			meshDef.Indices = null;
			meshDef.Subs = null;
			if (Settings.Debug && stopwatch.ElapsedMilliseconds > 20L)
			{
				Mod.Log.Msg(string.Concat(new string[]
				{
					"mesh ",
					meshDef.Name,
					": ",
					num.ToString(),
					" vertices in ",
					stopwatch.ElapsedMilliseconds.ToString(),
					" ms"
				}));
			}
			return mesh;
		}

		// Token: 0x0600005A RID: 90 RVA: 0x000059F4 File Offset: 0x00003BF4
		private static Matrix4x4 RowMajor(float[] a, int o)
		{
			Matrix4x4 result = default(Matrix4x4);
			result.m00 = a[o];
			result.m01 = a[o + 1];
			result.m02 = a[o + 2];
			result.m03 = a[o + 3];
			result.m10 = a[o + 4];
			result.m11 = a[o + 5];
			result.m12 = a[o + 6];
			result.m13 = a[o + 7];
			result.m20 = a[o + 8];
			result.m21 = a[o + 9];
			result.m22 = a[o + 10];
			result.m23 = a[o + 11];
			result.m30 = a[o + 12];
			result.m31 = a[o + 13];
			result.m32 = a[o + 14];
			result.m33 = a[o + 15];
			return result;
		}

		// Token: 0x0600005B RID: 91 RVA: 0x00005AD0 File Offset: 0x00003CD0
		private static Texture2D TextureOf(int i, int kind)
		{
			if (i < 0 || i >= Pack.Textures.Count)
			{
				return null;
			}
			TexDef texDef = Pack.Textures[i];
			if (texDef.Made != null || texDef.Failed || texDef.Data == null)
			{
				return texDef.Made;
			}
			if (texDef.Format != 10 && texDef.Format != 12)
			{
				texDef.Failed = true;
				Mod.Log.Warning(string.Concat(new string[]
				{
					"texture ",
					texDef.Name,
					" has format ",
					texDef.Format.ToString(),
					", not DXT1 or DXT5; left out."
				}));
				return null;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = (texDef.W >= 1024 && texDef.Mips > 1) ? 1 : 0;
			int num2 = 0;
			int num3 = texDef.W;
			int num4 = texDef.H;
			for (int j = 0; j < num; j++)
			{
				num2 += Dxt.Size(num3, num4, texDef.Format);
				num3 = Math.Max(1, num3 / 2);
				num4 = Math.Max(1, num4 / 2);
			}
			byte[] array = Dxt.Decode(texDef.Data, num2, num3, num4, texDef.Format);
			if (kind == 2)
			{
				Dxt.NormalFromAG(array);
			}
			byte[] array2 = Dxt.MipChain(array, num3, num4);
			Crumb.First("new Texture2D");
			Texture2D texture2D = new Texture2D(num3, num4, TextureFormat.RGBA32, true, kind != 0);
			texture2D.name = "ArmorRestored " + texDef.Name;
			Crumb.First("Texture2D.LoadRawTextureData");
			texture2D.LoadRawTextureData(new Il2CppStructArray<byte>(array2));
			texture2D.Apply(false, true);
			texture2D.hideFlags = HideFlags.DontUnloadUnusedAsset;
			texDef.Made = texture2D;
			texDef.Data = null;
			if (Settings.Debug)
			{
				Mod.Log.Msg(string.Concat(new string[]
				{
					"texture ",
					texDef.Name,
					": DXT",
					(texDef.Format == 10) ? "1" : "5",
					" ",
					texDef.W.ToString(),
					" decoded at ",
					num3.ToString(),
					"x",
					num4.ToString(),
					(kind == 2) ? " as a normal map" : ((kind == 1) ? " as data" : ""),
					" in ",
					stopwatch.ElapsedMilliseconds.ToString(),
					" ms"
				}));
			}
			return texture2D;
		}

		// Token: 0x0600005C RID: 92 RVA: 0x00005D60 File Offset: 0x00003F60
		internal static Material MaterialOf(int i)
		{
			if (i < 0 || i >= Pack.Materials.Count)
			{
				return null;
			}
			MatDef matDef = Pack.Materials[i];
			if (matDef.Made != null)
			{
				return matDef.Made;
			}
			Pack.LookUp();
			bool flag = matDef.Name == "Metal Armor";
			Texture2D texture2D = Pack.TextureOf(matDef.Main, 0);
			Texture2D texture2D2 = flag ? null : Pack.TextureOf(matDef.Metal, 1);
			Texture2D texture2D3 = Pack.TextureOf(matDef.Normal, 2);
			Material material = Pack.Templates[((texture2D2 != null) ? 1 : 0) + ((texture2D3 != null) ? 2 : 0)];
			Material material2 = null;
			if (material != null)
			{
				Crumb.First("Instantiate material");
				Object @object = Object.Instantiate(material);
				material2 = ((@object == null) ? null : @object.TryCast<Material>());
			}
			if (material2 == null && Pack._simple != null)
			{
				Crumb.First("new Material");
				material2 = new Material(Pack._simple);
			}
			if (material2 == null)
			{
				return null;
			}
			material2.name = "ArmorRestored " + matDef.Name;
			material2.hideFlags = HideFlags.DontUnloadUnusedAsset;
			Color c = default(Color);
			c.r = matDef.R;
			c.g = matDef.G;
			c.b = matDef.B;
			c.a = matDef.A;
			Pack.SetColor(material2, "_Color", c);
			if (material2.HasProperty("_MainTex"))
			{
				material2.SetTexture("_MainTex", texture2D);
			}
			if (texture2D2 != null)
			{
				material2.SetTexture("_MetallicGlossMap", texture2D2);
				Pack.Switch(material2, "_MetallicMap", "_UseMetallicMap", true);
				Pack.SetIf(material2, "_Metallic", 1f);
				Pack.SetIf(material2, "_Glossiness", matDef.Gloss);
			}
			else
			{
				Pack.Switch(material2, "_MetallicMap", "_UseMetallicMap", false);
				Pack.SetIf(material2, "_Metallic", flag ? 0.45f : matDef.Metallic);
				Pack.SetIf(material2, "_Glossiness", flag ? 0.4f : matDef.Gloss);
			}
			if (texture2D3 != null)
			{
				material2.SetTexture("_BumpMap", texture2D3);
				Pack.SetIf(material2, "_BumpScale", 1f);
			}
			Pack.Switch(material2, "_Normal", "_UseNormal", texture2D3 != null);
			Pack.Switch(material2, "_ScaledNormal", "_UseScaledNormal", false);
			if (texture2D3 == null)
			{
				material2.DisableKeyword("_NORMALMAP");
			}
			Pack.Switch(material2, "_AlphaSmooth", "_UseAlphaSmooth", false);
			Pack.Switch(material2, "_AlphaEmission", "_UseAlphaEmission", false);
			material2.DisableKeyword("_EMISSION");
			material2.DisableKeyword("_ALPHATEST_ON");
			Pack.SetColor(material2, "_EmissionColor", default(Color));
			Pack.SetIf(material2, "_Cull", 2f);
			Pack.SetIf(material2, "_ZWrite", 1f);
			Pack.SetIf(material2, "_SrcBlend", 1f);
			Pack.SetIf(material2, "_DstBlend", 0f);
			material2.renderQueue = 2000;
			matDef.Made = material2;
			if (Settings.Debug)
			{
				Mod.Log.Msg(string.Concat(new string[]
				{
					"material ",
					matDef.Name,
					": ",
					(material != null) ? ("copy of " + material.name) : "new",
					", keywords ",
					Pack.Keywords(material2),
					", main ",
					Pack.Has(texture2D),
					", metallic map ",
					Pack.Has(texture2D2),
					", normal map ",
					Pack.Has(texture2D3)
				}));
			}
			return material2;
		}

		// Token: 0x0600005D RID: 93 RVA: 0x00006150 File Offset: 0x00004350
		private static string Has(Texture2D t)
		{
			if (!(t == null))
			{
				return t.width.ToString() + "x" + t.height.ToString();
			}
			return "none";
		}

		// Token: 0x0600005E RID: 94 RVA: 0x00006192 File Offset: 0x00004392
		private static void SetColor(Material m, string property, Color c)
		{
			if (m.HasProperty(property))
			{
				m.SetColor(property, c);
			}
		}

		// Token: 0x0600005F RID: 95 RVA: 0x000061A5 File Offset: 0x000043A5
		private static void Switch(Material m, string property, string keyword, bool on)
		{
			if (m.HasProperty(property))
			{
				m.SetFloat(property, on ? 1f : 0f);
			}
			if (on)
			{
				m.EnableKeyword(keyword);
				return;
			}
			m.DisableKeyword(keyword);
		}

		// Token: 0x06000060 RID: 96 RVA: 0x000061D8 File Offset: 0x000043D8
		private static void SetIf(Material m, string property, float v)
		{
			if (m.HasProperty(property))
			{
				m.SetFloat(property, v);
			}
		}

		// Token: 0x06000061 RID: 97 RVA: 0x000061EC File Offset: 0x000043EC
		private static string Keywords(Material m)
		{
			List<string> list = new List<string>();
			foreach (string text in Pack.Known)
			{
				if (m.IsKeywordEnabled(text))
				{
					list.Add(text);
				}
			}
			if (list.Count != 0)
			{
				return string.Join(" ", list.ToArray());
			}
			return "none";
		}

		// Token: 0x06000062 RID: 98 RVA: 0x00006248 File Offset: 0x00004448
		private static void LookUp()
		{
			if (Pack._looked)
			{
				return;
			}
			Pack._looked = true;
			Crumb.At("material lookup");
			Pack._simple = Shader.Find("Township Tale/Cheap/SimpleStandard");
			int[] array = new int[]
			{
				int.MaxValue,
				int.MaxValue,
				int.MaxValue,
				int.MaxValue
			};
			int num = 0;
			foreach (Object @object in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>()))
			{
				Material material = (@object == null) ? null : @object.TryCast<Material>();
				Shader shader = (material == null) ? null : material.shader;
				if (!(shader == null) && !(shader.name != "Township Tale/Cheap/SimpleStandard"))
				{
					if (Pack._simple == null)
					{
						Pack._simple = shader;
					}
					if (material.renderQueue < 2450 && !material.IsKeywordEnabled("_UseAlphaSmooth") && !material.IsKeywordEnabled("_ALPHATEST_ON"))
					{
						string name = material.name;
						if (name != null && !name.StartsWith("ArmorRestored", StringComparison.Ordinal) && name.IndexOf("Font", StringComparison.OrdinalIgnoreCase) < 0)
						{
							num++;
							int num2 = material.IsKeywordEnabled("_UseMetallicMap") ? 1 : 0;
							bool flag = material.IsKeywordEnabled("_UseNormal");
							int num3 = ((num2 != 0) ? 1 : 0) + (flag ? 2 : 0);
							int num4 = (material.IsKeywordEnabled("_EMISSION") ? 2 : 0) + ((material.IsKeywordEnabled("_UseScaledNormal")) ? 1 : 0) + (material.IsKeywordEnabled("_UseAlphaEmission") ? 2 : 0);
							if (num4 < array[num3])
							{
								array[num3] = num4;
								Pack.Templates[num3] = material;
							}
						}
					}
				}
			}
			for (int i = 0; i < Pack.Templates.Length; i++)
			{
				if (!(Pack.Templates[i] != null))
				{
					int num5 = 0;
					while (num5 < Pack.Templates.Length && Pack.Templates[i] == null)
					{
						if ((num5 & i) == i && Pack.Templates[num5] != null)
						{
							Pack.Templates[i] = Pack.Templates[num5];
						}
						num5++;
					}
					if (Pack.Templates[i] == null)
					{
						Pack.Templates[i] = Pack.Templates[0];
					}
				}
			}
			Mod.Log.Msg(string.Concat(new string[]
			{
				"shader Township Tale/Cheap/SimpleStandard",
				(Pack._simple == null) ? " NOT found" : " found",
				"; ",
				num.ToString(),
				" opaque materials use it; templates: plain ",
				Pack.Name(Pack.Templates[0]),
				", metallic map ",
				Pack.Name(Pack.Templates[1]),
				", normal map ",
				Pack.Name(Pack.Templates[2]),
				", both ",
				Pack.Name(Pack.Templates[3]),
				"."
			}));
		}

		// Token: 0x06000063 RID: 99 RVA: 0x00006570 File Offset: 0x00004770
		private static string Name(Material m)
		{
			if (!(m == null))
			{
				return m.name;
			}
			return "none";
		}

		// Token: 0x04000079 RID: 121
		private const string Simple = "Township Tale/Cheap/SimpleStandard";

		// Token: 0x0400007A RID: 122
		private const int Color = 0;

		// Token: 0x0400007B RID: 123
		private const int Data = 1;

		// Token: 0x0400007C RID: 124
		private const int NormalMap = 2;

		// Token: 0x0400007D RID: 125
		internal static readonly List<TexDef> Textures = new List<TexDef>();

		// Token: 0x0400007E RID: 126
		internal static readonly List<MatDef> Materials = new List<MatDef>();

		// Token: 0x0400007F RID: 127
		internal static readonly List<MeshDef> Meshes = new List<MeshDef>();

		// Token: 0x04000080 RID: 128
		internal static readonly List<PieceDef> Pieces = new List<PieceDef>();

		// Token: 0x04000081 RID: 129
		private static Shader _simple;

		// Token: 0x04000082 RID: 130
		private static readonly Material[] Templates = new Material[4];

		// Token: 0x04000083 RID: 131
		private static bool _looked;

		// Token: 0x04000084 RID: 132
		private static readonly string[] Known = new string[]
		{
			"_UseMetallicMap",
			"_UseNormal",
			"_UseScaledNormal",
			"_NORMALMAP",
			"_UseAlphaSmooth",
			"_UseAlphaEmission",
			"_EMISSION",
			"_ALPHATEST_ON",
			"_LIGHTINGEXTRA_ON",
			"_SPECULARHIGHLIGHTS_OFF"
		};
	}
}
