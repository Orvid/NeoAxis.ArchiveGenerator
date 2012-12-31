using System;

namespace Orvid.Linq
{
	public delegate TRet Func<TRet>();
	public delegate TRet Func<T, TRet>(T arg1);
	public delegate TRet Func<T, T2, TRet>(T arg1, T2 arg2);
	public delegate TRet Func<T, T2, T3, TRet>(T arg1, T2 arg2, T3 arg3);
	public delegate TRet Func<T, T2, T3, T4, TRet>(T arg1, T2 arg2, T3 arg3, T4 arg4);
}
