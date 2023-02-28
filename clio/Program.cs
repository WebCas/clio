using Autofac;
using Clio.Command.UpdateCliCommand;
using Clio.UserEnvironment;
using Clio.Utilities;
using CommandLine;
using MediatR;
using OneOf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clio {

	class Program {

		public static bool Safe { get; private set; } = true;
		static string helpFolderName = $"help";
		static string helpDirectoryPath = helpFolderName;


		private static EnvironmentSettings GetEnvironmentSettings(EnvironmentOptions options) {
			var settingsRepository = new SettingsRepository();
			return settingsRepository.GetEnvironment(options);
		}

		private static OneOf<EnvironmentOptions, IEnumerable<Error>> GetOptionsFromArgs(string[] args) {
			Parser.Default.Settings.ShowHeader = false;
			Parser.Default.Settings.HelpDirectory = helpDirectoryPath;
			Parser.Default.Settings.CustomHelpViewer = new WikiHelpViewer();

			var parsedArgs = Parser.Default.ParseArguments(args, typeof(Program).Assembly.GetTypes());

			if (parsedArgs is NotParsed<object> obj)
			{
				return obj.Errors.ToList();
			}
			return (parsedArgs as Parsed<object>).Value as EnvironmentOptions;
		}


		private static void AutoUpdate() {
			var autoupdate = new SettingsRepository().GetAutoupdate();
			if (autoupdate)
			{
				new Thread(UpdateCliCommand.CheckUpdate).Start();
			}
		}



		private static async Task<int> Main(string[] args) {
			try
			{
				AutoUpdate();

				var opts = GetOptionsFromArgs(args);
				if (opts.Value is List<Error> && args.Length > 0)
				{
					Console.WriteLine("ERROR - Could not parse arguments");
					return 0;
				}
				var opt = opts.Value as EnvironmentOptions;

				EnvironmentSettings mySettings = GetEnvironmentSettings(opt);
				var myContainer = new BindingsModule().Register(mySettings);
				var envPath = myContainer.Resolve<ICreatioEnvironment>().GetAssemblyFolderPath();

				helpDirectoryPath = Path.Combine(envPath ?? string.Empty, helpFolderName);
				var console = myContainer.Resolve<IMessageConsole>();

				var mediator = myContainer.Resolve<IMediator>();
				var result = await mediator.Send(opt);
				if (result == 0)
				{
					console.WriteSuccess("Execution completed successfully");
				}
				else
				{
					console.WriteSuccess("Execution completed with errors");
				}
				return result;
			}
			catch (Exception ex)
			{
				var defColor = Console.ForegroundColor;
				Console.WriteLine(ex.Message, ConsoleColor.Red);
				Console.ForegroundColor = defColor;
				return 1;
			}
		}
	}

}
