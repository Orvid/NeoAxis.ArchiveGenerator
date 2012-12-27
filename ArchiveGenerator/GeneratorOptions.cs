using System;
using System.Collections.Generic;

namespace ArchiveGenerator
{
	public sealed class GeneratorOptions
	{
		public string TargetAssemblyName = "OArchive";
		public string TargetNamespace = "Orvid.Archive";
		public string TargetFileExtension = "oar";
		public string TargetArchiveFileName = "Data";
		public string Version = "0.0.0.1";
		public bool UseWindowsDirectorySeperator = true;

		public readonly OptimizationOptions Optimizations = new OptimizationOptions();

		public sealed class OptimizationOptions
		{
			private bool mMergeDuplicateFiles = true;
			public bool MergeDuplicateFiles
			{
				get { return mMergeDuplicateFiles; }
				set { mMergeDuplicateFiles = value; }
			}
		}
		
	}
}
