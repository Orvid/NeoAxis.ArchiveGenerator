using System;
using System.Reflection.Emit;

namespace ArchiveGenerator
{
	public static class ILGeneratorExtensions
	{
		public static void Load(this ILGenerator gen, LocalBuilder local)
		{
			gen.LoadLocal(local.LocalIndex);
		}

		public static void Load(this ILGenerator gen, ParameterBuilder param)
		{
			gen.LoadParameter(param.Position);
		}

		public static void LoadThis(this ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldarg_0);
		}

		public static void LoadLocal(this ILGenerator gen, int idx)
		{
			switch (idx)
			{
				case 0:
					gen.Emit(OpCodes.Ldloc_0);
					break;
				case 1:
					gen.Emit(OpCodes.Ldloc_1);
					break;
				case 2:
					gen.Emit(OpCodes.Ldloc_2);
					break;
				case 3:
					gen.Emit(OpCodes.Ldloc_3);
					break;
				default:
					if (idx < byte.MaxValue)
						gen.Emit(OpCodes.Ldloc_S, (byte)idx);
					else
						gen.Emit(OpCodes.Ldloc, idx);
					break;
			}
		}

		public static void LoadLocalAddress(this ILGenerator gen, int idx)
		{
			if (idx < byte.MaxValue)
				gen.Emit(OpCodes.Ldloca_S, (byte)idx);
			else
				gen.Emit(OpCodes.Ldloca, idx);
		}

		public static void StoreLocal(this ILGenerator gen, int idx)
		{
			switch (idx)
			{
				case 0:
					gen.Emit(OpCodes.Stloc_0);
					break;
				case 1:
					gen.Emit(OpCodes.Stloc_1);
					break;
				case 2:
					gen.Emit(OpCodes.Stloc_2);
					break;
				case 3:
					gen.Emit(OpCodes.Stloc_3);
					break;
				default:
					if (idx < byte.MaxValue)
						gen.Emit(OpCodes.Stloc_S, (byte)idx);
					else
						gen.Emit(OpCodes.Stloc, idx);
					break;
			}
		}

		public static void LoadParameter(this ILGenerator gen, int idx)
		{
			switch (idx)
			{
				case 0:
					gen.Emit(OpCodes.Ldarg_0);
					break;
				case 1:
					gen.Emit(OpCodes.Ldarg_1);
					break;
				case 2:
					gen.Emit(OpCodes.Ldarg_2);
					break;
				case 3:
					gen.Emit(OpCodes.Ldarg_3);
					break;
				default:
					if (idx < byte.MaxValue)
						gen.Emit(OpCodes.Ldarg_S, (byte)idx);
					else
						gen.Emit(OpCodes.Ldarg, idx);
					break;
			}
		}

		public static void StoreParameter(this ILGenerator gen, int idx)
		{
			if (idx < byte.MaxValue)
				gen.Emit(OpCodes.Starg_S, (byte)idx);
			else
				gen.Emit(OpCodes.Starg, idx);
		}

		public static void LoadParameterAddress(this ILGenerator gen, int idx)
		{
			if (idx < byte.MaxValue)
				gen.Emit(OpCodes.Ldarga_S, (byte)idx);
			else
				gen.Emit(OpCodes.Ldarga, idx);
		}

		public static void LoadInt(this ILGenerator gen, ulong value, byte size, bool signed)
		{
			bool needsConvToLong = false;
			if (size == 8)
			{
				needsConvToLong = true;
			}
			switch (value)
			{
				case 0:
					gen.Emit(OpCodes.Ldc_I4_0);
					break;
				case 1:
					gen.Emit(OpCodes.Ldc_I4_1);
					break;
				case 2:
					gen.Emit(OpCodes.Ldc_I4_2);
					break;
				case 3:
					gen.Emit(OpCodes.Ldc_I4_3);
					break;
				case 4:
					gen.Emit(OpCodes.Ldc_I4_4);
					break;
				case 5:
					gen.Emit(OpCodes.Ldc_I4_5);
					break;
				case 6:
					gen.Emit(OpCodes.Ldc_I4_6);
					break;
				case 7:
					gen.Emit(OpCodes.Ldc_I4_7);
					break;
				case 8:
					gen.Emit(OpCodes.Ldc_I4_8);
					break;
				default:
					if (signed)
					{
						long sVal;
						switch (size)
						{
							case 1:
								sVal = (long)(sbyte)(byte)value;
								break;
							case 2:
								sVal = (long)(short)(ushort)value;
								break;
							case 4:
								sVal = (long)(int)(uint)value;
								break;
							case 8:
								sVal = (long)value;
								break;

							default:
								throw new Exception("Unknown size of an integer to load!");
						}
						if (sVal == -1)
						{
							gen.Emit(OpCodes.Ldc_I4_M1);
						}
						else if (sVal < 0)
						{
							if (sVal >= sbyte.MinValue)
							{
								gen.Emit(OpCodes.Ldc_I4_S, (sbyte)sVal);
							}
							else if (sVal >= int.MinValue)
							{
								gen.Emit(OpCodes.Ldc_I4, (int)sVal);
							}
							else
							{
								needsConvToLong = false;
								gen.Emit(OpCodes.Ldc_I8, sVal);
							}
						}
						else
						{
							if (sVal <= sbyte.MaxValue)
							{
								gen.Emit(OpCodes.Ldc_I4_S, (sbyte)sVal);
							}
							else if (sVal <= int.MaxValue)
							{
								gen.Emit(OpCodes.Ldc_I4, (int)sVal);
							}
							else
							{
								needsConvToLong = false;
								gen.Emit(OpCodes.Ldc_I8, sVal);
							}
						}

					}
					else
					{
						ulong uVal;
						switch (size)
						{
							case 1:
								uVal = (ulong)(byte)value;
								break;
							case 2:
								uVal = (ulong)(ushort)value;
								break;
							case 4:
								uVal = (ulong)(uint)value;
								break;
							case 8:
								uVal = value;
								break;

							default:
								throw new Exception("Unknown size of an integer to load!");
						}

						if (uVal <= (ulong)sbyte.MaxValue)
						{
							gen.Emit(OpCodes.Ldc_I4_S, (sbyte)(byte)uVal);
						}
						else if (uVal <= uint.MaxValue)
						{
							gen.Emit(OpCodes.Ldc_I4, (int)uVal);
						}
						else
						{
							needsConvToLong = false;
							gen.Emit(OpCodes.Ldc_I8, uVal);
						}
					}
					break;
			}

			if (needsConvToLong)
			{
				if (signed)
					gen.Emit(OpCodes.Conv_I8);
				else
					gen.Emit(OpCodes.Conv_U8);
			}
		}
	}
}
