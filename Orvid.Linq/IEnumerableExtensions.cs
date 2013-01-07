using System;
using System.Collections.Generic;

namespace Orvid.Linq
{
	public static class IEnumerableExtensions
	{
		public static int Count<T>(this IEnumerable<T> pThis)
		{
			int i = 0;
			using (var e = pThis.GetEnumerator())
			{
				while (e.MoveNext())
					i++;
			}
			return i;
		}

		public static IEnumerable<T> Distinct<T>(this IEnumerable<T> pThis)
		{
			Dictionary<T, int> ret = new Dictionary<T, int>();
			pThis.ForEach(t => ret[t] = 0);
			return ret.Keys;
		}

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

		public static T Max<T>(this IEnumerable<T> pThis) where T : IComparable<T>
		{
			using (var e = pThis.GetEnumerator())
			{
				if (!e.MoveNext())
					throw new Exception();
				T t = e.Current;
				while (e.MoveNext())
				{
					if (t.CompareTo(e.Current) < 0)
						t = e.Current;
				}
				return t;
			}
		}

		public static T Min<T>(this IEnumerable<T> pThis) where T : IComparable<T>
		{
			using (var e = pThis.GetEnumerator())
			{
				if (!e.MoveNext())
					throw new Exception();
				T t = e.Current;
				while (e.MoveNext())
				{
					if (t.CompareTo(e.Current) > 0)
						t = e.Current;
				}
				return t;
			}
		}

		public static IEnumerable<TResult> Select<T, TResult>(this IEnumerable<T> pThis, Func<T, TResult> selector)
		{
			List<TResult> ret = new List<TResult>();
			pThis.ForEach(t => ret.Add(selector(t)));
			return ret;
		}

		public static IEnumerable<T> Sort<T>(this IEnumerable<T> pThis) where T : IComparable<T>
		{
			List<T> ret = new List<T>();
			using (var e = pThis.GetEnumerator())
			{
				while (e.MoveNext())
				{
					T cur = e.Current;
					if (ret.Count == 0)
						ret.Add(cur);
					else
					{
						int i = 0;
						for (; i < ret.Count; i++)
						{
							int r = ret[i].CompareTo(cur);
							if (r > 0)
							{
								ret.Insert(i, cur);
								break;
							}
						}
						if (i == ret.Count)
							ret.Add(cur);
					}
				}
			}
			return ret;
		}

		public static int Sum(this IEnumerable<int> pThis)
		{
			int ret = 0;
			pThis.ForEach(i => ret += i);
			return ret;
		}

		public static int Sum<T>(this IEnumerable<T> pThis, Func<T, int> selector)
		{
			return pThis.Select(selector).Sum();
		}

		public static T[] ToArray<T>(this IEnumerable<T> pThis)
		{
			T[] ret = new T[pThis.Count()];
			int oIdx = 0;
			pThis.ForEach(t => ret[oIdx++] = t);
			return ret;
		}

		public static IEnumerable<T> Where<T>(this IEnumerable<T> pThis, Func<T, bool> selector)
		{
			List<T> ret = new List<T>();
			pThis.ForEach(i => { if (selector(i)) ret.Add(i); });
			return ret;
		}
	}
}
