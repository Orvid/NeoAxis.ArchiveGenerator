using System;
using System.IO;

namespace ArchiveGenerator
{
	public static class SerializedCleaner
	{
		private enum SerialState : byte
		{
			Unknown,
			MaybeComment,
			Comment,
			AttributeOrNodeName,
			AttributeStart,
			AttributeEnd,
			Node,
		}

		public static Stream CleanFile(Stream src)
		{
			MemoryStream ret = new MemoryStream((int)src.Length);
			SerialState stat = SerialState.Unknown;
			bool spaceStarted = false;
			int val = -1;
			while ((val = src.ReadByte()) != -1)
			{
				switch (stat)
				{
					case SerialState.Unknown:
						switch ((char)val)
						{
							case ' ':
							case '\t':
							case '\r':
							case '\n':
								break;
							case '/':
								stat = SerialState.MaybeComment;
								break;
							default:
								stat = SerialState.AttributeOrNodeName;
								ret.WriteByte((byte)val);
								break;
						}
						spaceStarted = false;
						break;

					case SerialState.MaybeComment:
						switch ((char)val)
						{
							case '/':
								stat = SerialState.Comment;
								break;
							default:
								throw new Exception("Unexpected '/' character in the data stream!");
						}
						break;

					case SerialState.Comment:
						switch ((char)val)
						{
							case '\r':
							case '\n':
								stat = SerialState.Unknown;
								break;
							default:
								break;
						}
						break;

					case SerialState.AttributeOrNodeName:
						switch ((char)val)
						{
							case ' ':
								spaceStarted = true;
								break;
							case '\r':
							case '\n':
								stat = SerialState.Unknown;
								ret.WriteByte((byte)'\n');
								break;
							case '=':
								stat = SerialState.AttributeStart;
								ret.WriteByte((byte)'=');
								break;

							default:
								if (spaceStarted)
								{
									stat = SerialState.Node;
									ret.WriteByte((byte)' ');
									ret.WriteByte((byte)val);
								}
								else
								{
									ret.WriteByte((byte)val);
								}
								break;
						}
						break;

					case SerialState.AttributeStart:
						switch ((char)val)
						{
							case ' ':
								break;
							case '\r':
							case '\n':
								stat = SerialState.Unknown;
								ret.WriteByte((byte)'\n');
								break;
							default:
								stat = SerialState.AttributeEnd;
								ret.WriteByte((byte)val);
								break;
						}
						break;

					case SerialState.Node:
					case SerialState.AttributeEnd:
						switch ((char)val)
						{
							case '\r':
							case '\n':
								stat = SerialState.Unknown;
								ret.WriteByte((byte)'\n');
								break;
							default:
								ret.WriteByte((byte)val);
								break;
						}
						break;

					default:
						throw new Exception("Unknown SerialState!");
				}
			}
			ret.Position = 0;
			return ret;
		}
	}
}
