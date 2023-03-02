using Autofac;
using Clio.Command.UpdateCliCommand;
using Clio.Logger;
using Clio.UserEnvironment;
using CommandLine;
using MediatR;
using OneOf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clio
{

	class Program
	{

		public static bool Safe { get; private set; } = true;
		static string helpFolderName = $"help";
		static string helpDirectoryPath = helpFolderName;


		private static EnvironmentSettings GetEnvironmentSettings(EnvironmentOptions options)
		{
			var settingsRepository = new SettingsRepository();
			return settingsRepository.GetEnvironment(options);
		}

		private static OneOf<EnvironmentOptions, IEnumerable<Error>> GetOptionsFromArgs(string[] args)
		{
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


		private static void AutoUpdate()
		{
			var autoupdate = new SettingsRepository().GetAutoupdate();
			if (autoupdate)
			{
				new Thread(UpdateCliCommand.CheckUpdate).Start();
			}
		}



		private static async Task<int> Main(string[] args)
		{
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
				IContainer myContainer = new BindingsModule().Register(mySettings);
				var envPath = myContainer.Resolve<ICreatioEnvironment>().GetAssemblyFolderPath();

				helpDirectoryPath = Path.Combine(envPath ?? string.Empty, helpFolderName);
				var mediator = myContainer.Resolve<IMediator>();
				var _logger = myContainer.Resolve<ILogger<Program>>();
				var result = await mediator.Send(opt);
				if (result == 0)
				{
					await _logger.LogInfoAsync($"Execution completed successfully");
				}
				else
				{
					await _logger.LogErrorAsync($"Execution completed with error");
				}
				return result;
			}
			catch (Exception ex)
			{
				var defColor = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.DarkRed;
				Console.WriteLine();
				Console.WriteLine("*** ERROR ***");
				Console.WriteLine(ex.Message);
				Console.ForegroundColor = defColor;
				return 1;
			}
		}
	}

}
