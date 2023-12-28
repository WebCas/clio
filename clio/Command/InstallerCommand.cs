using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Clio.Common;
using Clio.Common.db;
using Clio.Common.Docker;
using Clio.Common.K8;
using Clio.Common.ScenarioHandlers;
using Clio.UserEnvironment;
using CommandLine;
using Docker.DotNet.Models;
using MediatR;
using StackExchange.Redis;
using IFileSystem = Clio.Common.IFileSystem;

namespace Clio.Command;

[Verb("deploy-creatio", HelpText = "Deploy Creatio from zip file")]
public class PfInstallerOptions : EnvironmentNameOptions
{

	#region Properties: Public

	[Option("SiteName", Required = false, HelpText = "SiteName")]
	public string SiteName { get; set; }

	[Option("SitePort", Required = false, HelpText = "Site port")]
	public int SitePort { get; set; }

	[Option("ZipFile", Required = true, HelpText = "Sets Zip File path")]
	public string ZipFile { get; set; }

	[Option("UseDocker", Required = false, HelpText = "Sets Zip File path", Default = false)]
	public bool UseDocker { get; set; }
	
	[Option("FsContent", Required = false, HelpText = "FileSystemContent", Default = false)]
	public FsContent FsContent { get; set;}

	
	
	#endregion

}

public enum FsContent
{
	MINIMAL = 0,
	FULL = 1
}


public class InstallerCommand : Command<PfInstallerOptions>
{

	#region Fields: Private

	private static readonly Action<string, string, IProgress<double>> CopyFileWithProgress =
		(sourcePath, destinationPath, progress) => {
			const int bufferSize = 1024 * 1024; // 1MB
			byte[] buffer = new byte[bufferSize];
			int bytesRead;

			using FileStream sourceStream = new(sourcePath, FileMode.Open, FileAccess.Read);
			long totalBytes = sourceStream.Length;

			using FileStream destinationStream = new(destinationPath, FileMode.OpenOrCreate, FileAccess.Write);
			while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0) {
				destinationStream.Write(buffer, 0, bytesRead);
				// Report progress
				double percentage = 100d * sourceStream.Position / totalBytes;
				progress.Report(percentage);
			}
		};

	private string CopyZipLocal(string src){
		if (!Directory.Exists(_productFolder)) {
			Directory.CreateDirectory(_productFolder);
		}

		FileInfo srcInfo = new(src);
		string dest = Path.Join(_productFolder, srcInfo.Name);

		if (File.Exists(dest)) {
			return dest;
		}

		Console.WriteLine($"Detected network drive as source, copying to local folder {_productFolder}");
		Console.Write("Copy Progress:    ");
		Progress<double> progressReporter = new(progress => {
			string result = progress switch {
				< 10 => progress.ToString("0").PadLeft(2) + " %",
				< 100 => progress.ToString("0").PadLeft(1) + " %",
				100 => "100 %",
				_ => ""
			};
			Console.CursorLeft = 15;
			Console.Write(result);
		});
		CopyFileWithProgress(src, dest, progressReporter);
		return dest;
	}

	private string CopyLocalWhenNetworkDrive(string path){
		if (path.StartsWith(@"\\")) {
			return CopyZipLocal(path);
		}
		return new DriveInfo(Path.GetPathRoot(path)) switch {
			{DriveType: DriveType.Network} => CopyZipLocal(path),
			var _ => path
		};
	}
	private IDirectoryInfo _dockerDir;
	private ImageTag _tag;

	private readonly string _iisRootFolder;
	private readonly string _productFolder;
	private readonly IPackageArchiver _packageArchiver;
	private readonly k8Commands _k8;
	private readonly IMediator _mediator;
	private readonly RegAppCommand _registerCommand;
	private readonly ImageBuilder _imageBuilder;
	private readonly IFileSystem _fileSystem;
	private readonly ILogger _logger;

	#endregion

	#region Constructors: Public

	public InstallerCommand(IPackageArchiver packageArchiver, k8Commands k8,
		IMediator mediator, RegAppCommand registerCommand, ISettingsRepository settingsRepository,
		ImageBuilder imageBuilder, IFileSystem fileSystem, ILogger logger){
		_packageArchiver = packageArchiver;
		_k8 = k8;
		_mediator = mediator;
		_registerCommand = registerCommand;
		_imageBuilder = imageBuilder;
		_fileSystem = fileSystem;
		_logger = logger;
		_iisRootFolder = settingsRepository.GetIISClioRootPath();
		_productFolder = settingsRepository.GetCreatioProductsFolder();
	}

	#endregion

	#region Methods: Private

	private static int ExitWithErrorMessage(string message){
		Console.WriteLine(message);
		return 1;
	}

	private static int ExitWithOkMessage(string message){
		Console.WriteLine(message);
		return 0;
	}

	private static int FindEmptyRedisDb(int port){
		ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
		IServer server = redis.GetServer("localhost", port);
		int count = server.DatabaseCount;
		for (int i = 1; i < count; i++) {
			long records = server.DatabaseSize(i);
			if (records == 0) {
				return i;
			}
		}
		return -1;
	}

	private static int StartWebBrowser(PfInstallerOptions options){
		string url = $"http://{InstallerHelper.FetFQDN()}:{options.SitePort}";
		try {
			Process.Start(url);
			return 0;
		} catch {
			// Windows
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") {CreateNoWindow = true});
			}
			//Linux
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				Process.Start("xdg-open", url);
			}
			// macOS
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				Process.Start("open", url);
			}
			return 1;
		}
	}

	private async Task<int> CreateIisSite(DirectoryInfo unzippedDirectory, PfInstallerOptions options){
		Console.WriteLine("[Create IIS Site] - Started");
		CreateIISSiteRequest request = new() {
			Arguments = new Dictionary<string, string> {
				{"siteName", options.SiteName},
				{"port", options.SitePort.ToString()},
				{"sourceDirectory", unzippedDirectory.FullName},
				{"destinationDirectory", _iisRootFolder}, {
					"isNetFramework",
					(InstallerHelper.DetectFramework(unzippedDirectory) == InstallerHelper.FrameworkType.NetFramework)
					.ToString()
				}
			}
		};
		return (await _mediator.Send(request)).Value switch {
			(HandlerError error) => ExitWithErrorMessage(error.ErrorDescription),
			(CreateIISSiteResponse {Status: BaseHandlerResponse.CompletionStatus.Success} result) => ExitWithOkMessage(
				result.Description),
			(CreateIISSiteResponse {Status: BaseHandlerResponse.CompletionStatus.Failure} result) =>
				ExitWithErrorMessage(result.Description),
			_ => ExitWithErrorMessage("Unknown error occured")
		};
	}

	private async Task<int> CreateDockerImage(DirectoryInfo unzippedDirectory){
		var di = _fileSystem.DirectoryInfo.Wrap(unzippedDirectory);
		_tag = ImageBuilder.BuildTagFromDirName(di);
		bool imageExists = await _imageBuilder.CheckDockerImageExistsAsync(_tag);
		if(!imageExists) {
			await _imageBuilder.CreateNewDockerImageAsync(_tag, unzippedDirectory.FullName);
		}
		return 0;
	}

	private void CreatePgTemplate(DirectoryInfo unzippedDirectory, string tmpDbName){
		k8Commands.ConnectionStringParams csp = _k8.GetPostgresConnectionString();
		Postgres postgres = new(csp.DbPort, csp.DbUsername, csp.DbPassword);

		bool exists = postgres.CheckTemplateExists(tmpDbName);
		if (exists) {
			return;
		}
		FileInfo src = unzippedDirectory.GetDirectories("db").FirstOrDefault()?.GetFiles("*.backup").FirstOrDefault();
		_logger.WriteLine($"[Starting Database restore] - {DateTime.Now:hh:mm:ss}");

		_k8.CopyBackupFileToPod(k8Commands.PodType.Postgres, src.FullName, src.Name);

		postgres.CreateDb(tmpDbName);
		_k8.RestorePgDatabase(src.Name, tmpDbName);
		postgres.SetDatabaseAsTemplate(tmpDbName);
		_k8.DeleteBackupImage(k8Commands.PodType.Postgres, src.Name);
		_logger.WriteLine($"[Completed Database restore] - {DateTime.Now:hh:mm:ss}");
	}

	private int DoMsWork(DirectoryInfo unzippedDirectory, string siteName){
		FileInfo src = unzippedDirectory.GetDirectories("db").FirstOrDefault()?.GetFiles("*.bak").FirstOrDefault();
		_logger.WriteLine($"[Starting Database restore] - {DateTime.Now:hh:mm:ss}");
		_k8.CopyBackupFileToPod(k8Commands.PodType.Mssql, src.FullName, $"{siteName}.bak");

		k8Commands.ConnectionStringParams csp = _k8.GetMssqlConnectionString();
		Mssql mssql = new(csp.DbPort, csp.DbUsername, csp.DbPassword);

		bool exists = mssql.CheckDbExists(siteName);
		if (!exists) {
			mssql.CreateDb(siteName, $"{siteName}.bak");
		}
		_k8.DeleteBackupImage(k8Commands.PodType.Mssql, $"{siteName}.bak");
		return 0;
	}

	private int DoPgWork(DirectoryInfo unzippedDirectory, string destDbName){
		string tmpDbName = "template_" + unzippedDirectory.Name;
		k8Commands.ConnectionStringParams csp = _k8.GetPostgresConnectionString();
		Postgres postgres = new(csp.DbPort, csp.DbUsername, csp.DbPassword);

		CreatePgTemplate(unzippedDirectory, tmpDbName);
		postgres.CreateDbFromTemplate(tmpDbName, destDbName);
		_logger.WriteLine($"[Database created] - {destDbName}");
		return 0;
	}

	private async Task<int> CreateContainerFolder(DirectoryInfo unzippedDirectory, PfInstallerOptions options){
		IDirectoryInfo directoryInfo = _fileSystem.DirectoryInfo.Wrap(unzippedDirectory);
		_dockerDir = directoryInfo.Parent?.CreateSubdirectory($"Docker_{options.SiteName}");
		_logger.WriteInfo($"Container content folder: {_dockerDir.FullName}");
		switch (options.FsContent) {
			case FsContent.MINIMAL:
				CopyMinimalConfigFiles();
				break;
			case FsContent.FULL:
				CopyAllFiles();
				break;
			case var _:
				ExitWithErrorMessage("Unknown error occured");
				break;
		}

		_logger.WriteLine("[Update connection string] - Started");
		k8Commands.ConnectionStringParams csParam = _k8.GetPostgresConnectionString();
		int redisDb = FindEmptyRedisDb(csParam.RedisPort);
		
		ConfigureConnectionStringRequest request = new () {
				Arguments = new Dictionary<string, string> {
					{"folderPath", Path.Join(_dockerDir.FullName)}, {
						"dbString",
						$"Server={InstallerHelper.FetFQDN()};Port={csParam.DbPort};Database={options.SiteName};User ID={csParam.DbUsername};password={csParam.DbPassword};Timeout=500; CommandTimeout=400;MaxPoolSize=1024;"
					},
					{"redis", $"host={InstallerHelper.FetFQDN()};db={redisDb};port={csParam.RedisPort}"}, {
						"isNetFramework", false.ToString()
					}
				}
		};
		
		int mResult =  (await _mediator.Send(request)).Value switch {
			HandlerError error => ExitWithErrorMessage(error.ErrorDescription),
			ConfigureConnectionStringResponse {
				Status: BaseHandlerResponse.CompletionStatus.Success
			} result => ExitWithOkMessage(result.Description),
			ConfigureConnectionStringResponse {
				Status: BaseHandlerResponse.CompletionStatus.Failure
			} result => ExitWithErrorMessage(result.Description),
			var _ => ExitWithErrorMessage("Unknown error occured")
		};
		
		return mResult;

		void CopyMinimalConfigFiles(){
			IFileInfo cs = directoryInfo.GetFiles("ConnectionStrings.config").First();
			IFileInfo conf = directoryInfo.GetFiles("Terrasoft.WebHost.dll.config").First();
			_fileSystem.CopyFiles(new []{cs.FullName,conf.FullName}, _dockerDir.FullName, true);
		}
		void CopyAllFiles(){
			_fileSystem.CopyDirectory(unzippedDirectory.FullName, _dockerDir.FullName, true);
		}
	}
	
	private async Task<int> UpdateConnectionString(DirectoryInfo unzippedDirectory, PfInstallerOptions options){
		_logger.WriteLine("[Update connection string] - Started");
		InstallerHelper.DatabaseType dbType = InstallerHelper.DetectDataBase(unzippedDirectory);
		k8Commands.ConnectionStringParams csParam = dbType switch {
			InstallerHelper.DatabaseType.Postgres => _k8.GetPostgresConnectionString(),
			InstallerHelper.DatabaseType.MsSql => _k8.GetMssqlConnectionString()
		};

		int redisDb = FindEmptyRedisDb(csParam.RedisPort);

		ConfigureConnectionStringRequest request = dbType switch {
			InstallerHelper.DatabaseType.Postgres => new ConfigureConnectionStringRequest {
				Arguments = new Dictionary<string, string> {
					{"folderPath", Path.Join(_iisRootFolder, options.SiteName)}, {
						"dbString",
						$"Server=127.0.0.1;Port={csParam.DbPort};Database={options.SiteName};User ID={csParam.DbUsername};password={csParam.DbPassword};Timeout=500; CommandTimeout=400;MaxPoolSize=1024;"
					},
					{"redis", $"host=127.0.0.1;db={redisDb};port={csParam.RedisPort}"}, {
						"isNetFramework",
						options.UseDocker ? "False" : (InstallerHelper.DetectFramework(unzippedDirectory) == InstallerHelper.FrameworkType.NetFramework).ToString()
					}
				}
			},
			InstallerHelper.DatabaseType.MsSql => new ConfigureConnectionStringRequest {
				Arguments = new Dictionary<string, string> {
					{"folderPath", Path.Join(_iisRootFolder, options.SiteName)}, {
						"dbString",
						$"Data Source=127.0.0.1,{csParam.DbPort};Initial Catalog={options.SiteName};User Id={csParam.DbUsername}; Password={csParam.DbPassword};MultipleActiveResultSets=True;Pooling=true;Max Pool Size=100"
					},
					{"redis", $"host=127.0.0.1;db={redisDb};port={csParam.RedisPort}"}, {
						"isNetFramework",
						(InstallerHelper.DetectFramework(unzippedDirectory) ==
							InstallerHelper.FrameworkType.NetFramework).ToString()
					}
				}
			}
		};

		return (await _mediator.Send(request)).Value switch {
			HandlerError error => ExitWithErrorMessage(error.ErrorDescription),
			ConfigureConnectionStringResponse {
				Status: BaseHandlerResponse.CompletionStatus.Success
			} result => ExitWithOkMessage(result.Description),
			ConfigureConnectionStringResponse {
				Status: BaseHandlerResponse.CompletionStatus.Failure
			} result => ExitWithErrorMessage(result.Description),
			var _ => ExitWithErrorMessage("Unknown error occured")
		};
	}
	
	private static readonly Action<RegAppCommand, RegAppOptions> RegisterEnvironment =(cmd, opt)=> cmd.Execute(opt);
	#endregion

	#region Methods: Public

	public override int Execute(PfInstallerOptions options){
		if (!File.Exists(options.ZipFile)) {
			_logger.WriteLine($"Could not find zip file: {options.ZipFile}");
			return 1;
		}
		if (!Directory.Exists(_iisRootFolder)) {
			Directory.CreateDirectory(_iisRootFolder);
		}
		while (string.IsNullOrEmpty(options.SiteName)) {
			_logger.WriteLine("Please enter site name:");
			options.SiteName = Console.ReadLine();

			if (Directory.Exists(Path.Join(_iisRootFolder, options.SiteName))) {
				_logger.WriteLine(
					$"Site with name {options.SiteName} already exists in {Path.Join(_iisRootFolder, options.SiteName)}");
				options.SiteName = string.Empty;
			}
		}

		while (options.SitePort is <= 0 or > 65536) {
			_logger.WriteLine(
				$"Please enter site port, Max value - 65535:{Environment.NewLine}(recommended range between 40000 and 40100)");
			if (int.TryParse(Console.ReadLine(), out int value)) {
				options.SitePort = value;
			} else {
				_logger.WriteLine("Site port must be an in value");
			}
		}

		options.ZipFile = CopyLocalWhenNetworkDrive(options.ZipFile);
		_logger.WriteLine($"[Staring unzipping] - {options.ZipFile}");
		DirectoryInfo unzippedDirectory = InstallerHelper.UnzipOrTakeExisting(options.ZipFile, _packageArchiver);
		
		_logger.WriteLine($"[Unzip completed] - {unzippedDirectory.FullName}");
		_logger.WriteLine(string.Empty);
		
		int dbRestoreResult = InstallerHelper.DetectDataBase(unzippedDirectory) switch {
			InstallerHelper.DatabaseType.MsSql => DoMsWork(unzippedDirectory, options.SiteName),
			var _ => DoPgWork(unzippedDirectory, options.SiteName)
		};
		
		int createSiteResult = dbRestoreResult switch {
			0 when !options.UseDocker => CreateIisSite(unzippedDirectory, options).GetAwaiter().GetResult(),
			0 when options.UseDocker => CreateDockerImage(unzippedDirectory).GetAwaiter().GetResult(),
			var _ => ExitWithErrorMessage("Database restore failed")
		};

		int updateConnectionStringResult = createSiteResult switch {
			0 when !options.UseDocker => UpdateConnectionString(unzippedDirectory, options).GetAwaiter().GetResult(),
			0 when options.UseDocker => CreateContainerFolder(unzippedDirectory, options).GetAwaiter().GetResult(),
			var _ => ExitWithErrorMessage("Failed to update ConnectionString.config file")
		};
		
		if(options.UseDocker) {
			ContainerParams cp = new (_tag, options.SiteName, options.SitePort, options.SitePort+1, _dockerDir, options.FsContent);
			CreateContainerResponse container = _imageBuilder.CreateContainer(cp).GetAwaiter().GetResult();
			_imageBuilder.StartContainer(container.ID).GetAwaiter().GetResult();
		}
		
		RegAppOptions regOptions = options.UseDocker switch {
			false => new RegAppOptions {
				EnvironmentName = options.SiteName,
				Login = "Supervisor",
				Password = "Supervisor",
				Uri = $"http://{InstallerHelper.FetFQDN()}:{options.SitePort}",
				IsNetCore = InstallerHelper.DetectFramework(unzippedDirectory) == InstallerHelper.FrameworkType.NetCore},
			true => new RegAppOptions {
				EnvironmentName = options.SiteName,
				Login = "Supervisor",
				Password = "Supervisor",
				Uri = $"http://{InstallerHelper.FetFQDN()}:{options.SitePort}",
				IsNetCore = true
			}
		};
		RegisterEnvironment(_registerCommand, regOptions);
		WaitUntilItPings(regOptions);
		_ = updateConnectionStringResult switch {
			0 => StartWebBrowser(options),
			var _ => ExitWithErrorMessage($"Could not open: http://{InstallerHelper.FetFQDN()}:{options.SitePort}")
		};

		_logger.WriteLine("Press any key to exit...");
		Console.ReadKey();
		return 0;
	}
	
	
	private void WaitUntilItPings(RegAppOptions regOptions){
		_logger.WriteWarning($"Waiting for application to start on: {regOptions.Uri}");
		IApplicationClient client = new CreatioClientAdapter(regOptions.Uri, regOptions.Login, regOptions.Password, true);
		const string pingRoute = "/api/HealthCheck/Ping";
		client.ExecuteGetRequest(regOptions.Uri + pingRoute, -1, 20, 5);
	}
	#endregion

}