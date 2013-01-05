using System;
using System.Diagnostics;
using System.Collections.Generic;
using Engine.FileSystem;
using Engine;
using Engine.Utils;
using Game;
using GameCommon;
using Engine.EntitySystem;
using Engine.Renderer;
using System.Reflection;
using System.Globalization;
using System.Reflection.Emit;

namespace Profiler
{
	class Program
	{
		private static void ProfileOut(string description, Stopwatch sw)
		{
			sw.Stop();
			Console.WriteLine(description + " took " + sw.ElapsedMilliseconds.ToString() + "MS");
			sw.Reset();
		}

		static void Main(string[] args)
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			if (!VirtualFileSystem.Init("user:Logs/Game.log", true, null, null, null))
				return;
			ProfileOut("VFS Startup", sw);

			sw.Start();
			EngineApp.ConfigName = "user:Configs/Game.config";
			if (PlatformInfo.Platform == PlatformInfo.Platforms.Windows)
				EngineApp.UseDirectInputForMouseRelativeMode = true;
			EngineApp.AllowJoysticksAndCustomInputDevices = true;
			EngineApp.AllowWriteEngineConfigFile = true;
			EngineApp.AllowChangeVideoMode = true;
			ProfileOut("EngineApp Pre-Initialization", sw);

			sw.Start();
			EngineApp.Init(new GameEngineApp());
			ProfileOut("EngineApp Initialization", sw);

			sw.Start();
			EngineApp.Instance.WindowTitle = "Game";
			ProfileOut("EngineApp Post-Initialization", sw);

			sw.Start();
			EngineConsole.Init();
			ProfileOut("EngineConsole Init", sw);

			sw.Start();
			EngineApp.Instance.Config.RegisterClassParameters(typeof(GameEngineApp));
			ProfileOut("EngineApp Class Parameters Registration", sw);

			sw.Start();
			EngineApp.Instance.Create();
			ProfileOut("EngineApp Instance Creation", sw);

			sw.Start();
			EngineApp.Shutdown();
			ProfileOut("EngineApp Shutdown", sw);


			sw.Start();
			VirtualFileSystem.Shutdown();
			ProfileOut("VFS Shutdown", sw);

			Console.ReadLine();
		}


	}

}
