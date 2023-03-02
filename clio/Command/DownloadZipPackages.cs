using Clio.Command.PackageCommand;
using Clio.Common;
using Clio.UserEnvironment;
using System;
using System.IO;
using System.Threading;

namespace Clio.Command
{
	internal class DownloadZipPackagesCommand : Command<PullPkgOptions>
	{
		private readonly IApplicationClient _applicationClient;
		private readonly IWorkingDirectoriesProvider _workingDirectoriesProvider;
		private readonly IPackageArchiver _packageArchiver;
		private readonly ICreatioEnvironment _creatioEnvironment;

		public DownloadZipPackagesCommand(IApplicationClient applicationClient, IWorkingDirectoriesProvider workingDirectoriesProvider,
		IPackageArchiver packageArchiver, ICreatioEnvironment creatioEnvironment)
		{
			_applicationClient = applicationClient;
			_workingDirectoriesProvider = workingDirectoriesProvider;
			_packageArchiver = packageArchiver;
			_creatioEnvironment = creatioEnvironment;
		}
		public override int Execute(PullPkgOptions options)
		{

			string packageName = options.Name;
			if (options.Unzip)
			{
				string destPath = options.DestPath ?? Environment.CurrentDirectory;
				_workingDirectoriesProvider.CreateTempDirectory(tempDirectory =>
				{
					string zipFilePath = Path.Combine(tempDirectory, $"{packageName}.zip");
					DownloadZipPackagesInternal(packageName, zipFilePath, options.Async);
					UnZipPackages(zipFilePath, destPath);
				});
			}
			else
			{
				string destPath = options.DestPath ?? Path.Combine(Environment.CurrentDirectory, $"{packageName}.zip");
				if (Directory.Exists(destPath))
				{
					destPath = Path.Combine(destPath, $"{packageName}.zip");
				}
				DownloadZipPackagesInternal(packageName, destPath, options.Async);
			}
			return 0;
		}

		private void DownloadZipPackagesInternal(string packageName, string destinationPath, bool _async)
		{
			_logger.LogInfo($"Start download packages ({packageName})");
			int count = 0;
			var packageNames = string.Format("\"{0}\"", packageName.Replace(" ", string.Empty).Replace(",", "\",\""));
			string requestData = "[" + packageNames + "]";

			var a = _creatioEnvironment.GetZipPackageUrl;
			if (!_async)
			{
				_applicationClient.DownloadFile(_creatioEnvironment.GetZipPackageUrl, destinationPath, requestData);
			}
			else
			{
				_applicationClient.ExecutePostRequest(_creatioEnvironment.DeleteExistsPackagesZipUrl, string.Empty, 10000);
				new Thread(() =>
				{
					try
					{
						_applicationClient.DownloadFile(_creatioEnvironment.GetZipPackageUrl, Path.GetTempFileName(), requestData);
					}
					catch { }
				}).Start();
				bool again = false;
				do
				{
					Thread.Sleep(2000);
					again = !bool.Parse(_applicationClient.ExecutePostRequest(_creatioEnvironment.ExistsPackageZipUrl, string.Empty));
					if (++count > 600)
					{
						throw new TimeoutException("Timeout exception");
					}
				} while (again);
				Thread.Sleep(1000);
				_applicationClient.DownloadFile(_creatioEnvironment.DownloadExistsPackageZipUrl, destinationPath, requestData);
			}
			_logger.LogInfo($"Download packages ({packageName}) completed");
		}
		private void UnZipPackages(string zipFilePath, string destinationPath)
		{
			_packageArchiver.ExtractPackages(zipFilePath, true, true, true, false, destinationPath);
		}

	}
}
