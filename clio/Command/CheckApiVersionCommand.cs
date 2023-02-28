using Clio.Common;
using Clio.UserEnvironment;
using Clio.Utilities;
using System;
using System.IO;

namespace Clio.Command {


	internal class CheckApiVersionOptions : EnvironmentOptions {
	}

	internal class CheckApiVersionCommand : Command<CheckApiVersionOptions> {
		private readonly IMessageConsole _messageConsole;
		private readonly IApplicationClient _applicationClient;
		private readonly ICreatioEnvironment _creatioEnvironment;

		public CheckApiVersionCommand(IMessageConsole messageConsole, IApplicationClient applicationClient, ICreatioEnvironment creatioEnvironment
		) {
			_messageConsole = messageConsole;
			_applicationClient = applicationClient;
			_creatioEnvironment = creatioEnvironment;
		}

		public override int Execute(CheckApiVersionOptions options) {
			var dir = AppDomain.CurrentDomain.BaseDirectory;
			string versionFilePath = Path.Combine(dir, "cliogate", "version.txt");
			var localApiVersion = new Version(File.ReadAllText(versionFilePath));
			var appApiVersion = GetAppApiVersion(options);
			if (appApiVersion == new Version("0.0.0.0"))
			{
				_messageConsole.WriteSuccess($"Your app does not contain clio API." +
				 $"{Environment.NewLine}Please consider installing it via the \'clio install-gate\' command.", ConsoleColor.DarkYellow);
			}
			else if (localApiVersion > appApiVersion)
			{
				_messageConsole.WriteSuccess($"You are using clio api version {appApiVersion}, however version {localApiVersion} is available." +
				 $"{Environment.NewLine}Please consider upgrading via the \'clio update-gate\' command.", ConsoleColor.DarkYellow);
			}
			return 0;
		}

		private Version GetAppApiVersion(CheckApiVersionOptions options) {
			string appVersionResponse = _applicationClient.ExecuteGetRequest(_creatioEnvironment.ApiVersionUrl).Trim('"');
			if (string.IsNullOrWhiteSpace(appVersionResponse))
			{
				return new Version("0.0.0.0");
			}
			return new Version(appVersionResponse);
		}
	}
}
