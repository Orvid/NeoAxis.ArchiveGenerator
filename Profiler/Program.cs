using System;
using System.Diagnostics;
using System.Collections.Generic;
using Engine.FileSystem;

namespace Profiler
{
	class Program
	{
		static void Main(string[] args)
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			if (!VirtualFileSystem.Init("user:Logs/Game.log", true, null, null, null))
				return;
			sw.Stop();

			Console.WriteLine("VFS Startup took " + sw.ElapsedMilliseconds.ToString() + "MS");

			sw.Reset();

			sw.Start();
			VirtualFileSystem.Shutdown();
			sw.Stop();

			Console.WriteLine("VFS Shutdown took " + sw.ElapsedMilliseconds.ToString() + "MS");

			Console.ReadLine();
		}
	}
}
