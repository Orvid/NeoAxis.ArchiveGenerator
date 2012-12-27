using System;
using System.Collections.Generic;
using System.Text;

namespace ArchiveGenerator
{
	internal static class Program
	{
		private static string[] Separate(string[] args)
		{
			List<string> ret = new List<string>(args.Length);

			for (int i = 0; i < args.Length; i++)
			{
				string s = args[i];
				if (s.IndexOf('=') != -1)
				{
					var idx = s.IndexOf('=');
					ret.Add(s.Substring(0, idx));
					ret.Add(s.Substring(idx + 1, s.Length - idx - 1));
				}
				else if (s.IndexOf(':') != -1)
				{
					var idx = s.IndexOf(':');
					ret.Add(s.Substring(0, idx));
					ret.Add(s.Substring(idx + 1, s.Length - idx - 1));
				}
				else
				{
					ret.Add(s);
				}
			}

			return ret.ToArray();
		}

		private static void Main(string[] args)
		{
			var options = new GeneratorOptions();

			args = Separate(args);

			for (int i = 0; i < args.Length; i++)
			{
				// Thanks to a bit of work, these all have the same effect:
				// -o Test
				// --o Test
				// /o Test
				// --O Test
				// -o=Test
				// -o:Test
				// -o="Test"
				// -target-archive Test
				// -target_archive Test
				// -targetarchive Test
				// -TargetArchive Test
				// Eliminating -'s and _'s allows for whatever form of word separation you want.
				string arg = args[i].ToLower().Replace("-", "").Replace("_", "");

				if (arg.StartsWith("/"))
					arg = arg.Substring(1);

				switch (arg)
				{
					// English
					case "targetarchive":
					case "archive":
					case "out":
					case "o":
					// Russian
					case "архива":
					case "а":
						if (!EnsureArgs(args, i, 1))
							return;
						i++;
						options.TargetArchiveFileName = args[i];
						break;

					// English
					case "targetarchiveextension":
					case "fileextension":
					case "extension":
					case "e":
					// Russian
					case "расширение":
					case "р":
						if (!EnsureArgs(args, i, 1))
							return;
						i++;
						options.TargetFileExtension = args[i];
						break;

					// English
					case "targetassemblyname":
					case "targetassembly":
					case "assemblyname":
					case "assembly":
					case "a":
					// Russian
					case "сборка":
					case "с":
						if (!EnsureArgs(args, i, 1))
							return;
						i++;
						options.TargetAssemblyName = args[i];
						break;

					// English
					case "targetnamespace":
					case "namespace":
					case "n":
					// Russian
					case "пространствоимен":
					case "п":
						if (!EnsureArgs(args, i, 1))
							return;
						i++;
						options.TargetNamespace = args[i];
						break;

					// English
					case "targetassemblyversion":
					case "assemblyversion":
					case "version":
					case "ver":
					case "v":
					// Russian
					case "версия":
					case "в":
						if (!EnsureArgs(args, i, 1))
							return;
						i++;
						options.Version = args[i];
						break;

					// We allow both "seperator" and "separator"
					case "directoryseparator":
					case "directoryseperator":
					case "dirseparator":
					case "dirseperator":
					case "separator":
					case "seperator":
					case "s":
						if (!EnsureArgs(args, i, 1))
							return;
						i++;
						switch (args[i].ToLower())
						{
							case "windows":
							case "win":
							case "w":
								options.UseWindowsDirectorySeperator = true;
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
								options.UseWindowsDirectorySeperator = false;
								break;

							default:
								Error("Unknown platform for the directory separator '" + args[i] + "'!");
								return;
						}
						break;

					// Shorthand for "directory-separator windows"
					case "w":
						options.UseWindowsDirectorySeperator = true;
						break;
					// Shorthand for "directory-separator unix"
					case "u":
						options.UseWindowsDirectorySeperator = false;
						break;

					case "mergeduplicatefiles":
                    case "mergeduplicates":
					case "mergeduplicate":
					case "merge":
					case "m":
						options.Optimizations.MergeDuplicateFiles = true;
						break;

					default:
						Error("Unknown argument '" + args[i] + "'!");
						return;
				}
			}

			var gen = new Generator(options);
			gen.Generate();
		}

		private static bool EnsureArgs(string[] args, int curIdx, int requiredArgs)
		{
			bool ret = args.Length > curIdx + requiredArgs;
			if (!ret)
				Error("Expected an argument after '" + args[curIdx] + "'!");
			return ret;
		}

		private static void PrintHelp()
		{

		}

		private static void Error(string msg)
		{
			Console.WriteLine("ERROR: " + msg);
			PrintHelp();
		}
	}
}
