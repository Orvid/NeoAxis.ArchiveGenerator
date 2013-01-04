using System;
using System.Reflection;
using System.Reflection.Emit;

namespace ArchiveGenerator
{
	public static class DecompressionEmitter
	{
		private static TypeBuilder mTb = null;
		private static MethodBuilder decompress_LZF = null;

		private static void CreateTb(ModuleBuilder modBldr, GeneratorOptions config)
		{
			if (mTb == null)
				mTb = modBldr.DefineType(config.TargetNamespace + ".Decompression", TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed);
		}

		public static void FinalizeDecompressionType()
		{
			if (mTb != null)
				mTb.CreateType();
		}

		public static MethodInfo GetDecompress(ModuleBuilder modBldr, GeneratorOptions config, CompressionAlgorithm alg)
		{
			switch (alg)
			{
				case CompressionAlgorithm.LZF:
					return GetDecompressLZF(modBldr, config);
				default:
					throw new Exception("Unknown CompressionAlgorithm!");
			}
		}

		private static MethodInfo GetDecompressLZF(ModuleBuilder modBldr, GeneratorOptions config)
		{
			CreateTb(modBldr, config);
			if (decompress_LZF == null)
				decompress_LZF = EmitDecompressLZF(mTb);
			return decompress_LZF;
		}

		private static MethodBuilder EmitDecompressLZF(TypeBuilder parent)
		{
			var mBldr = parent.DefineMethod("DecompressLZF", MethodAttributes.Assembly | MethodAttributes.Static, CallingConventions.Standard, typeof(byte[]), new Type[] { typeof(byte[]) });
			const int P_data = 0;
			mBldr.DefineParameter(1, ParameterAttributes.None, "data");
			var gen = mBldr.GetILGenerator();
			const int L_strm = 0;
			gen.DeclareLocal(typeof(byte[]));
			const int L_iidx = 1;
			gen.DeclareLocal(typeof(uint));
			const int L_oidx = 2;
			gen.DeclareLocal(typeof(uint));
			const int L_ctrl = 3;
			gen.DeclareLocal(typeof(uint));
			const int L_len = 4;
			gen.DeclareLocal(typeof(uint));
			const int L_reference = 5;
			gen.DeclareLocal(typeof(int));

			// strm = new byte[BitConverter.ToInt32(data, 0)];
			gen.LoadParameter(P_data);
			gen.Emit(OpCodes.Ldc_I4_0);
			gen.Emit(OpCodes.Call, typeof(BitConverter).GetMethod("ToInt32"));
			gen.Emit(OpCodes.Newarr, typeof(byte));
			gen.StoreLocal(L_strm);

			// iidx = 4;
			gen.Emit(OpCodes.Ldc_I4_4);
			gen.StoreLocal(L_iidx);

			// oidx = 0;
			gen.Emit(OpCodes.Ldc_I4_0);
			gen.StoreLocal(L_oidx);


			Label mainLoopCond = gen.DefineLabel();
			gen.Emit(OpCodes.Br, mainLoopCond);
			Label mainLoopBody = gen.DefineLabel();
			{
				// ctrl = data[iidx++];
				gen.MarkLabel(mainLoopBody);
				gen.LoadParameter(P_data);
				gen.LoadLocal(L_iidx);
				gen.Emit(OpCodes.Dup);
				gen.Emit(OpCodes.Ldc_I4_1);
				gen.Emit(OpCodes.Add);
				gen.StoreLocal(L_iidx);
				gen.Emit(OpCodes.Ldelem_U1);
				gen.StoreLocal(L_ctrl);

				// if (ctrl < (1 << 5))
				Label backReference = gen.DefineLabel();
				gen.LoadLocal(L_ctrl);
				gen.LoadInt((ulong)(1 << 5), 4, false);
				gen.Emit(OpCodes.Bge_S, backReference);
				{
					// ctrl++;
					gen.LoadLocal(L_ctrl);
					gen.Emit(OpCodes.Ldc_I4_1);
					gen.Emit(OpCodes.Add);
					gen.StoreLocal(L_ctrl);

					// do
						Label lBody = gen.DefineLabel();
						gen.MarkLabel(lBody);
						// strm[oidx++] = data[iidx++];
						gen.LoadLocal(L_strm);
						gen.LoadLocal(L_oidx);
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_oidx);
						gen.LoadParameter(P_data);
						gen.LoadLocal(L_iidx);
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_iidx);
						gen.Emit(OpCodes.Ldelem_U1);
						gen.Emit(OpCodes.Stelem_I1);
					// while ((--ctrl) != 0);
					gen.LoadLocal(L_ctrl);
					gen.Emit(OpCodes.Ldc_I4_1);
					gen.Emit(OpCodes.Sub);
					gen.Emit(OpCodes.Dup);
					gen.StoreLocal(L_ctrl);
					gen.Emit(OpCodes.Brtrue_S, lBody);
					gen.Emit(OpCodes.Br, mainLoopCond);
				}
				// else
				{
					gen.MarkLabel(backReference);
					// len = ctrl >> 5;
					gen.LoadLocal(L_ctrl);
					gen.Emit(OpCodes.Ldc_I4_5);
					gen.Emit(OpCodes.Shr_Un);
					gen.StoreLocal(L_len);
					// reference = (int)((int)oidx - ((ctrl & 0x1f) << 8) - 1);
					gen.LoadLocal(L_oidx);
					gen.Emit(OpCodes.Conv_I4);
					gen.LoadLocal(L_ctrl);
					gen.Emit(OpCodes.Ldc_I4_S, (byte)0x1F);
					gen.Emit(OpCodes.And);
					gen.Emit(OpCodes.Ldc_I4_8);
					gen.Emit(OpCodes.Shl);
					gen.Emit(OpCodes.Sub);
					gen.Emit(OpCodes.Ldc_I4_1);
					gen.Emit(OpCodes.Sub);
					gen.Emit(OpCodes.Conv_I4);
					gen.StoreLocal(L_reference);
					// if (len == 7)
					Label afterLen = gen.DefineLabel();
					gen.LoadLocal(L_len);
					gen.Emit(OpCodes.Ldc_I4_7);
					gen.Emit(OpCodes.Bne_Un_S, afterLen);
						// len += data[iidx++];
						gen.LoadLocal(L_len);
						gen.LoadParameter(P_data);
						gen.LoadLocal(L_iidx);
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_iidx);
						gen.Emit(OpCodes.Ldelem_U1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_len);
					// reference -= data[iidx++];
					gen.MarkLabel(afterLen);
					gen.LoadLocal(L_reference);
					gen.LoadParameter(P_data);
					gen.LoadLocal(L_iidx);
					gen.Emit(OpCodes.Dup);
					gen.Emit(OpCodes.Ldc_I4_1);
					gen.Emit(OpCodes.Add);
					gen.StoreLocal(L_iidx);
					gen.Emit(OpCodes.Ldelem_U1);
					gen.Emit(OpCodes.Sub);
					gen.StoreLocal(L_reference);
					// strm[oidx++] = strm[reference++];
					// strm[oidx++] = strm[reference++];
					// do
					//     strm[oidx++] = strm[reference++];
					Label lBody = gen.DefineLabel();
					for (int i = 0; i < 3; i++)
					{
						if (i == 2)
							gen.MarkLabel(lBody);
						gen.LoadLocal(L_strm);
						gen.LoadLocal(L_oidx);
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_oidx);
						gen.LoadLocal(L_strm);
						gen.LoadLocal(L_reference);
						gen.Emit(OpCodes.Dup);
						gen.Emit(OpCodes.Ldc_I4_1);
						gen.Emit(OpCodes.Add);
						gen.StoreLocal(L_reference);
						gen.Emit(OpCodes.Ldelem_U1);
						gen.Emit(OpCodes.Stelem_I1);
					}
					// while ((--len) != 0);
					gen.LoadLocal(L_len);
					gen.Emit(OpCodes.Ldc_I4_1);
					gen.Emit(OpCodes.Sub);
					gen.Emit(OpCodes.Dup);
					gen.StoreLocal(L_len);
					gen.Emit(OpCodes.Brtrue_S, lBody);
				}
			}
			// while (iidx < data.Length)
			gen.MarkLabel(mainLoopCond);
			gen.LoadLocal(L_iidx);
			gen.LoadParameter(P_data);
			gen.Emit(OpCodes.Ldlen);
			gen.Emit(OpCodes.Blt, mainLoopBody);

			// return strm;
			gen.LoadLocal(L_strm);
			gen.Emit(OpCodes.Ret);
			return mBldr;
		}
	}
}
