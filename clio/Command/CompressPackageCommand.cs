﻿using Clio.Common;
using Clio.Requests;
using CommandLine;
using System;

namespace Clio.Command {
	[Verb("generate-pkg-zip", Aliases = new string[] { "compress" }, HelpText = "Prepare an archive of creatio package")]
	public class GeneratePkgZipOptions : AllCommandsRequest {
		[Value(0, MetaName = "Name", Required = false, HelpText = "Name of the compressed package")]
		public string Name {
			get; set;
		}

		[Option('d', "DestinationPath", Required = false, HelpText = "Full destination path for gz file")]
		public string DestinationPath {
			get; set;
		}

		[Option('p', "Packages", Required = false)]
		public string Packages {
			get; set;
		}

		[Option('s', "SkipPdb", Required = false, Default = false)]
		public bool SkipPdb {
			get; set;
		}
	}

	public class CompressPackageCommand : Command<GeneratePkgZipOptions> {
		private readonly IPackageArchiver _packageArchiver;

		public CompressPackageCommand(IPackageArchiver packageArchiver) {
			_packageArchiver = packageArchiver;
		}
		public override int Execute(GeneratePkgZipOptions options) {
			try
			{
				if (options.Packages == null)
				{
					var destinationPath = string.IsNullOrEmpty(options.DestinationPath) ? $"{options.Name}.gz" : options.DestinationPath;
					_packageArchiver.Pack(options.Name, destinationPath, options.SkipPdb, true);
				}
				else
				{
					var packages = StringParser.ParseArray(options.Packages);
					string zipFileName = $"packages_{DateTime.Now.ToString("yy.MM.dd_hh.mm.ss")}.zip";
					var destinationPath = string.IsNullOrEmpty(options.DestinationPath) ? zipFileName : options.DestinationPath;
					_packageArchiver.Pack(options.Name, destinationPath, packages, options.SkipPdb, true);
				}
				Console.WriteLine("Done");
				return 0;
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				return 1;
			}
		}
	}
}
