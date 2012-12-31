using System;
using System.Collections.Generic;

namespace Orvid.Linq
{
	public static class IEnumerableExtensions
	{
		public static T FirstOrDefault<T>(this IEnumerable<T> pThis)
		{
			using (var e = pThis.GetEnumerator())
			{
				if (e.MoveNext())
					return e.Current;
				else
					return default(T);
			}
		}

		public static void ForEach<T>(this IEnumerable<T> pThis, Action<T> action)
		{
			foreach (T t in pThis) action(t);
		}

		public static IEnumerable<T> Where<T>(this IEnumerable<T> pThis, Func<T, bool> selector)
		{
			List<T> ret = new List<T>();
			pThis.ForEach(i => { if (selector(i)) ret.Add(i); });
			return ret;
		}
	}
}
