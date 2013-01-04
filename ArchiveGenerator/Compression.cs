using System;
using System.Collections.Generic;
using System.Text;

namespace ArchiveGenerator
{
	public enum CompressionAlgorithm : byte
	{
		None = 0,
		LZF,
	}

	public static class Compression
	{

		#region LZF

		private static class LZF
		{
			private const uint HLOG = 14;
			private const int HSIZE = (1 << 14);
			private const uint MAX_LIT = (1 << 5);
			private const uint MAX_OFF = (1 << 13);
			private const uint MAX_REF = ((1 << 8) + (1 << 3));

			private static readonly Queue<long[]> HashTables = new Queue<long[]>();
			private static long[] GetHashtable()
			{
				long[] res;
				if (HashTables.Count == 0)
					res = new long[HSIZE];
				else
				{
					res = HashTables.Dequeue();
					Array.Clear(res, 0, HSIZE);
				}
				return res;
			}

			/*
			* compressed format
			*
			* 000LLLLL <L+1>    ; literal
			* LLLOOOOO oooooooo ; backref L
			* 111OOOOO LLLLLLLL oooooooo ; backref L+7
			*
			*/
			public static unsafe byte[] Compress(byte[] data)
			{
				int iLen = data.Length;
				byte[] strm = new byte[iLen + 4];
				long[] htab = GetHashtable();
				long hslot;
				uint oidx = 4;
				uint iidx = 0;
				long reference;

				uint hval = (uint)(((data[iidx]) << 8) | data[iidx + 1]);
				long off;
				int lit = 0;

				try
				{
					for (; ; )
					{
						if (iidx < iLen - 2)
						{
							hval = (hval << 8) | data[iidx + 2];
							hslot = ((((hval ^ (hval << 5)) >> (int)(3 * 8 - HLOG)) - hval * 5) & (HSIZE - 1));
							reference = htab[hslot];
							htab[hslot] = (long)iidx;

							if ((off = iidx - reference - 1) < MAX_OFF
								&& iidx + 4 < iLen
								&& reference > 0
								&& data[reference + 0] == data[iidx + 0]
								&& data[reference + 1] == data[iidx + 1]
								&& data[reference + 2] == data[iidx + 2]
								)
							{
								/* match found at *reference++ */
								uint len = 2;
								uint maxlen = (uint)iLen - iidx - len;
								maxlen = maxlen > MAX_REF ? MAX_REF : maxlen;

								do
									len++;
								while (len < maxlen && data[reference + len] == data[iidx + len]);

								if (lit != 0)
								{
									strm[oidx++] = (byte)(lit - 1);
									lit = -lit;
									do
										strm[oidx++] = data[iidx + lit];
									while ((++lit) != 0);
								}

								len -= 2;
								iidx++;

								if (len < 7)
								{
									strm[oidx++] = (byte)((off >> 8) + (len << 5));
								}
								else
								{
									strm[oidx++] = (byte)((off >> 8) + (7 << 5));
									strm[oidx++] = (byte)(len - 7);
								}

								strm[oidx++] = (byte)off;

								iidx += len - 1;
								hval = (uint)(((data[iidx]) << 8) | data[iidx + 1]);

								hval = (hval << 8) | data[iidx + 2];
								htab[((((hval ^ (hval << 5)) >> (int)(3 * 8 - HLOG)) - hval * 5) & (HSIZE - 1))] = iidx;
								iidx++;

								hval = (hval << 8) | data[iidx + 2];
								htab[((((hval ^ (hval << 5)) >> (int)(3 * 8 - HLOG)) - hval * 5) & (HSIZE - 1))] = iidx;
								iidx++;
								continue;
							}
						}
						else if (iidx == iLen)
							break;

						/* one more literal byte we must copy */
						lit++;
						iidx++;

						if (lit == MAX_LIT)
						{
							strm[oidx++] = (byte)(MAX_LIT - 1);
							lit = -lit;
							do
							{
								int finIdx = (int)(iidx + lit);
								strm[oidx++] = data[finIdx];
							}
							while ((++lit) != 0);
						}
					}

					HashTables.Enqueue(htab);

					if (lit != 0)
					{
						strm[oidx++] = (byte)(lit - 1);
						lit = -lit;
						do
							strm[oidx++] = data[iidx + lit];
						while ((++lit) != 0);
					}
				}
				catch (IndexOutOfRangeException)
				{
					return null;
				}

				fixed (byte* strm2 = strm)
				{
					*((int*)strm2) = data.Length;
				}

				byte[] outd = new byte[oidx];
				Array.Copy(strm, outd, (int)oidx);
				return outd;
			}
		}

		#endregion

		public static byte[] CompressData(byte[] data, CompressionAlgorithm alg)
		{
			switch (alg)
			{
				case CompressionAlgorithm.None:
					return data;
				case CompressionAlgorithm.LZF:
					return LZF.Compress(data);
				default:
					throw new Exception("Unknown compression algorithm!");
			}
		}

	}
}
