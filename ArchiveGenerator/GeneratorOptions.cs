using System;
using System.Collections.Generic;
using Orvid.Config;
using System.ComponentModel;

namespace ArchiveGenerator
{
	public sealed class GeneratorOptions : IManuallyConfigurable
	{
		[Configure]
		[Alias(
			// English
			"target-assembly", 
			"assembly-name", 
			"assembly",
			"a",
			// Russian
			"сборка",
			"с"
		)]
		[Description("The name of the assembly to create for the archive provider.")]
		[DefaultValue("OArchive")]
		public string TargetAssemblyName;

		[Configure]
		[Alias(
			// English
			"namespace",
			"n",
			// Russian
			"пространствоимен",
			"п"
		)]
		[Description("The name of the namespace to create the archive provider in.")]
		[DefaultValue("Orvid.Archive")]
		[ValidationRegex(@"[a-zA-Z][a-zA-Z0-9]*(\.[a-zA-Z][a-zA-Z0-9]*)*")]
		public string TargetNamespace;

		[Configure]
		[Alias(
			// English
			"target-archive-extension",
			"archive-extension",
			"file-extension",
			"extension",
			"e",
			// Russian
			"расширение",
			"р"
		)]
		[Description("The file extension to use for the created archive and archive provider.")]
		[DefaultValue("oar")]
		public string TargetFileExtension;

		[Configure]
		[Alias(
			// English
			"target-archive-file",
			"target-file-name",
			"target-archive",
			"target-file",
			"archive-file-name",
			"archive-file",
			"archive",
			"file-name",
			"file",
			"out",
			"o",
			// Russian
			"архива",
			"а"
		)]
		[Description("The name of the archive to create.")]
		[DefaultValue("Data")]
		public string TargetArchiveFileName;

		[Configure]
		[Alias(
			// English
			"target-assembly-version",
			"target-version",
			"assembly-version",
			"ver",
			"v",
			// Russian
			"версия",
			"в"
		)]
		[Description("The version to mark the archive provider assembly as.")]
		[DefaultValue("0.0.0.1")]
		[ValidationRegex(@"([0-9]{1,4}\.){3}[0-9]{1,4}")]
		public string Version;

		[Configure]
		[Alias(
			"use-windows-directory-separator",
			"windows-directory-seperator",
			"windows-directory-separator"
		)]
		[Description("If true, the windows directory seperator will be used, otherwise the unix one will be used.")]
		[DefaultValue(true)]
		public bool UseWindowsDirectorySeperator = true;

		public ICollection<ConfigItem> ManuallyConfigure(List<ConfigItem> items)
		{
			List<ConfigItem> unknownItems = new List<ConfigItem>();

			foreach (ConfigItem itm in items)
			{
				switch (itm.Name)
				{
					// We allow both "seperator" and "separator"
					case "directoryseparator":
					case "directoryseperator":
					case "dirseparator":
					case "dirseperator":
					case "separator":
					case "seperator":
					case "s":
						switch (itm.Value.ToLower())
						{
							case "windows":
							case "win":
							case "w":
								this.UseWindowsDirectorySeperator = true;
								break;

							case "linux":
							case "lin":
							case "mac":
							case "macos":
							case "osx":
							case "macosx":
							case "android":
							case "droid":
							case "unix":
							case "u":
								this.UseWindowsDirectorySeperator = false;
								break;

							default:
								throw new Exception("Unknown platform for the directory separator '" + itm.Value + "'!");
						}
						break;

					// Shorthand for "directory-separator windows"
					case "w":
						this.UseWindowsDirectorySeperator = true;
						break;
					// Shorthand for "directory-separator unix"
					case "u":
						this.UseWindowsDirectorySeperator = false;
						break;

					default:
						unknownItems.Add(itm);
						break;
				}
			}

			return unknownItems;
		}

		[Flatten]
		public readonly OptimizationOptions Optimizations = new OptimizationOptions();

		public sealed class OptimizationOptions
		{
			[Configure]
			[Alias(
				"merge-duplicates",
				"merge-duplicate",
				"merge-files",
				"merge",
				"m"
			)]
			[Description("If true, duplicate files will reference the same place in the created archive.")]
			[DefaultValue(true)]
			public bool MergeDuplicateFiles;

			[Configure]
			[Alias(
				"minify",
				"min"
			)]
			[Description("If true, characters that aren't required are removed from files that are serialized.")]
			[DefaultValue(true)]
			public bool MinifySerialized;

			[Configure]
			[Alias(
				"ordered"
			)]
			[Description("If true, files will be stored in the archive in such a way that files of the same type end up next to each other, making the resulting archive more compressable.")]
			[DefaultValue(true)]
            public bool OrderedEmit;

			[Configure]
			[Alias(
				"order"
			)]
			[Description("The order that files will be stored in the archive.")]
			[DefaultValue(new string[]
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
            })]
			public string[] EmitOrder;

		}
		
	}
}
