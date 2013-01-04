//#define DisableArrayInit
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Engine.FileSystem;
using Engine.FileSystem.Archives;
using Orvid.Linq;

namespace ArchiveGenerator
{
	public sealed class Generator
	{
		private readonly GeneratorOptions config;

		public Generator(GeneratorOptions options)
		{
			config = options;
		}

		private sealed class FileTreeDescriptor
		{
			public sealed class FileDescriptor
			{
				public readonly string FullName;
				public readonly int Size;
				public readonly FileInfo SourceFile;
				public int InternalSize;
				public long FileOffset;
				public CompressionAlgorithm CompressionAlgorithm = CompressionAlgorithm.None;

				public FileDescriptor(string name, long size, FileInfo file)
				{
					this.FullName = name;
					this.Size = (int)size;
					this.SourceFile = file;
					this.FileOffset = -1;
				}

				public override string ToString()
				{
					return FullName + ": " + Size.ToString();
				}
			}

			private const int InitialDirectoryCapacity = 32;
			private const int InitialFileCapacity = 512;

			private readonly List<string> mDirectories = new List<string>(InitialDirectoryCapacity);
			public List<string> Directories { get { return mDirectories; } }

			private readonly List<FileDescriptor> mFiles = new List<FileDescriptor>(InitialFileCapacity);
			public List<FileDescriptor> Files { get { return mFiles; } }

			private FileTreeDescriptor() { }

			public byte[] GetUsedCompressionAlgorithms()
			{
				return Files.Select(f => (byte)f.CompressionAlgorithm).Distinct().ToArray();
			}

			public byte[] GetCompressionData()
			{
				byte[] ret = new byte[Files.Count];
				for (int i = 0; i < ret.Length; i++)
				{
					ret[i] = (byte)Files[i].CompressionAlgorithm;
				}
				return ret;
			}

			public byte[] GetLengthData()
			{
				byte[] ret = new byte[Files.Count << 2];
				for (int i = 0; i < Files.Count; i++)
				{
					Array.Copy(BitConverter.GetBytes(Files[i].Size), 0, ret, i << 2, 4);
				}
				return ret;
			}

			public byte[] GetInternalLengthData()
			{
				byte[] ret = new byte[Files.Count << 2];
				for (int i = 0; i < Files.Count; i++)
				{
					Array.Copy(BitConverter.GetBytes(Files[i].InternalSize), 0, ret, i << 2, 4);
				}
				return ret;
			}

			public byte[] GetOffsetData()
			{
				byte[] ret = new byte[Files.Count << 3];
				for (int i = 0; i < Files.Count; i++)
				{
					Array.Copy(BitConverter.GetBytes(Files[i].FileOffset), 0, ret, i << 3, 8);
				}
				return ret;
			}

			public byte[] GetFilesData()
			{
				List<byte> ret = new List<byte>(1024 * 512);
				for(int i = 0; i < Files.Count; i++)
				{
					byte[] dat = Encoding.UTF8.GetBytes(Files[i].FullName);
					ret.AddRange(BitConverter.GetBytes(dat.Length));
					ret.AddRange(dat);
				}
				return ret.ToArray();
			}

			public byte[] GetDirectoriesData()
			{
				List<byte> ret = new List<byte>(1024 * 512);
				for (int i = 0; i < Directories.Count; i++)
				{
					byte[] dat = Encoding.UTF8.GetBytes(Directories[i]);
					ret.AddRange(BitConverter.GetBytes(dat.Length));
					ret.AddRange(dat);
				}
				return ret.ToArray();
			}

			public static FileTreeDescriptor GetTree(GeneratorOptions config)
			{
				var desc = new FileTreeDescriptor();
				Directory.SetCurrentDirectory("Data");
				DirectoryInfo d = new DirectoryInfo("./");
				foreach (var sd in d.GetDirectories())
				{
					FillTree(desc, sd.Name, config);
				}
				CreateArchive(desc, config);
				Directory.SetCurrentDirectory("../");
				return desc;
			}

            private static void FillTree(FileTreeDescriptor desc, string curDirectory, GeneratorOptions config)
            {
                if (config.UseWindowsDirectorySeperator)
                    desc.Directories.Add(curDirectory + "\\");
                else
                    desc.Directories.Add(curDirectory + "/");

                DirectoryInfo d = new DirectoryInfo(curDirectory);
                foreach (var f in d.GetFiles())
                {
                    if (config.UseWindowsDirectorySeperator)
                        desc.Files.Add(new FileDescriptor(curDirectory + "\\" + f.Name, f.Length, f));
                    else
                        desc.Files.Add(new FileDescriptor(curDirectory + "/" + f.Name, f.Length, f));
                }
                foreach (var sd in d.GetDirectories())
                {
                    if (config.UseWindowsDirectorySeperator)
                        FillTree(desc, curDirectory + "\\" + sd.Name, config);
                    else
                        FillTree(desc, curDirectory + "/" + sd.Name, config);
                }
            }

			private static void CreateArchive(FileTreeDescriptor tree, GeneratorOptions config)
			{
				string oFNme = config.TargetArchiveFileName + "." + config.TargetFileExtension;
				if (File.Exists(oFNme))
					File.Delete(oFNme);
                Console.WriteLine("Creating '" + oFNme + "' with " + tree.Files.Count + " files");
                TotalFileCount = tree.Files.Count;
				using (var strm = new FileStream(oFNme, FileMode.Create, FileAccess.Write))
				{
					TreeCreationInit(config);

					tree.Files.Sort(
					(a, b) =>
					{
						int ap = GetFilePriority(a.SourceFile);
						int bp = GetFilePriority(b.SourceFile);
						if (ap > bp)
							return 1;
						if (ap < bp)
							return -1;
						int c = StringComparer.CurrentCultureIgnoreCase.Compare(a.SourceFile.Name, b.SourceFile.Name);
						if (c == 0)
							return StringComparer.CurrentCultureIgnoreCase.Compare(a.FullName, b.FullName);
						return c;
					});

					foreach (var v in tree.Files)
					{
                        WriteStartAddingFile(v);
						v.FileOffset = AddFile(strm, v.SourceFile, config, v);
					}

					TreeCreationFinish();
				}
			}

            private static int CurFileNum;
            private static int TotalFileCount;
            private static int TotalFileCountWidth;
            private static void WriteStartAddingFile(FileDescriptor f)
            {
                CurFileNum++;
                Console.Write("(" + CurFileNum.ToString().PadLeft(TotalFileCountWidth, ' ') + "/" + TotalFileCount + ") ");
                Console.Write("Adding " + f.FullName);
            }

			private static bool? mConsoleRedirectedCache;
			private static bool ConsoleRedirected
			{
				get
				{
					if (mConsoleRedirectedCache != null)
						return mConsoleRedirectedCache.Value;
					bool b;
					try
					{
						b = Console.CursorVisible && false;
					}
					catch
					{
						b = true;
					}
					mConsoleRedirectedCache = b;
					return b;
				}
			}

            private static void WriteEndAddingFile(bool merged)
            {
                if (merged)
                {
					if (!ConsoleRedirected)
					{
						Console.CursorLeft = Console.BufferWidth - 7;
						Console.WriteLine("Merged");
					}
					else
					{
						Console.WriteLine("\t\tMerged");
					}
                }
                else
                {
					if (!ConsoleRedirected)
					{
						Console.CursorLeft = Console.BufferWidth - 5;
						Console.WriteLine("Done");
					}
					else
					{
						Console.WriteLine("\t\tDone");
					}
                }
            }

            private static Dictionary<string, int> FileExtensionPriorities;
			private static int GetFilePriority(FileInfo file)
			{
                int p;
				string fileExtension = file.Extension.ToLower();
                if (!FileExtensionPriorities.TryGetValue(fileExtension, out p))
                    return FileExtensionPriorities.Count + 2000;
				if (fileExtension == ".dds")
				{
					string n = Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant();
					if (
						n.EndsWith("normal") ||
						n.EndsWith("normals") ||
						n.EndsWith("n") ||
						n.EndsWith("nrm") ||
						n.EndsWith("normalmap") ||
						n.EndsWith("normalsmap")
					)
					{
						p += 1;
					}
					else if (
						n.EndsWith("ao") ||
						file.Directory.Name.ToLowerInvariant().EndsWith("ao")
					)
					{
						p += 2;
					}
					else if (
						n.EndsWith("spec") ||
						n.EndsWith("s") ||
						n.EndsWith("specular") ||
						n.EndsWith("specularity") ||
						n.EndsWith("specularmap") ||
						n.EndsWith("specmap")
					)
					{
						p += 3;
					}
					else if (
						n == "preview"
					)
					{
						p += 4;
					}
					else if (
						n.EndsWith("emission") 
					)
					{
						p += 5;
					}
					else if (
						n.EndsWith("height") ||
						n.EndsWith("heightmap")
					)
					{
						p += 6;
					}
					else if (
						n.EndsWith("glow") ||
						n.EndsWith("glowmap")
					)
					{
						p += 7;
					}
					else if (
						n.EndsWith("translucency")
					)
					{
						p += 8;
					}
				}
                return p;
			}

            private static void TreeCreationInit(GeneratorOptions config)
            {
                if (config.Optimizations.MergeDuplicateFiles)
                    KnownFiles = new Dictionary<int, Tuple<FileInfo, long>>();
                HashProvider = new SHA512Managed();
                CurFileNum = 0;
                TotalFileCountWidth = TotalFileCount.ToString().Length;
                FileExtensionPriorities = new Dictionary<string, int>(config.Optimizations.EmitOrder.Length);
                for (int i = 0, i2 = 0; i < config.Optimizations.EmitOrder.Length; i++, i2++)
                {
					string fe = config.Optimizations.EmitOrder[i].ToLower();
                    FileExtensionPriorities[fe] = i2;
					if (fe == ".dds")
						i2 += 30;
                }
            }

            private static void TreeCreationFinish()
			{
				KnownFiles = null;
				HashProvider = null;
                CurFileNum = 0;
                TotalFileCount = 0;
                TotalFileCountWidth = 0;
                FileExtensionPriorities = null;
			}

			private static bool CStyleMinifiable(FileInfo file)
			{
				switch (file.Extension.ToLower())
				{
					case ".shaderbaseextension":
					case ".cg_hlsl":
						return true;
					default:
						return false;
				}
			}

			private static bool SerializedMinifiable(FileInfo file)
			{
				switch (file.Extension.ToLower())
				{
					case ".type":
					case ".highmaterial":
					case ".physics":
					case ".animationtree":
					case ".modelimport":
					case ".particle":
					case ".gui":
					case ".config":
					case ".map":
					case ".block":
					case ".language":
					case ".fontdefinition":
						return true;
					default:
						return false;
				}
			}

			private static byte[] DoCompress(FileDescriptor desc, byte[] dat)
			{
				switch (desc.SourceFile.Extension.ToLower())
				{
					case ".type":
					case ".highmaterial":
					case ".physics":
					case ".animationtree":
					case ".modelimport":
					case ".particle":
					case ".gui":
					case ".config":
					case ".map":
					case ".block":
					case ".language":
					case ".fontdefinition":
					case ".cg_hlsl":
					case ".program":
					case ".shaderbaseextension":
					case ".compositor":
					case ".m_aterial":
					case ".material":
					case ".txt":
					case ".xml":
					default:
						var r = Compression.CompressData(dat, CompressionAlgorithm.LZF);
						if (r != null)
						{
							desc.CompressionAlgorithm = CompressionAlgorithm.LZF;
							return r;
						}
						else
						{
							return dat;
						}
					//default:
					//	return dat;
				}
			}

			private static Dictionary<int, Tuple<FileInfo, long>> KnownFiles;
			private static HashAlgorithm HashProvider;
			private static long AddFile(FileStream strm, FileInfo file, GeneratorOptions config, FileDescriptor desc)
			{
				if (!config.Optimizations.MergeDuplicateFiles)
				{
					long off = strm.Position;
					using (var fs = file.OpenRead())
					{
						Stream ifs = fs;
						if (config.Optimizations.Minify)
						{
							if (SerializedMinifiable(file))
								ifs = NeoAxisSerializedFileMinifier.Minify(ifs);
							else if (CStyleMinifiable(file))
								ifs = CStyleMinifier.Minify(ifs);
						}
						byte[] buf = new byte[(int)ifs.Length];
						ifs.Read(buf, 0, buf.Length);
						strm.Write(buf, 0, buf.Length);
						desc.InternalSize = buf.Length;
                    }
                    WriteEndAddingFile(false);
					return off;
				}
				else
				{
					byte[] buf;
					long off = strm.Position;
					using (var fs = file.OpenRead())
					{
						Stream ifs = fs;
						if (config.Optimizations.Minify)
						{
							if (SerializedMinifiable(file))// && file.Name == "PhysicsSystem.config")
								ifs = NeoAxisSerializedFileMinifier.Minify(ifs);
							else if (CStyleMinifiable(file))// && file.Name == "StdQuad_vp.cg_hlsl")
								ifs = CStyleMinifier.Minify(ifs);
						}
						buf = new byte[(int)ifs.Length];
						ifs.Read(buf, 0, buf.Length);
						//if (file.Name == "PhysicsSystem.config")
						//{
						//    using (var ofs = new FileStream("Definitions/PhysicsSystem.minified.config", FileMode.Create))
						//    {
						//        ofs.Write(buf, 0, buf.Length);
						//    }
						//}
						//if (file.Name == "StdQuad_vp.cg_hlsl")
						//{
						//    using (var ofs = new FileStream("Materials/Common/StdQuad_vp.minified.cg_hlsl", FileMode.Create))
						//    {
						//        ofs.Write(buf, 0, buf.Length);
						//    }
						//}
						if (config.Optimizations.CompressText)
						{
							buf = DoCompress(desc, buf);
						}
					}
					desc.InternalSize = buf.Length;
					int hash = HashProvider.ComputeIntHash(buf);
					Tuple<FileInfo, long> kf;
					if (!KnownFiles.TryGetValue(hash, out kf))
					{
						KnownFiles.Add(hash, new Tuple<FileInfo, long>(file, off));
                        strm.Write(buf, 0, buf.Length);
                        WriteEndAddingFile(false);
					}
					else if (!FileDataEqual(kf.ValA, file))
                    {
                        WriteEndAddingFile(false);
						Console.WriteLine("Warning: Hash collision!");
                        strm.Write(buf, 0, buf.Length);
					}
					else
					{
                        off = kf.ValB;
                        WriteEndAddingFile(true);
					}
					return off;
				}
			}

			private static bool FileDataEqual(FileInfo f1, FileInfo f2)
			{
				if (f1.Length != f2.Length)
					return false;
				byte[] bufA = new byte[f1.Length];
				byte[] bufB = new byte[f2.Length];
				using (var fs = f1.OpenRead())
				{
					fs.Read(bufA, 0, bufA.Length);
				}
				using (var fs = f2.OpenRead())
				{
					fs.Read(bufB, 0, bufB.Length);
				}
				for (int i = 0; i < bufA.Length; i++)
				{
					if (bufA[i] != bufB[i])
						return false;
				}
				return true;
			}
		}

		private static CustomAttributeBuilder GetCompilerGeneratedAttribute()
		{
			return new CustomAttributeBuilder(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes), new object[] { });
		}

		public void Generate()
		{
			var asName = new AssemblyName(config.TargetAssemblyName) 
			{
				Version = new Version(config.Version),
				ProcessorArchitecture = ProcessorArchitecture.MSIL,
			};
			var aBldr = AppDomain.CurrentDomain.DefineDynamicAssembly(asName, AssemblyBuilderAccess.Save);
			aBldr.SetCustomAttribute(new CustomAttributeBuilder(typeof(CompilationRelaxationsAttribute).GetConstructor(new Type[] { typeof(int) }), new object[] { (int)CompilationRelaxations.NoStringInterning }));
			aBldr.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyVersionAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { config.Version }));
			aBldr.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyFileVersionAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { config.Version }));
#if DEBUG
			var modBldr = aBldr.DefineDynamicModule(config.TargetAssemblyName, config.TargetAssemblyName + ".dll", true);
#else
			var modBldr = aBldr.DefineDynamicModule(config.TargetAssemblyName, config.TargetAssemblyName + ".dll");
#endif

			#region Generate Archive Type
			var asmTpBldr = modBldr.DefineType(config.TargetNamespace + ".CustomArchive", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(Archive));
			ConstructorBuilder archiveConstructor = null;
			{
				var tree = FileTreeDescriptor.GetTree(config);
				FieldBuilder initCompressionData = null;
				FieldBuilder allCompressionField = null;
				byte[] usedCompressionAlgorithms = null;
				{
					var dat = tree.GetCompressionData();
					if (dat.Where(b => b != 0).Count() > 0)
					{
						usedCompressionAlgorithms = tree.GetUsedCompressionAlgorithms();
						initCompressionData = asmTpBldr.DefineInitializedData("InitData_Compression", dat, FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
						allCompressionField = asmTpBldr.DefineField("AllCompressions", typeof(byte[]), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
					}
				}
				var initInternalLengthData = asmTpBldr.DefineInitializedData("InitData_InternalLength", tree.GetInternalLengthData(), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var allInternalLengthsField = asmTpBldr.DefineField("AllInternalLengths", typeof(int[]), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var initLengthData = asmTpBldr.DefineInitializedData("InitData_Length", tree.GetLengthData(), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var allLengthsField = asmTpBldr.DefineField("AllLengths", typeof(int[]), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var initOffsetData = asmTpBldr.DefineInitializedData("InitData_Offset", tree.GetOffsetData(), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var allOffsetsField = asmTpBldr.DefineField("AllOffsets", typeof(long[]), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				byte[] dirsData = tree.GetDirectoriesData();
				var initDirectoriesData = asmTpBldr.DefineInitializedData("InitData_Directories", dirsData, FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var allDirectoriesField = asmTpBldr.DefineField("AllDirectories", typeof(string[]), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				byte[] filesData = tree.GetFilesData();
				var initFilesData = asmTpBldr.DefineInitializedData("InitData_Files", filesData, FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var allFilesField = asmTpBldr.DefineField("AllFiles", typeof(string[]), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				var fileSwitchDictionaryField = asmTpBldr.DefineField("FileSwitchDictionary", typeof(Dictionary<string, int>), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
				fileSwitchDictionaryField.SetCustomAttribute(GetCompilerGeneratedAttribute());
				var fileField = asmTpBldr.DefineField("file", typeof(FileStream), FieldAttributes.Private);
				{
					archiveConstructor = asmTpBldr.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(ArchiveFactory), typeof(string) });
					const int P_factory = 1;
					archiveConstructor.DefineParameter(P_factory, ParameterAttributes.None, "factory");
					const int P_fileName = 2;
					archiveConstructor.DefineParameter(P_fileName, ParameterAttributes.None, "fileName");
					var gen = archiveConstructor.GetILGenerator();
					gen.LoadThis();
					gen.LoadParameter(P_factory);
					gen.LoadParameter(P_fileName);
					gen.Emit(OpCodes.Call, typeof(Archive).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(ArchiveFactory), typeof(string) }, new ParameterModifier[] { }));
					gen.LoadThis();
					gen.LoadParameter(P_fileName);
					gen.Emit(OpCodes.Ldc_I4_3); // FileMode.Open
					gen.Emit(OpCodes.Ldc_I4_1); // FileAccess.Read
					gen.Emit(OpCodes.Newobj, typeof(FileStream).GetConstructor(new Type[] { typeof(string), typeof(FileMode), typeof(FileAccess) }));
					gen.Emit(OpCodes.Stfld, fileField);
					gen.Emit(OpCodes.Ret);
				}

				{
					var mBldr = asmTpBldr.DefineMethod("Dispose", MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), Type.EmptyTypes);
					var gen = mBldr.GetILGenerator();
					gen.LoadThis();
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.Emit(OpCodes.Call, typeof(FileStream).GetMethod("Dispose"));
					gen.LoadThis();
					gen.Emit(OpCodes.Ldnull);
					gen.Emit(OpCodes.Stfld, fileField);
					gen.Emit(OpCodes.Ret);
				}

				{
					var cBldr = asmTpBldr.DefineTypeInitializer();
					var gen = cBldr.GetILGenerator();
					const int L_i = 0;
					gen.DeclareLocal(typeof(int));
					const int L_data = 1;
					gen.DeclareLocal(typeof(byte[]));
					const int L_i2 = 2;
					gen.DeclareLocal(typeof(int));
					const int L_len = 3;
					gen.DeclareLocal(typeof(int));


					{
#if !DisableArrayInit
						gen.LoadInt((ulong)tree.Files.Count, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(int));
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldtoken, initInternalLengthData);
						gen.Emit(OpCodes.Call, typeof(RuntimeHelpers).GetMethod("InitializeArray"));
						gen.Emit(OpCodes.Stsfld, allInternalLengthsField);
#endif
					}

					{
#if !DisableArrayInit
						gen.LoadInt((ulong)tree.Files.Count, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(int));
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldtoken, initLengthData);
						gen.Emit(OpCodes.Call, typeof(RuntimeHelpers).GetMethod("InitializeArray"));
						gen.Emit(OpCodes.Stsfld, allLengthsField);
#endif
					}

					{
#if !DisableArrayInit
						gen.LoadInt((ulong)tree.Files.Count, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(long));
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldtoken, initOffsetData);
						gen.Emit(OpCodes.Call, typeof(RuntimeHelpers).GetMethod("InitializeArray"));
						gen.Emit(OpCodes.Stsfld, allOffsetsField);
#endif
					}

					if (allCompressionField != null)
					{
#if !DisableArrayInit
						gen.LoadInt((ulong)tree.Files.Count, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(byte));
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldtoken, initCompressionData);
						gen.Emit(OpCodes.Call, typeof(RuntimeHelpers).GetMethod("InitializeArray"));
						gen.Emit(OpCodes.Stsfld, allCompressionField);
#endif
					}

					{
						gen.LoadInt((ulong)tree.Directories.Count, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(string));
						gen.Emit(OpCodes.Stsfld, allDirectoriesField);

						// loc 0 - i
						// loc 1 - data
						// loc 2 - i2
						// loc 3 - len

#if !DisableArrayInit
						gen.LoadInt((ulong)dirsData.Length, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(byte));
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldtoken, initDirectoriesData);
						gen.Emit(OpCodes.Call, typeof(RuntimeHelpers).GetMethod("InitializeArray"));
						gen.StoreLocal(L_data);
#endif

						// int i = 0, i2 = 0;
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.StoreLocal(L_i);
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.StoreLocal(L_i2);
						Label condLbl = gen.DefineLabel();
						Label bodyLbl = gen.DefineLabel();
						gen.Emit(OpCodes.Br_S, condLbl);
						gen.MarkLabel(bodyLbl);

						// int len = BitConverter.ToInt32(dirsDat, i2);
						gen.LoadLocal(L_data);
						gen.LoadLocal(L_i2);
						gen.Emit(OpCodes.Call, typeof(BitConverter).GetMethod("ToInt32", new Type[] { typeof(byte[]), typeof(int) }));
						gen.StoreLocal(L_len);

						// i2 += 4;
						gen.LoadLocal(L_i2);
						gen.Emit(OpCodes.Ldc_I4_4);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_i2);

						// allDirectories[i] = Encoding.UTF8.GetString(dirs, i2, len);
						gen.Emit(OpCodes.Ldsfld, allDirectoriesField);
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Call, typeof(Encoding).GetProperty("UTF8").GetGetMethod());
						gen.LoadLocal(L_data);
						gen.LoadLocal(L_i2);
						gen.LoadLocal(L_len);
						gen.Emit(OpCodes.Callvirt, typeof(Encoding).GetMethod("GetString", new Type[] { typeof(byte[]), typeof(int), typeof(int) }));
						gen.Emit(OpCodes.Stelem_Ref);

						// i2 += len;
						gen.LoadLocal(L_i2);
						gen.LoadLocal(L_len);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_i2);

						// i++;
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_i);

						// i < dirsDat.Length;
						gen.MarkLabel(condLbl);
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Ldsfld, allDirectoriesField);
						gen.Emit(OpCodes.Ldlen);
						gen.Emit(OpCodes.Blt_S, bodyLbl);
					}

					{
						gen.LoadInt((ulong)tree.Files.Count, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(string));
						gen.Emit(OpCodes.Stsfld, allFilesField);

						// loc 0 - i
						// loc 1 - data
						// loc 2 - i2
						// loc 3 - len

#if !DisableArrayInit
						gen.LoadInt((ulong)filesData.Length, 4, false);
						gen.Emit(OpCodes.Newarr, typeof(byte));
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldtoken, initFilesData);
						gen.Emit(OpCodes.Call, typeof(RuntimeHelpers).GetMethod("InitializeArray"));
						gen.StoreLocal(L_data);
#endif

						// int i = 0, i2 = 0;
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.StoreLocal(L_i);
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.StoreLocal(L_i2);
						Label condLbl = gen.DefineLabel();
						Label bodyLbl = gen.DefineLabel();
						gen.Emit(OpCodes.Br_S, condLbl);
						gen.MarkLabel(bodyLbl);

						// int len = BitConverter.ToInt32(filesDat, i2);
						gen.LoadLocal(L_data);
						gen.LoadLocal(L_i2);
						gen.Emit(OpCodes.Call, typeof(BitConverter).GetMethod("ToInt32", new Type[] { typeof(byte[]), typeof(int) }));
						gen.StoreLocal(L_len);

						// i2 += 4;
						gen.LoadLocal(L_i2);
						gen.Emit(OpCodes.Ldc_I4_4);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_i2);

						// dest[i] = Encoding.UTF8.GetString(filesDat, i2, len);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Call, typeof(Encoding).GetProperty("UTF8").GetGetMethod());
						gen.LoadLocal(L_data);
						gen.LoadLocal(L_i2);
						gen.LoadLocal(L_len);
						gen.Emit(OpCodes.Callvirt, typeof(Encoding).GetMethod("GetString", new Type[] { typeof(byte[]), typeof(int), typeof(int) }));
						gen.Emit(OpCodes.Stelem_Ref);

						// i2 += len;
						gen.LoadLocal(L_i2);
						gen.LoadLocal(L_len);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_i2);

						// i++;
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_i);

						// i < filesDat.Length;
						gen.MarkLabel(condLbl);
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.Emit(OpCodes.Ldlen);
						gen.Emit(OpCodes.Blt_S, bodyLbl);
					}

					{
						// FileSwitchDictionary = new Dictionary<string, int>(fileCount);
						gen.LoadInt((ulong)tree.Files.Count, 4, false);
						gen.Emit(OpCodes.Newobj, typeof(Dictionary<string, int>).GetConstructor(new Type[] { typeof(int) }));
						gen.Emit(OpCodes.Stsfld, fileSwitchDictionaryField);

						Label condLbl = gen.DefineLabel();
						Label loopBody = gen.DefineLabel();
						// i = 0;
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.StoreLocal(L_i);
						gen.Emit(OpCodes.Br_S, condLbl);

						// FileSwitchDictionary.Add(allFiles[i], i);
						gen.MarkLabel(loopBody);
						gen.Emit(OpCodes.Ldsfld, fileSwitchDictionaryField);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Ldelem_Ref);
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Call, typeof(Dictionary<string, int>).GetMethod("Add"));

						// i++;
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_i);

						// i < allFiles.Length;
						gen.MarkLabel(condLbl);
						gen.LoadLocal(L_i);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.Emit(OpCodes.Ldlen);
						gen.Emit(OpCodes.Blt_S, loopBody);
					}
					gen.Emit(OpCodes.Ret);
				}

				{
					var mBldr = asmTpBldr.DefineMethod("OnGetDirectoryAndFileList", MethodAttributes.FamORAssem | MethodAttributes.Final | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), new Type[] { typeof(string[]).MakeByRefType(), AccessWorkarounds.Archive_GetListFileInfo_Type.MakeArrayType().MakeByRefType() });
					const int P_directories = 1;
					mBldr.DefineParameter(1, ParameterAttributes.Out, "directories");
					const int P_files = 2;
					mBldr.DefineParameter(2, ParameterAttributes.Out, "files");
					var gen = mBldr.GetILGenerator();
					const int L_lInfoArr = 0;
					gen.DeclareLocal(AccessWorkarounds.Archive_GetListFileInfo_Type.MakeArrayType());
					const int L_i = 1;
					gen.DeclareLocal(typeof(int));

					// directories = allDirectories;
					gen.LoadParameter(P_directories);
					gen.Emit(OpCodes.Ldsfld, allDirectoriesField);
					gen.Emit(OpCodes.Stind_Ref);

					// lInfoArr = new Archive.GetListFileInfo[fileCount];
					gen.LoadInt((ulong)tree.Files.Count, 4, false);
					gen.Emit(OpCodes.Newarr, AccessWorkarounds.Archive_GetListFileInfo_Type);
					gen.StoreLocal(L_lInfoArr);

					// i = 0;
					gen.Emit(OpCodes.Ldc_I4_0);
					gen.StoreLocal(L_i);

					Label condLbl = gen.DefineLabel();
					Label bodyLbl = gen.DefineLabel();
					gen.Emit(OpCodes.Br_S, condLbl);

					// lInfoArr[i] = new Archive.GetListFileInfo(allFiles[i], (long)allLengths[i]);
					gen.MarkLabel(bodyLbl);
					gen.LoadLocal(L_lInfoArr);
					gen.LoadLocal(L_i);
					gen.Emit(OpCodes.Ldsfld, allFilesField);
					gen.LoadLocal(L_i);
					gen.Emit(OpCodes.Ldelem_Ref);
					gen.Emit(OpCodes.Ldsfld, allLengthsField);
					gen.LoadLocal(L_i);
					gen.Emit(OpCodes.Ldelem_I4);
					gen.Emit(OpCodes.Conv_I8);
					gen.Emit(OpCodes.Newobj, AccessWorkarounds.Archive_GetListFileInfo_Type.GetConstructor(new Type[] { typeof(string), typeof(long) }));
					gen.Emit(OpCodes.Stelem, AccessWorkarounds.Archive_GetListFileInfo_Type);

					// i++;
					gen.LoadLocal(L_i);
					gen.Emit(OpCodes.Ldc_I4_1);
					gen.Emit(OpCodes.Add);
					gen.StoreLocal(L_i);

					// i < allFiles.Length;
					gen.MarkLabel(condLbl);
					gen.LoadLocal(L_i);
					gen.Emit(OpCodes.Ldsfld, allFilesField);
					gen.Emit(OpCodes.Ldlen);
					gen.Emit(OpCodes.Blt_S, bodyLbl);

					// files = lInfoArr;
					gen.LoadParameter(P_files);
					gen.LoadLocal(L_lInfoArr);
					gen.Emit(OpCodes.Stind_Ref);

					gen.Emit(OpCodes.Ret);
				}

				{
					var mBldr = asmTpBldr.DefineMethod("OnFileOpen", MethodAttributes.FamORAssem | MethodAttributes.Final | MethodAttributes.Virtual, CallingConventions.Standard, typeof(VirtualFileStream), new Type[] { typeof(string) });
					const int P_inArchiveFileName = 1;
					mBldr.DefineParameter(1, ParameterAttributes.None, "inArchiveFileName");
					var gen = mBldr.GetILGenerator();
					const int L_buf = 0;
					gen.DeclareLocal(typeof(byte[])); // buf
					const int L_len = 1;
					gen.DeclareLocal(typeof(int)); // len
					const int L_idx = 2;
					gen.DeclareLocal(typeof(int)); // idx

					// len = AllLengths[idx = FileSwitchDictionary[inArchiveFileName]];
					gen.Emit(OpCodes.Ldsfld, allInternalLengthsField);
					gen.Emit(OpCodes.Ldsfld, fileSwitchDictionaryField);
					gen.LoadParameter(P_inArchiveFileName);
					gen.Emit(OpCodes.Call, typeof(Dictionary<string, int>).GetProperty("Item").GetGetMethod());
					gen.Emit(OpCodes.Dup);
					gen.StoreLocal(L_idx);
					gen.Emit(OpCodes.Ldelem_I4);
					gen.StoreLocal(L_len);

					// buf = new byte[len];
					gen.LoadLocal(L_len);
					gen.Emit(OpCodes.Newarr, typeof(byte));
					gen.StoreLocal(L_buf);

					// lock(this.file)
					// {
					gen.LoadThis();
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.Emit(OpCodes.Call, typeof(System.Threading.Monitor).GetMethod("Enter", new Type[] { typeof(object) }));
					gen.BeginExceptionBlock();

					// file.Position = AllOffsets[idx];
					gen.LoadThis();
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.Emit(OpCodes.Ldsfld, allOffsetsField);
					gen.LoadLocal(L_idx);
					gen.Emit(OpCodes.Ldelem_I8);
					gen.Emit(OpCodes.Callvirt, typeof(FileStream).GetProperty("Position").GetSetMethod());

					// file.Read(buf, 0, len);
					gen.LoadThis();
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.LoadLocal(L_buf);
					gen.Emit(OpCodes.Ldc_I4_0);
					gen.LoadLocal(L_len);
					gen.Emit(OpCodes.Callvirt, typeof(FileStream).GetMethod("Read"));
					gen.Emit(OpCodes.Pop);
					Label retLbl = gen.DefineLabel();
					gen.Emit(OpCodes.Leave_S, retLbl);
					// }
					gen.BeginFinallyBlock();
					gen.LoadThis();
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.Emit(OpCodes.Call, typeof(System.Threading.Monitor).GetMethod("Exit", new Type[] { typeof(object) }));
					gen.Emit(OpCodes.Endfinally);
					gen.EndExceptionBlock();

					// return new MemoryVirtualFileStream(buf);
					gen.MarkLabel(retLbl);

					// Now generate a switch with
					// the required compression types.
					if (allCompressionField != null)
					{
						gen.Emit(OpCodes.Ldsfld, allCompressionField);
						gen.LoadLocal(L_idx);
						gen.Emit(OpCodes.Ldelem_U1);
						Label endSwitch = gen.DefineLabel();
						Label[] algs = new Label[usedCompressionAlgorithms.Max() + 1];
						algs.Initialize(endSwitch);
						usedCompressionAlgorithms.Where(c => c != 0).ForEach(c => algs[c] = gen.DefineLabel());
						gen.Emit(OpCodes.Switch, algs);
						gen.Emit(OpCodes.Br_S, endSwitch);

						foreach (var v in usedCompressionAlgorithms.Where(c => c != 0))
						{
							gen.MarkLabel(algs[v]);
							gen.LoadLocal(L_buf);
							gen.Emit(OpCodes.Call, DecompressionEmitter.GetDecompress(modBldr, config, (CompressionAlgorithm)v));
							gen.StoreLocal(L_buf);
						}

						gen.MarkLabel(endSwitch);
					}

					gen.LoadLocal(L_buf);
					gen.Emit(OpCodes.Newobj, typeof(MemoryVirtualFileStream).GetConstructor(new Type[] { typeof(byte[]) }));
					gen.Emit(OpCodes.Ret);
				}
			}

			asmTpBldr.CreateType();
			#endregion


			#region Gerate Archive Manager Type
			var arManBldr = modBldr.DefineType(config.TargetNamespace + ".CustomArchiveFactory", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(ArchiveFactory));

			{
				var cBldr = arManBldr.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
				var gen = cBldr.GetILGenerator();

				gen.LoadThis();
				gen.Emit(OpCodes.Ldstr, config.TargetFileExtension);
				gen.Emit(OpCodes.Call, typeof(ArchiveFactory).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, new ParameterModifier[] { }));
				gen.Emit(OpCodes.Ret);
			}

			{
				var mBldr = arManBldr.DefineMethod("OnInit", MethodAttributes.FamORAssem | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.Standard, typeof(bool), Type.EmptyTypes);
				var gen = mBldr.GetILGenerator();

				gen.Emit(OpCodes.Ldc_I4_1);
				gen.Emit(OpCodes.Ret);
			}

			{
				var mBldr = arManBldr.DefineMethod("OnLoadArchive", MethodAttributes.FamORAssem | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.Standard, typeof(Archive), new Type[] { typeof(string) });
				const int P_fileName = 1;
				mBldr.DefineParameter(1, ParameterAttributes.None, "fileName");
				var gen = mBldr.GetILGenerator();

				gen.LoadThis();
				gen.LoadParameter(P_fileName);
				gen.Emit(OpCodes.Newobj, archiveConstructor);
				gen.Emit(OpCodes.Ret);
			}

			arManBldr.CreateType();
			#endregion

			DecompressionEmitter.FinalizeDecompressionType();

			modBldr.CreateGlobalFunctions();
			aBldr.Save(config.TargetAssemblyName + ".dll");
		}
	}
}
