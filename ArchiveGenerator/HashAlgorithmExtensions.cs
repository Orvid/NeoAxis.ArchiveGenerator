using System;
using System.Security.Cryptography;

namespace ArchiveGenerator
{
	public static class HashAlgorithmExtensions
	{
		public static unsafe int ComputeIntHash(this HashAlgorithm alg, byte[] dat)
		{
			int h;
			byte[] hash = alg.ComputeHash(dat);
			fixed(byte* barr = hash)
			{
				int* iarr = (int*)barr;
				h = iarr[0];
				for (int i = 1; i < hash.Length >> 2; i++)
				{
					h ^= iarr[i];
				}
			}
			return h;
		}
	}
}
