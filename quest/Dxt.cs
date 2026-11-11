using System;

namespace ArmorRestoredQuest
{
	// Token: 0x02000006 RID: 6
	internal static class Dxt
	{
		// Token: 0x0600001F RID: 31 RVA: 0x00003262 File Offset: 0x00001462
		internal static int Size(int w, int h, int format)
		{
			return Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * ((format == 10) ? 8 : 16);
		}

		// Token: 0x06000020 RID: 32 RVA: 0x00003288 File Offset: 0x00001488
		internal static byte[] Decode(byte[] data, int offset, int w, int h, int format)
		{
			byte[] array = new byte[w * h * 4];
			int num = Math.Max(1, (w + 3) / 4);
			int num2 = Math.Max(1, (h + 3) / 4);
			int num3 = (format == 10) ? 8 : 16;
			byte[] array2 = new byte[16];
			byte[] array3 = new byte[16];
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					int num4 = offset + (i * num + j) * num3;
					int num5 = num4;
					if (format == 12)
					{
						Dxt.Alpha(data, num4, array3);
						num5 = num4 + 8;
					}
					Dxt.ColorBlock(data, num5, array2, format == 10);
					uint num6 = BitConverter.ToUInt32(data, num5 + 4);
					for (int k = 0; k < 4; k++)
					{
						int num7 = i * 4 + k;
						if (num7 >= h)
						{
							break;
						}
						for (int l = 0; l < 4; l++)
						{
							int num8 = j * 4 + l;
							if (num8 < w)
							{
								int num9 = k * 4 + l;
								int num10 = (int)(num6 >> num9 * 2 & 3U);
								int num11 = (num7 * w + num8) * 4;
								array[num11] = array2[num10 * 4];
								array[num11 + 1] = array2[num10 * 4 + 1];
								array[num11 + 2] = array2[num10 * 4 + 2];
								array[num11 + 3] = ((format == 12) ? array3[num9] : array2[num10 * 4 + 3]);
							}
						}
					}
				}
			}
			return array;
		}

		// Token: 0x06000021 RID: 33 RVA: 0x000033F4 File Offset: 0x000015F4
		private static void ColorBlock(byte[] d, int o, byte[] c, bool dxt1)
		{
			int num = (int)d[o] | (int)d[o + 1] << 8;
			int num2 = (int)d[o + 2] | (int)d[o + 3] << 8;
			Dxt.Rgb(num, c, 0);
			Dxt.Rgb(num2, c, 4);
			if (num > num2 || !dxt1)
			{
				for (int i = 0; i < 3; i++)
				{
					c[8 + i] = (byte)((2 * c[i] + c[4 + i]) / 3);
					c[12 + i] = (byte)((c[i] + 2 * c[4 + i]) / 3);
				}
				c[11] = byte.MaxValue;
				c[15] = byte.MaxValue;
				return;
			}
			for (int j = 0; j < 3; j++)
			{
				c[8 + j] = (byte)((c[j] + c[4 + j]) / 2);
				c[12 + j] = 0;
			}
			c[11] = byte.MaxValue;
			c[15] = 0;
		}

		// Token: 0x06000022 RID: 34 RVA: 0x000034A8 File Offset: 0x000016A8
		private static void Rgb(int v, byte[] c, int o)
		{
			int num = v >> 11 & 31;
			int num2 = v >> 5 & 63;
			int num3 = v & 31;
			c[o] = (byte)(num << 3 | num >> 2);
			c[o + 1] = (byte)(num2 << 2 | num2 >> 4);
			c[o + 2] = (byte)(num3 << 3 | num3 >> 2);
			c[o + 3] = byte.MaxValue;
		}

		// Token: 0x06000023 RID: 35 RVA: 0x000034F8 File Offset: 0x000016F8
		private static void Alpha(byte[] d, int o, byte[] a)
		{
			int num = (int)d[o];
			int num2 = (int)d[o + 1];
			int[] array = new int[8];
			array[0] = num;
			array[1] = num2;
			if (num > num2)
			{
				for (int i = 1; i < 7; i++)
				{
					array[i + 1] = ((7 - i) * num + i * num2) / 7;
				}
			}
			else
			{
				for (int j = 1; j < 5; j++)
				{
					array[j + 1] = ((5 - j) * num + j * num2) / 5;
				}
				array[6] = 0;
				array[7] = 255;
			}
			ulong num3 = 0UL;
			for (int k = 0; k < 6; k++)
			{
				num3 |= (ulong)d[o + 2 + k] << 8 * k;
			}
			for (int l = 0; l < 16; l++)
			{
				a[l] = (byte)array[(int)(num3 >> 3 * l & 7UL)];
			}
		}

		// Token: 0x06000024 RID: 36 RVA: 0x000035C4 File Offset: 0x000017C4
		internal static void NormalFromAG(byte[] rgba)
		{
			for (int i = 0; i < rgba.Length; i += 4)
			{
				float num = (float)rgba[i + 3] / 127.5f - 1f;
				float num2 = (float)rgba[i + 1] / 127.5f - 1f;
				float num3 = 1f - num * num - num2 * num2;
				float num4 = (num3 > 0f) ? ((float)Math.Sqrt((double)num3)) : 0f;
				rgba[i] = rgba[i + 3];
				rgba[i + 2] = (byte)Math.Min(255, (int)((num4 + 1f) * 127.5f + 0.5f));
				rgba[i + 3] = byte.MaxValue;
			}
		}

		// Token: 0x06000025 RID: 37 RVA: 0x0000366C File Offset: 0x0000186C
        internal static byte[] HalfRgba(byte[] source,int width,int height)
        {
            if(width<1 || height<1 || source==null || source.Length<checked(width*height*4)) throw new ArgumentException("Invalid RGBA image");
            int w=Math.Max(1,width/2),h=Math.Max(1,height/2);var result=new byte[checked(w*h*4)];
            for(int y=0;y<h;y++) for(int x=0;x<w;x++) for(int c=0;c<4;c++)
            {
                int x0=Math.Min(width-1,x*2),x1=Math.Min(width-1,x*2+1);
                int y0=Math.Min(height-1,y*2),y1=Math.Min(height-1,y*2+1);
                result[(y*w+x)*4+c]=(byte)((source[(y0*width+x0)*4+c]+source[(y0*width+x1)*4+c]+source[(y1*width+x0)*4+c]+source[(y1*width+x1)*4+c]+2)/4);
            }
            return result;
        }

		internal static byte[] MipChain(byte[] top, int w, int h)
		{
			int num = 0;
			int num2 = w;
			int num3 = h;
			for (;;)
			{
				num += num2 * num3 * 4;
				if (num2 == 1 && num3 == 1)
				{
					break;
				}
				num2 = Math.Max(1, num2 / 2);
				num3 = Math.Max(1, num3 / 2);
			}
			byte[] array = new byte[num];
			Buffer.BlockCopy(top, 0, array, 0, top.Length);
			int num4 = 0;
			int num5 = top.Length;
			int num6 = w;
			int num7 = h;
			while (num6 > 1 || num7 > 1)
			{
				int num8 = Math.Max(1, num6 / 2);
				int num9 = Math.Max(1, num7 / 2);
				for (int i = 0; i < num9; i++)
				{
					for (int j = 0; j < num8; j++)
					{
						int num10 = Math.Min(num6 - 1, j * 2);
						int num11 = Math.Min(num6 - 1, j * 2 + 1);
						int num12 = Math.Min(num7 - 1, i * 2);
						int num13 = Math.Min(num7 - 1, i * 2 + 1);
						for (int k = 0; k < 4; k++)
						{
							int num14 = (int)(array[num4 + (num12 * num6 + num10) * 4 + k] + array[num4 + (num12 * num6 + num11) * 4 + k] + array[num4 + (num13 * num6 + num10) * 4 + k] + array[num4 + (num13 * num6 + num11) * 4 + k]);
							array[num5 + (i * num8 + j) * 4 + k] = (byte)((num14 + 2) / 4);
						}
					}
				}
				num4 = num5;
				num5 += num8 * num9 * 4;
				num6 = num8;
				num7 = num9;
			}
			return array;
		}
	}
}
