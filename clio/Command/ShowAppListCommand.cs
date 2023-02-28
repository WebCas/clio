﻿using Clio.Requests;
using Clio.UserEnvironment;
using CommandLine;
using System;

namespace Clio.Command {
	[Verb("show-web-app-list", Aliases = new string[] { "apps", "show-web-app", "app" }, HelpText = "Show the list of web applications and their settings")]
	public class AppListOptions : AllCommandsRequest {
		[Value(0, MetaName = "App name", Required = false, HelpText = "Name of application")]
		public string Name {
			get; set;
		}
	}

	public class ShowAppListCommand : Command<AppListOptions> {

		private readonly ISettingsRepository _settingsRepository;

		public ShowAppListCommand(ISettingsRepository settingsRepository) {
			_settingsRepository = settingsRepository;
		}

		public override int Execute(AppListOptions options) {
			try
			{
				_settingsRepository.ShowSettingsTo(Console.Out, options.Name);
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
