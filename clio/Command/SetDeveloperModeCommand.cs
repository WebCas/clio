using Clio.Package;
using Clio.UserEnvironment;
using CommandLine;

namespace Clio.Command {


	[Verb("set-dev-mode", Aliases = new string[] { "dev", "unlock" }, HelpText = "Activate developer mode for selected environment")]
	internal class DeveloperModeOptions : EnvironmentOptions {
		[Value(0, MetaName = "Name", Required = false, HelpText = "Application name")]
		public string Name {
			get => Environment; set {
				Environment = value;
			}
		}
	}

	internal class SetDeveloperModeCommand : Command<DeveloperModeOptions> {
		private readonly SysSettingsCommand _sysSettingsCommand;
		private readonly RestartCommand _restartCommand;
		private readonly IPackageLockManager _packageLockManager;
		private readonly CheckApiVersionCommand _checkApiVersionCommand;

		public SetDeveloperModeCommand(SysSettingsCommand sysSettingsCommand, RestartCommand restartCommand,
		IPackageLockManager packageLockManager, CheckApiVersionCommand checkApiVersionCommand) {
			_sysSettingsCommand = sysSettingsCommand;
			_restartCommand = restartCommand;
			_packageLockManager = packageLockManager;
			_checkApiVersionCommand = checkApiVersionCommand;
		}
		public override int Execute(DeveloperModeOptions options) {

			var o = options as object as EnvironmentOptions as CheckApiVersionOptions;
			_checkApiVersionCommand.Execute(o);

			var repository = new SettingsRepository();
			CreatioEnvironment.Settings.DeveloperModeEnabled = true;
			repository.ConfigureEnvironment(CreatioEnvironment.EnvironmentName, CreatioEnvironment.Settings);
			var sysSettingOptions = new SysSettingsOptions
			{
				Code = "Maintainer",
				Value = CreatioEnvironment.Settings.Maintainer,
				Type = "Text"
			};

			_sysSettingsCommand.UpdateSysSetting(sysSettingOptions);
			_packageLockManager.Unlock();
			RestartOptions restartOptions = new()
			{
				Name = options?.Name,
				Uri = options?.Uri,
				Login = options?.Login,
				Password = options?.Password,
				ClientId = options?.ClientId,
				ClientSecret = options?.ClientSecret,
				Maintainer = options?.Maintainer,
				Safe = options?.Safe,
			};

			_restartCommand.Execute(restartOptions);
			return 0;
		}
	}
}
