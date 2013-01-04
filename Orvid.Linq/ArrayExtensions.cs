using System;
using System.Collections.Generic;
using System.Text;

namespace Orvid.Linq
{
	public static class ArrayExtensions
	{
		public static void Initialize<T>(this T[] pThis, T value)
		{
			for (int i = 0; i < pThis.Length; i++)
				pThis[i] = value;
		}
	}
}
