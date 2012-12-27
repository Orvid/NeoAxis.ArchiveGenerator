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
			public class FileDescriptor
			{
				public readonly string FullName;
				public readonly int Size;
				public readonly FileInfo SourceFile;
				public long FileOffset;

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

			public byte[] GetLengthData()
			{
				byte[] ret = new byte[Files.Count << 2];
				for (int i = 0; i < Files.Count; i++)
				{
					Array.Copy(BitConverter.GetBytes(Files[i].Size), 0, ret, i << 2, 4);
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
						return StringComparer.CurrentCultureIgnoreCase.Compare(a.FullName, b.FullName);
					});

					foreach (var v in tree.Files)
					{
                        WriteStartAddingFile(v);
						v.FileOffset = AddFile(strm, v.SourceFile, config);
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

            private static void WriteEndAddingFile(bool merged)
            {
                if (merged)
                {
                    Console.CursorLeft = Console.BufferWidth - 7;
                    Console.WriteLine("Merged");
                }
                else
                {
                    Console.CursorLeft = Console.BufferWidth - 5;
                    Console.WriteLine("Done");
                }
            }

            private static Dictionary<string, int> FileExtensionPriorities;
			private static int GetFilePriority(FileInfo file)
			{
                int p;
                string fileExtension = file.Extension;
                if (!FileExtensionPriorities.TryGetValue(fileExtension.ToLower(), out p))
                    return FileExtensionPriorities.Count + 2000;
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
                for (int i = 0; i < config.Optimizations.EmitOrder.Length; i++)
                {
                    FileExtensionPriorities[config.Optimizations.EmitOrder[i].ToLower()] = i;
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

			private static Dictionary<int, Tuple<FileInfo, long>> KnownFiles;
			private static HashAlgorithm HashProvider;
			private static long AddFile(FileStream strm, FileInfo file, GeneratorOptions config)
			{
				if (!config.Optimizations.MergeDuplicateFiles)
				{
					long off = strm.Position;
					using (var fs = file.OpenRead())
					{
						byte[] buf = new byte[file.Length];
						fs.Read(buf, 0, buf.Length);
						strm.Write(buf, 0, buf.Length);
                    }
                    WriteEndAddingFile(false);
					return off;
				}
				else
				{
					byte[] buf = new byte[file.Length];
					long off = strm.Position;
					using (var fs = file.OpenRead())
					{
						fs.Read(buf, 0, buf.Length);
					}
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
			var modBldr = aBldr.DefineDynamicModule(config.TargetAssemblyName, config.TargetAssemblyName + ".dll");

			#region Generate Archive Type
			var asmTpBldr = modBldr.DefineType(config.TargetNamespace + ".CustomArchive", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(Archive));
			ConstructorBuilder archiveConstructor = null;
			{
				var tree = FileTreeDescriptor.GetTree(config);

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
					archiveConstructor.DefineParameter(1, ParameterAttributes.None, "factory");
					archiveConstructor.DefineParameter(2, ParameterAttributes.None, "fileName");
					var gen = archiveConstructor.GetILGenerator();
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldarg_1);
					gen.Emit(OpCodes.Ldarg_2);
					gen.Emit(OpCodes.Call, typeof(Archive).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(ArchiveFactory), typeof(string) }, new ParameterModifier[] { }));
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldarg_2);
					gen.Emit(OpCodes.Ldc_I4_3); // FileMode.Open
					gen.Emit(OpCodes.Ldc_I4_1); // FileAccess.Read
					gen.Emit(OpCodes.Newobj, typeof(FileStream).GetConstructor(new Type[] { typeof(string), typeof(FileMode), typeof(FileAccess) }));
					gen.Emit(OpCodes.Stfld, fileField);
					gen.Emit(OpCodes.Ret);
				}

				{
					var mBldr = asmTpBldr.DefineMethod("Dispose", MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), Type.EmptyTypes);
					var gen = mBldr.GetILGenerator();
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.Emit(OpCodes.Call, typeof(FileStream).GetMethod("Dispose"));
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldnull);
					gen.Emit(OpCodes.Stfld, fileField);
					gen.Emit(OpCodes.Ret);
				}

				{
					var cBldr = asmTpBldr.DefineTypeInitializer();
					var gen = cBldr.GetILGenerator();
					gen.DeclareLocal(typeof(int));
					gen.DeclareLocal(typeof(byte[]));
					gen.DeclareLocal(typeof(int));
					gen.DeclareLocal(typeof(int));

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
						gen.Emit(OpCodes.Stloc_1);
#endif

						// int i = 0, i2 = 0;
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.Emit(OpCodes.Stloc_0);
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.Emit(OpCodes.Stloc_2);
						Label condLbl = gen.DefineLabel();
						Label bodyLbl = gen.DefineLabel();
						gen.Emit(OpCodes.Br_S, condLbl);
						gen.MarkLabel(bodyLbl);

						// int len = BitConverter.ToInt32(dirsDat, i2);
						gen.Emit(OpCodes.Ldloc_1);
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Call, typeof(BitConverter).GetMethod("ToInt32", new Type[] { typeof(byte[]), typeof(int) }));
						gen.Emit(OpCodes.Stloc_3);

						// i2 += 4;
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Ldc_I4_4);
						gen.Emit(OpCodes.Add);
						gen.Emit(OpCodes.Stloc_2);

						// allDirectories[i] = Encoding.UTF8.GetString(dirs, i2, len);
						gen.Emit(OpCodes.Ldsfld, allDirectoriesField);
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Call, typeof(Encoding).GetProperty("UTF8").GetGetMethod());
						gen.Emit(OpCodes.Ldloc_1);
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Ldloc_3);
						gen.Emit(OpCodes.Callvirt, typeof(Encoding).GetMethod("GetString", new Type[] { typeof(byte[]), typeof(int), typeof(int) }));
						gen.Emit(OpCodes.Stelem_Ref);

						// i2 += len;
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Ldloc_3);
						gen.Emit(OpCodes.Add);
						gen.Emit(OpCodes.Stloc_2);

						// i++;
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.Emit(OpCodes.Stloc_0);

						// i < dirsDat.Length;
						gen.MarkLabel(condLbl);
						gen.Emit(OpCodes.Ldloc_0);
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
						gen.Emit(OpCodes.Stloc_1);
#endif

						// int i = 0, i2 = 0;
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.Emit(OpCodes.Stloc_0);
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.Emit(OpCodes.Stloc_2);
						Label condLbl = gen.DefineLabel();
						Label bodyLbl = gen.DefineLabel();
						gen.Emit(OpCodes.Br_S, condLbl);
						gen.MarkLabel(bodyLbl);

						// int len = BitConverter.ToInt32(filesDat, i2);
						gen.Emit(OpCodes.Ldloc_1);
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Call, typeof(BitConverter).GetMethod("ToInt32", new Type[] { typeof(byte[]), typeof(int) }));
						gen.Emit(OpCodes.Stloc_3);

						// i2 += 4;
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Ldc_I4_4);
						gen.Emit(OpCodes.Add);
						gen.Emit(OpCodes.Stloc_2);

						// dest[i] = Encoding.UTF8.GetString(filesDat, i2, len);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Call, typeof(Encoding).GetProperty("UTF8").GetGetMethod());
						gen.Emit(OpCodes.Ldloc_1);
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Ldloc_3);
						gen.Emit(OpCodes.Callvirt, typeof(Encoding).GetMethod("GetString", new Type[] { typeof(byte[]), typeof(int), typeof(int) }));
						gen.Emit(OpCodes.Stelem_Ref);

						// i2 += len;
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Ldloc_3);
						gen.Emit(OpCodes.Add);
						gen.Emit(OpCodes.Stloc_2);

						// i++;
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.Emit(OpCodes.Stloc_0);

						// i < filesDat.Length;
						gen.MarkLabel(condLbl);
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.Emit(OpCodes.Ldlen);
						gen.Emit(OpCodes.Blt_S, bodyLbl);
					}

					{
						gen.LoadInt((ulong)tree.Files.Count, 4, false);
						gen.Emit(OpCodes.Newobj, typeof(Dictionary<string, int>).GetConstructor(new Type[] { typeof(int) }));
						gen.Emit(OpCodes.Stsfld, fileSwitchDictionaryField);
						Label condLbl = gen.DefineLabel();
						Label loopBody = gen.DefineLabel();
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.Emit(OpCodes.Stloc_0);
						gen.Emit(OpCodes.Br_S, condLbl);
						gen.MarkLabel(loopBody);
						gen.Emit(OpCodes.Ldsfld, fileSwitchDictionaryField);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Ldelem_Ref);
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Call, typeof(Dictionary<string, int>).GetMethod("Add"));
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.Emit(OpCodes.Stloc_0);
						gen.MarkLabel(condLbl);
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Ldsfld, allFilesField);
						gen.Emit(OpCodes.Ldlen);
						gen.Emit(OpCodes.Blt_S, loopBody);
					}
					gen.Emit(OpCodes.Ret);
				}

				{
					var mBldr = asmTpBldr.DefineMethod("OnGetDirectoryAndFileList", MethodAttributes.FamORAssem | MethodAttributes.Final | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), new Type[] { typeof(string[]).MakeByRefType(), AccessWorkarounds.Archive_GetListFileInfo_Type.MakeArrayType().MakeByRefType() });
					mBldr.DefineParameter(1, ParameterAttributes.Out, "directories");
					mBldr.DefineParameter(2, ParameterAttributes.Out, "files");
					var gen = mBldr.GetILGenerator();
					gen.DeclareLocal(AccessWorkarounds.Archive_GetListFileInfo_Type.MakeArrayType());
					gen.DeclareLocal(typeof(int));
					gen.Emit(OpCodes.Ldarg_1);
					gen.Emit(OpCodes.Ldsfld, allDirectoriesField);
					gen.Emit(OpCodes.Stind_Ref);

					gen.LoadInt((ulong)tree.Files.Count, 4, false);
					gen.Emit(OpCodes.Newarr, AccessWorkarounds.Archive_GetListFileInfo_Type);
					gen.Emit(OpCodes.Stloc_0);

					gen.Emit(OpCodes.Ldc_I4_0);
					gen.Emit(OpCodes.Stloc_1);
					Label condLbl = gen.DefineLabel();
					Label bodyLbl = gen.DefineLabel();
					gen.Emit(OpCodes.Br_S, condLbl);
					gen.MarkLabel(bodyLbl);
					gen.Emit(OpCodes.Ldloc_0);
					gen.Emit(OpCodes.Ldloc_1);
					gen.Emit(OpCodes.Ldsfld, allFilesField);
					gen.Emit(OpCodes.Ldloc_1);
					gen.Emit(OpCodes.Ldelem_Ref);
					gen.Emit(OpCodes.Ldsfld, allLengthsField);
					gen.Emit(OpCodes.Ldloc_1);
					gen.Emit(OpCodes.Ldelem_I4);
					gen.Emit(OpCodes.Conv_I8);
					gen.Emit(OpCodes.Newobj, AccessWorkarounds.Archive_GetListFileInfo_Type.GetConstructor(new Type[] { typeof(string), typeof(long) }));
					gen.Emit(OpCodes.Stelem, AccessWorkarounds.Archive_GetListFileInfo_Type);
					gen.Emit(OpCodes.Ldloc_1);
					gen.Emit(OpCodes.Ldc_I4_1);
					gen.Emit(OpCodes.Add);
					gen.Emit(OpCodes.Stloc_1);
					gen.MarkLabel(condLbl);
					gen.Emit(OpCodes.Ldloc_1);
					gen.Emit(OpCodes.Ldsfld, allFilesField);
					gen.Emit(OpCodes.Ldlen);
					gen.Emit(OpCodes.Blt_S, bodyLbl);

					gen.Emit(OpCodes.Ldarg_2);
					gen.Emit(OpCodes.Ldloc_0);
					gen.Emit(OpCodes.Stind_Ref);

					gen.Emit(OpCodes.Ret);
				}

				{
					var mBldr = asmTpBldr.DefineMethod("OnFileOpen", MethodAttributes.FamORAssem | MethodAttributes.Final | MethodAttributes.Virtual, CallingConventions.Standard, typeof(VirtualFileStream), new Type[] { typeof(string) });
					mBldr.DefineParameter(1, ParameterAttributes.None, "inArchiveFileName");
					var gen = mBldr.GetILGenerator();
					gen.DeclareLocal(typeof(byte[])); // buf
					gen.DeclareLocal(typeof(int)); // len
					gen.DeclareLocal(typeof(int)); // idx

					// len = AllLengths[idx = FileSwitchDictionary[inArchiveFileName]];
					gen.Emit(OpCodes.Ldsfld, allLengthsField);
					gen.Emit(OpCodes.Ldsfld, fileSwitchDictionaryField);
					gen.Emit(OpCodes.Ldarg_1);
					gen.Emit(OpCodes.Call, typeof(Dictionary<string, int>).GetProperty("Item").GetGetMethod());
					gen.Emit(OpCodes.Dup);
					gen.Emit(OpCodes.Stloc_2);
					gen.Emit(OpCodes.Ldelem_I4);
					gen.Emit(OpCodes.Stloc_1);
					
					// buf = new byte[len];
					gen.Emit(OpCodes.Ldloc_1);
					gen.Emit(OpCodes.Newarr, typeof(byte));
					gen.Emit(OpCodes.Stloc_0);

					// lock(this.file)
					// {
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.Emit(OpCodes.Call, typeof(System.Threading.Monitor).GetMethod("Enter", new Type[] { typeof(object) }));
					gen.BeginExceptionBlock();

						// file.Position = AllOffsets[idx];
						gen.Emit(OpCodes.Ldarg_0);
						gen.Emit(OpCodes.Ldfld, fileField);
						gen.Emit(OpCodes.Ldsfld, allOffsetsField);
						gen.Emit(OpCodes.Ldloc_2);
						gen.Emit(OpCodes.Ldelem_I8);
						gen.Emit(OpCodes.Callvirt, typeof(FileStream).GetProperty("Position").GetSetMethod());

						// file.Read(buf, 0, len);
						gen.Emit(OpCodes.Ldarg_0);
						gen.Emit(OpCodes.Ldfld, fileField);
						gen.Emit(OpCodes.Ldloc_0);
						gen.Emit(OpCodes.Ldc_I4_0);
						gen.Emit(OpCodes.Ldloc_1);
						gen.Emit(OpCodes.Callvirt, typeof(FileStream).GetMethod("Read"));
						gen.Emit(OpCodes.Pop);
						Label retLbl = gen.DefineLabel();
						gen.Emit(OpCodes.Leave_S, retLbl);
					// }
					gen.BeginFinallyBlock();
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldfld, fileField);
					gen.Emit(OpCodes.Call, typeof(System.Threading.Monitor).GetMethod("Exit", new Type[] { typeof(object) }));
					gen.Emit(OpCodes.Endfinally);
					gen.EndExceptionBlock();

					// return new MemoryVirtualFileStream(buf);
					gen.MarkLabel(retLbl);
					gen.Emit(OpCodes.Ldloc_0);
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

				gen.Emit(OpCodes.Ldarg_0);
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
				mBldr.DefineParameter(1, ParameterAttributes.None, "fileName");
				var gen = mBldr.GetILGenerator();

				gen.Emit(OpCodes.Ldarg_0);
				gen.Emit(OpCodes.Ldarg_1);
				gen.Emit(OpCodes.Newobj, archiveConstructor);
				gen.Emit(OpCodes.Ret);
			}

			arManBldr.CreateType();
			#endregion

			modBldr.CreateGlobalFunctions();
			aBldr.Save(config.TargetAssemblyName + ".dll");
		}
	}
}
