using Clio.Common;
using Clio.UserEnvironment;
using System;
using System.IO;

namespace Clio.Command
{


	internal class CheckApiVersionOptions : EnvironmentOptions
	{
	}

	internal class CheckApiVersionCommand : Command<CheckApiVersionOptions>
	{
		private readonly IApplicationClient _applicationClient;
		private readonly ICreatioEnvironment _creatioEnvironment;

		public CheckApiVersionCommand(IApplicationClient applicationClient, ICreatioEnvironment creatioEnvironment)
		{
			_applicationClient = applicationClient;
			_creatioEnvironment = creatioEnvironment;
		}

		public override int Execute(CheckApiVersionOptions options)
		{
			var dir = AppDomain.CurrentDomain.BaseDirectory;
			string versionFilePath = Path.Combine(dir, "cliogate", "version.txt");
			var localApiVersion = new Version(File.ReadAllText(versionFilePath));
			var appApiVersion = GetAppApiVersion();
			if (appApiVersion == new Version("0.0.0.0"))
			{
				_logger.LogInfo($"Your app does not contain clio API." +
				 $"{Environment.NewLine}Please consider installing it via the \'clio install-gate\' command.");
			}
			else if (localApiVersion > appApiVersion)
			{
				_logger.LogInfo($"You are using clio api version {appApiVersion}, however version {localApiVersion} is available." +
				 $"{Environment.NewLine}Please consider upgrading via the \'clio update-gate\' command.");
			}
			return 0;
		}

		private Version GetAppApiVersion()
		{
			string appVersionResponse = _applicationClient.ExecuteGetRequest(_creatioEnvironment.ApiVersionUrl).Trim('"');
			if (string.IsNullOrWhiteSpace(appVersionResponse))
			{
				return new Version("0.0.0.0");
			}
			return new Version(appVersionResponse);
		}
	}
}
