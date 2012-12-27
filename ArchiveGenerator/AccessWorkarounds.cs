using System;
using System.Reflection;
using Engine.FileSystem.Archives;

namespace ArchiveGenerator
{
	public static class AccessWorkarounds
	{
		private static readonly Type mArchive_GetListFileInfo_Type = typeof(Archive).GetNestedType("GetListFileInfo", BindingFlags.NonPublic);
		public static Type Archive_GetListFileInfo_Type
		{
			get { return mArchive_GetListFileInfo_Type; }
		}
	}
}
