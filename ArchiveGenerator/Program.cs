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
		}
	}
}
