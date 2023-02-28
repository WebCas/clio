using Clio.Common;
using System.IO;

namespace Clio.Command.PackageCommand {
	internal class InstallGateCommand : Command<InstallGateOptions> {
		private readonly IWorkingDirectoriesProvider _workingDirectoriesProvider;
		private readonly PushPackageCommand _pushPackageCommand;

		public InstallGateCommand(IWorkingDirectoriesProvider workingDirectoriesProvider, PushPackageCommand pushPackageCommand) {
			_workingDirectoriesProvider = workingDirectoriesProvider;
			_pushPackageCommand = pushPackageCommand;
		}
		public override int Execute(InstallGateOptions options) {

			var opt = CreatePushPkgOptions(options);
			_pushPackageCommand.Execute(opt);

			return 0;
		}
		private PushPkgOptions CreatePushPkgOptions(InstallGateOptions options) {
			var settingsRepository = new SettingsRepository();
			var settings = settingsRepository.GetEnvironment(options);
			string packageName = settings.IsNetCore ? "cliogate_netcore" : "cliogate";
			string packagePath = Path.Combine(_workingDirectoriesProvider.ExecutingDirectory, "cliogate",
				$"{packageName}.gz");
			return new PushPkgOptions
			{
				Environment = options.Environment,
				Name = packagePath,
				Login = options.Login,
				Uri = options.Uri,
				Password = options.Password,
				Maintainer = options.Maintainer,
				IsNetCore = options.IsNetCore,
				AuthAppUri = options.AuthAppUri,
				ClientSecret = options.ClientSecret,
				ClientId = options.ClientId
			};
		}
	}
}
