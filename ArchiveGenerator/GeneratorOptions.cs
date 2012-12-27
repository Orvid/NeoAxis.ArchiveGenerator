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
			private bool mMergeDuplicateFiles = false;
			public bool MergeDuplicateFiles
			{
				get { return mMergeDuplicateFiles; }
				set { mMergeDuplicateFiles = value; }
			}

            private bool mOrderedEmit = true;
            public bool OrderedEmit
            {
                get { return mOrderedEmit; }
                set { mOrderedEmit = value; }
            }

            private string[] mEmitOrder = new string[]
            {
                // Text Files
                ".type",
				".highmaterial",
				".physics",
				".animationtree",
				".modelimport",
				".particle",
				".gui",
				".config",
				".map",
				".block",
				".language",
				".fontdefinition",
				".cg_hlsl",
				".program",
				".shaderbaseextension",
				".compositor",
				".m_aterial",
				".material",
				".txt",
                ".xml",
                // Any extension-less file used in
                // NA is probably a text file.
				"",

                // Library Files
                //
                // Usually only created by the
                // logic system.
				".dll",

				".data",
				".dat",
				".raw",
				".ttf",

                // Model Formats
				".mesh",
				".skeleton",

                // Image Formats
				".bmp",
				".tga",
				".cur",
				".dds",
				".jpg",
				".png",

                // Audio Formats
				".ogg",
				".wav",

                // Video Formats
				".ogv",

                // Model Formats
                // 
                // They shouldn't really be in a 
                // production deployment, but you
                // never know what kind of project
                // someone might be doing.
                ".blend",
                ".3ds",
				".dae",
                ".lwo",
				".max",
                ".obj",
				".x",

                // Cache Formats
				".shadercache",

                // Archive Formats
                ".bz2",
                ".rar",
                ".tar",
			    ".zip",
                ".7z",
                ".gz",
                ".z",

                // Unknown Formats
			    ".bak",
            };
            public string[] EmitOrder
            {
                get { return mEmitOrder; }
                set 
                {
                    if (value == null)
                        throw new ArgumentNullException("value", "Cannot set the emit order to null!");
                    mEmitOrder = value; 
                }
            }
            
		}
		
	}
}
