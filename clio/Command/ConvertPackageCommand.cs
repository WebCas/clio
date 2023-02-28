using Clio.Requests;
using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Clio.Command {

	[Verb("convert", HelpText = "Convert package to project", Hidden = true)]
	internal class ConvertOptions : AllCommandsRequest {
		[Option('p', "Path", Required = false,
			HelpText = "Path to package directory", Default = null)]
		public string Path {
			get; set;
		}

		[Value(0, MetaName = "<package names>", Required = false,
			HelpText = "Name of the convert instance (or comma separated names)")]
		public string Name {
			get; set;
		}

		[Option('c', "ConvertSourceCode", Required = false, HelpText = "Convert source code schema to files", Default = false)]
		public bool ConvertSourceCode {
			get; set;
		}


		[Usage(ApplicationAlias = "clio")]
		public static IEnumerable<Example> Examples =>
			new List<Example> {
				new Example("Convert existing packages",
					new ConvertOptions { Path = "C:\\Pkg\\" , Name = "MyApp,MyIntegration"}
				),
				new Example("Convert all packages in folder",
					new ConvertOptions { Path = "C:\\Pkg\\"}
				)
			};
	}


	internal class ConvertPackageCommand : Command<ConvertOptions> {
		public override int Execute(ConvertOptions options) {
			return PackageConverter.Convert(options);
		}
	}
}
