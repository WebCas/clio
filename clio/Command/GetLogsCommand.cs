using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clio.Common;
using Clio.Package;
using CommandLine;

namespace Clio.Command
{

	#region Class: GetLogsOptions

	[Verb("get-logs", Aliases = new[] { "glogs" }, HelpText = "Get logs command")]
	public class GetLogsOptions : EnvironmentNameOptions
	{

		#region Properties: Public

		[Option("BeginDateTime", Required = true, Default = null,
			HelpText = "Begin date-time (yyyy-MM-dd HH:mm:ss)")]
		public string BeginDateTime { get; set; } = string.Empty;

		[Option("LastDateTime", Required = true, Default = null,
			HelpText = "Last date-time (yyyy-MM-dd HH:mm:ss)")]
		public string EndDateTime { get; set; }

		[Option("LogFolderPath", Required = false, Default = null,
			HelpText = "Folder path for download logs")]
		public string LogFolderPath { get; set; }


		[Option("IgnoredLoggers", Required = false, Default = null, HelpText = "Ignored loggers")]
		public string ExcludeLoggers { get; set; }


		#endregion

	}

	#endregion

	#region Class: GetLogsCommand

	public class GetLogsCommand : Command<GetLogsOptions>
	{

		#region Fields: Private

		private readonly IClioLogDownloader _clioLogDownloader;

		#endregion

		#region Constructors: Public

		public GetLogsCommand(IClioLogDownloader clioLogDownloader) {
			_clioLogDownloader = clioLogDownloader;
		}

		#endregion

		#region Methods: Public

		public override int Execute(GetLogsOptions options) {
			try {
				_clioLogDownloader.GetLogFile(options.BeginDateTime, options.EndDateTime, options.ExcludeLoggers,
					options.LogFolderPath);
				Console.WriteLine();
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				return 1;
			}
		}

		#endregion

	}

	#endregion

}