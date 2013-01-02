using System;
using System.Collections.Generic;
using System.Text;
using Orvid.Config;

namespace ArchiveGenerator
{
	internal static class Program
	{
		private static void Main(string[] args)
		{
			var options = new GeneratorOptions();
			if (!Configurator.Configure(options, args))
			    return;
			var gen = new Generator(options);
			gen.Generate();

			//using (var v = new System.IO.FileStream("Data/Gui/AboutWindow.gui", System.IO.FileMode.Open))
			//{
			//    var o = SerializedCleaner.CleanFile(v);
			//    using (var of = new System.IO.FileStream("Data/Gui/AboutWindow.Minified.gui", System.IO.FileMode.Create))
			//    {
			//        byte[] buf = new byte[o.Length];
			//        o.Read(buf, 0, buf.Length);
			//        of.Write(buf, 0, buf.Length);
			//    }
			//}

		}
	}
}
