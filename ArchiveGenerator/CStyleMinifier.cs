//#define AlwaysNewLines
using System;
using System.IO;

namespace ArchiveGenerator
{
	public static class CStyleMinifier
	{
		private enum MinifierState
		{
			Unknown,
			Identifier,
			MaybeComment,
			SingleLineComment,
			BlockComment,
			MaybeEndBlockComment,

			Compiler,

			MaybeNonEscapeString,
			NonEscapeString,
			String,
			StringEscape,

		}

		public static Stream Minify(Stream src)
		{
			MemoryStream ret = new MemoryStream((int)src.Length);
			MinifierState stat = MinifierState.Unknown;
			bool prevWasIdentifier = false;
			bool prevWasCompiler = false;
			bool prevPrevWasCompiler = true;
			int val = -1;
			while ((val = src.ReadByte()) != -1)
			{
				switch (stat)
				{
					case MinifierState.Unknown:
						switch ((char)val)
						{
							case ' ':
							case '\t':
								break;
							case '\r':
#if AlwaysNewLines
								ret.WriteByte((byte)'\r');
								ret.WriteByte((byte)'\n');
								prevPrevWasCompiler = true;
								prevWasCompiler = false;
								prevWasIdentifier = false;
								break;
							case '\n':
								break;
#else
							case '\n':
								if (prevWasCompiler)
								{
									ret.WriteByte((byte)'\r');
									ret.WriteByte((byte)'\n');
									prevPrevWasCompiler = true;
									prevWasCompiler = false;
									prevWasIdentifier = false;
								}
								break;
#endif
							case '/':
								stat = MinifierState.MaybeComment;
								break;
							case '#':
								stat = MinifierState.Compiler;
								break;
							case '@':
								stat = MinifierState.MaybeNonEscapeString;
								ret.WriteByte((byte)'@');
								prevWasIdentifier = false;
								prevPrevWasCompiler = false;
								break;
							case '"':
								prevWasIdentifier = false;
								prevPrevWasCompiler = false;
								stat = MinifierState.String;
								ret.WriteByte((byte)'"');
								break;
							default:
								if (IsIdentifierChar((char)val))
								{
									if (prevWasIdentifier)
										ret.WriteByte((byte)' ');
									ret.WriteByte((byte)val);
									stat = MinifierState.Identifier;
									prevWasIdentifier = true;
								}
								else
								{
									ret.WriteByte((byte)val);
									prevWasIdentifier = false;
								}
								prevPrevWasCompiler = false;
								break;
						}
						break;

					case MinifierState.MaybeNonEscapeString:
						switch ((char)val)
						{
							case '"':
								ret.WriteByte((byte)val);
								stat = MinifierState.NonEscapeString;
								break;
							default:
								src.Position--;
								src.Flush();
								stat = MinifierState.Unknown;
								break;
						}
						break;

					case MinifierState.NonEscapeString:
						switch ((char)val)
						{
							case '"':
								ret.WriteByte((byte)val);
								stat = MinifierState.Unknown;
								break;
							default:
								ret.WriteByte((byte)val);
								break;
						}
						break;

					case MinifierState.String:
						switch ((char)val)
						{
							case '\\':
								ret.WriteByte((byte)val);
								stat = MinifierState.StringEscape;
								break;

							case '"':
								ret.WriteByte((byte)val);
								stat = MinifierState.Unknown;
								break;

							default:
								ret.WriteByte((byte)val);
								break;
						}
						break;

					case MinifierState.StringEscape:
						ret.WriteByte((byte)val);
						stat = MinifierState.String;
						break;

					case MinifierState.Identifier:
						if (IsIdentifierChar((char)val))
						{
							ret.WriteByte((byte)val);
						}
						else
						{
							stat = MinifierState.Unknown;
							src.Position--;
							src.Flush();
						}
						break;
					case MinifierState.MaybeComment:
						if ((char)val == '/')
						{
							stat = MinifierState.SingleLineComment;
						}
						else if ((char)val == '*')
						{
							stat = MinifierState.BlockComment;
						}
						else
						{
							prevWasIdentifier = false;
							ret.WriteByte((byte)'/');
							src.Position--;
							src.Flush();
							stat = MinifierState.Unknown;
						}
						break;
					case MinifierState.SingleLineComment:
						switch ((char)val)
						{
							case '\r':
							case '\n':
								stat = MinifierState.Unknown;
								break;
							default:
								break;
						}
						break;
					case MinifierState.BlockComment:
						switch ((char)val)
						{
							case '*':
								stat = MinifierState.MaybeEndBlockComment;
								break;
							default:
								break;
						}
						break;
					case MinifierState.MaybeEndBlockComment:
						switch ((char)val)
						{
							case '/':
								stat = MinifierState.Unknown;
								break;
							default:
								stat = MinifierState.BlockComment;
								src.Position--;
								src.Flush();
								break;
						}
						break;

					case MinifierState.Compiler:
						if (!prevPrevWasCompiler)
							ret.WriteByte((byte)'\n');
						ret.WriteByte((byte)'#');
						switch ((char)val)
						{
							// if, ifdef, ifndef, include
							case 'i':
							case 'I':
								ret.WriteByte((byte)'i');
								val = src.ReadByte();
								switch ((char)val)
								{
									// if, ifdef, ifndef
									case 'f':
									case 'F':
										ret.WriteByte((byte)'f');
										val = src.ReadByte();
										switch ((char)val)
										{
											// ifndef
											case 'n':
											case 'N':
												ret.WriteByte((byte)'n');
												val = src.ReadByte();
												if ((char)val != 'd' && (char)val != 'D')
													goto default;
												ret.WriteByte((byte)'d');
												val = src.ReadByte();
												if ((char)val != 'e' && (char)val != 'E')
													goto default;
												ret.WriteByte((byte)'e');
												val = src.ReadByte();
												if ((char)val != 'f' && (char)val != 'F')
													goto default;
												ret.WriteByte((byte)'f');
												val = src.ReadByte();
												if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
													goto default;
												goto case ' '; // Cleanup that last character.
										
											// ifdef
											case 'd':
											case 'D':
												ret.WriteByte((byte)'d');
												val = src.ReadByte();
												if ((char)val != 'e' && (char)val != 'E')
													goto default;
												ret.WriteByte((byte)'e');
												val = src.ReadByte();
												if ((char)val != 'f' && (char)val != 'F')
													goto default;
												ret.WriteByte((byte)'f');
												val = src.ReadByte();
												if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
													goto default;
												goto case ' '; // Cleanup that last character.

											// if
											case unchecked((char)(-1)):
											case ' ':
											case '\t':
											case '\r':
											case '\n':
												src.Position--;
												src.Flush();
												break;

											default:
												goto default;
										}
										break;

									case 'n':
									case 'N':
										ret.WriteByte((byte)'n');
										val = src.ReadByte();
										if ((char)val != 'c' && (char)val != 'C')
											goto default;
										ret.WriteByte((byte)'c');
										val = src.ReadByte();
										if ((char)val != 'l' && (char)val != 'L')
											goto default;
										ret.WriteByte((byte)'l');
										val = src.ReadByte();
										if ((char)val != 'u' && (char)val != 'U')
											goto default;
										ret.WriteByte((byte)'u');
										val = src.ReadByte();
										if ((char)val != 'd' && (char)val != 'D')
											goto default;
										ret.WriteByte((byte)'d');
										val = src.ReadByte();
										if ((char)val != 'e' && (char)val != 'E')
											goto default;
										ret.WriteByte((byte)'e');
										ret.WriteByte((byte)' ');
										val = src.ReadByte();
										if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
											goto default;
										src.Position--;
										src.Flush();
										break;

									default:
										goto default;
								}
								break;

							// define
							case 'd':
							case 'D':
								ret.WriteByte((byte)'d');
								val = src.ReadByte();
								if ((char)val != 'e' && (char)val != 'E')
									goto default;
								ret.WriteByte((byte)'e');
								val = src.ReadByte();
								if ((char)val != 'f' && (char)val != 'F')
									goto default;
								ret.WriteByte((byte)'f');
								val = src.ReadByte();
								if ((char)val != 'i' && (char)val != 'I')
									goto default;
								ret.WriteByte((byte)'i');
								val = src.ReadByte();
								if ((char)val != 'n' && (char)val != 'N')
									goto default;
								ret.WriteByte((byte)'n');
								val = src.ReadByte();
								if ((char)val != 'e' && (char)val != 'E')
									goto default;
								ret.WriteByte((byte)'e');
								val = src.ReadByte();
								if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
									goto default;
								src.Position--;
								src.Flush();
								break;

							// else, elif, endif, error, extension
							case 'e':
							case 'E':
								ret.WriteByte((byte)'e');
								val = src.ReadByte();
								switch ((char)val)
								{
									// else, elif
									case 'l':
									case 'L':
										ret.WriteByte((byte)'l');
										val = src.ReadByte();
										switch ((char)val)
										{
											// elif
											case 'i':
											case 'I':
												ret.WriteByte((byte)'i');
												val = src.ReadByte();
												if ((char)val != 'f' && (char)val != 'F')
													goto default;
												ret.WriteByte((byte)'f');
												break;

											// else
											case 's':
											case 'S':
												ret.WriteByte((byte)'s');
												val = src.ReadByte();
												if ((char)val != 'e' && (char)val != 'E')
													goto default;
												ret.WriteByte((byte)'e');
												break;

											default:
												goto default;
										}
										val = src.ReadByte();
										if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
											goto default;
										src.Position--;
										src.Flush();
										break;

									// error
									case 'r':
									case 'R':
										ret.WriteByte((byte)'r');
										val = src.ReadByte();
										if ((char)val != 'r' && (char)val != 'R')
											goto default;
										ret.WriteByte((byte)'r');
										val = src.ReadByte();
										if ((char)val != 'o' && (char)val != 'O')
											goto default;
										ret.WriteByte((byte)'o');
										val = src.ReadByte();
										if ((char)val != 'r' && (char)val != 'R')
											goto default;
										ret.WriteByte((byte)'r');
										val = src.ReadByte();
										if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
											goto default;
										src.Position--;
										src.Flush();
										break;

									// endif
									case 'n':
									case 'N':
										ret.WriteByte((byte)'n');
										val = src.ReadByte();
										if ((char)val != 'd' && (char)val != 'D')
											goto default;
										ret.WriteByte((byte)'d');
										val = src.ReadByte();
										if ((char)val != 'i' && (char)val != 'I')
											goto default;
										ret.WriteByte((byte)'i');
										val = src.ReadByte();
										if ((char)val != 'f' && (char)val != 'F')
											goto default;
										ret.WriteByte((byte)'f');
										val = src.ReadByte();
										if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
											goto default;
										src.Position--;
										src.Flush();
										break;

									// extension
									case 'x':
									case 'X':
										ret.WriteByte((byte)'x');
										val = src.ReadByte();
										if ((char)val != 't' && (char)val != 'T')
											goto default;
										ret.WriteByte((byte)'t');
										val = src.ReadByte();
										if ((char)val != 'e' && (char)val != 'E')
											goto default;
										ret.WriteByte((byte)'e');
										val = src.ReadByte();
										if ((char)val != 'n' && (char)val != 'N')
											goto default;
										ret.WriteByte((byte)'n');
										val = src.ReadByte();
										if ((char)val != 's' && (char)val != 'S')
											goto default;
										ret.WriteByte((byte)'s');
										val = src.ReadByte();
										if ((char)val != 'i' && (char)val != 'I')
											goto default;
										ret.WriteByte((byte)'i');
										val = src.ReadByte();
										if ((char)val != 'o' && (char)val != 'O')
											goto default;
										ret.WriteByte((byte)'o');
										val = src.ReadByte();
										if ((char)val != 'n' && (char)val != 'N')
											goto default;
										ret.WriteByte((byte)'n');
										val = src.ReadByte();
										if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
											goto default;
										src.Position--;
										src.Flush();
										break;

									default:
										goto default;
								}
								break;

							// pragma
							case 'p':
							case 'P':
								ret.WriteByte((byte)'p');
								val = src.ReadByte();
								if ((char)val != 'r' && (char)val != 'R')
									goto default;
								ret.WriteByte((byte)'r');
								val = src.ReadByte();
								if ((char)val != 'a' && (char)val != 'A')
									goto default;
								ret.WriteByte((byte)'a');
								val = src.ReadByte();
								if ((char)val != 'g' && (char)val != 'G')
									goto default;
								ret.WriteByte((byte)'g');
								val = src.ReadByte();
								if ((char)val != 'm' && (char)val != 'M')
									goto default;
								ret.WriteByte((byte)'m');
								val = src.ReadByte();
								if ((char)val != 'a' && (char)val != 'A')
									goto default;
								ret.WriteByte((byte)'a');
								val = src.ReadByte();
								if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
									goto default;
								src.Position--;
								src.Flush();
								break;

							// undef
							case 'u':
							case 'U':
								ret.WriteByte((byte)'u');
								val = src.ReadByte();
								if ((char)val != 'n' && (char)val != 'N')
									goto default;
								ret.WriteByte((byte)'n');
								val = src.ReadByte();
								if ((char)val != 'd' && (char)val != 'D')
									goto default;
								ret.WriteByte((byte)'d');
								val = src.ReadByte();
								if ((char)val != 'e' && (char)val != 'E')
									goto default;
								ret.WriteByte((byte)'e');
								val = src.ReadByte();
								if ((char)val != 'f' && (char)val != 'F')
									goto default;
								ret.WriteByte((byte)'f');
								val = src.ReadByte();
								if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
									goto default;
								src.Position--;
								src.Flush();
								break;

							// version
							case 'v':
							case 'V':
								ret.WriteByte((byte)'v');
								val = src.ReadByte();
								if ((char)val != 'e' && (char)val != 'E')
									goto default;
								ret.WriteByte((byte)'e');
								val = src.ReadByte();
								if ((char)val != 'r' && (char)val != 'R')
									goto default;
								ret.WriteByte((byte)'r');
								val = src.ReadByte();
								if ((char)val != 's' && (char)val != 'S')
									goto default;
								ret.WriteByte((byte)'s');
								val = src.ReadByte();
								if ((char)val != 'i' && (char)val != 'I')
									goto default;
								ret.WriteByte((byte)'i');
								val = src.ReadByte();
								if ((char)val != 'o' && (char)val != 'O')
									goto default;
								ret.WriteByte((byte)'o');
								val = src.ReadByte();
								if ((char)val != 'n' && (char)val != 'N')
									goto default;
								ret.WriteByte((byte)'n');
								val = src.ReadByte();
								if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
									goto default;
								src.Position--;
								src.Flush();
								break;

							// line
							case 'l':
							case 'L':
								ret.WriteByte((byte)'l');
								val = src.ReadByte();
								if ((char)val != 'i' && (char)val != 'I')
									goto default;
								ret.WriteByte((byte)'i');
								val = src.ReadByte();
								if ((char)val != 'n' && (char)val != 'N')
									goto default;
								ret.WriteByte((byte)'n');
								val = src.ReadByte();
								if ((char)val != 'e' && (char)val != 'E')
									goto default;
								ret.WriteByte((byte)'e');
								val = src.ReadByte();
								if ((char)val != ' ' && (char)val != '\t' && (char)val != '\r' && (char)val != '\n' && val != -1)
									goto default;
								src.Position--;
								src.Flush();
								break;

							default:
								throw new Exception("Unknown compiler statement!");
						}
						prevWasIdentifier = true;
						prevWasCompiler = true;
						stat = MinifierState.Unknown;
						break;


					default:
						throw new Exception("Unknown MinifierState!");
				}
			}
			ret.WriteByte((byte)'\r');
			ret.WriteByte((byte)'\n');
			ret.Position = 0;
			return ret;
		}

		private static bool IsIdentifierChar(char c)
		{
			switch (c)
			{
				case 'a':
				case 'b':
				case 'c':
				case 'd':
				case 'e':
				case 'f':
				case 'g':
				case 'h':
				case 'i':
				case 'j':
				case 'k':
				case 'l':
				case 'm':
				case 'n':
				case 'o':
				case 'p':
				case 'q':
				case 'r':
				case 's':
				case 't':
				case 'u':
				case 'v':
				case 'w':
				case 'x':
				case 'y':
				case 'z':
				case 'A':
				case 'B':
				case 'C':
				case 'D':
				case 'E':
				case 'F':
				case 'G':
				case 'H':
				case 'I':
				case 'J':
				case 'K':
				case 'L':
				case 'M':
				case 'N':
				case 'O':
				case 'P':
				case 'Q':
				case 'R':
				case 'S':
				case 'T':
				case 'U':
				case 'V':
				case 'W':
				case 'X':
				case 'Y':
				case 'Z':
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case '_':
					return true;

				default:
					return false;
			}
		}
	}
}
