using System;
using System.Globalization;
using System.IO;
using Clio.Common;
using Clio.Common.db;
using Clio.UserEnvironment;
using CommandLine;

namespace Clio.Command;

using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Clio.Utilities;

#region Class: RestoreDbCommandOptions

[Verb("restore-db", Aliases = new string[] {"rdb"}, HelpText = "Restores database from backup file")]
public class RestoreDbCommandOptions : EnvironmentOptions { }

#endregion

#region Class: RestoreDbCommand

public class RestoreDbCommand : Command<RestoreDbCommandOptions>
{

	#region Fields: Private

	private readonly ILogger _logger;
	private readonly IFileSystem _fileSystem;
	private readonly IDbClientFactory _dbClientFactory;
	private readonly ISettingsRepository _settingsRepository;

	#endregion

	#region Constructors: Public

	public RestoreDbCommand(ILogger logger, IFileSystem fileSystem, IDbClientFactory dbClientFactory, ISettingsRepository settingsRepository) {
		_logger = logger;
		_fileSystem = fileSystem;
		_dbClientFactory = dbClientFactory;
		_settingsRepository = settingsRepository;
	}

	#endregion

	#region Methods: Public

	public override int Execute(RestoreDbCommandOptions options) {
		EnvironmentSettings env = _settingsRepository.GetEnvironment(options);
		
		int result =  env.DbServer.Uri.Scheme switch {
			"mssql" => RestoreMs(env.DbServer, env.DbName, options.Force, env.BackupFilePath),
			"psql" => RestorePg(env.DbServer, env.DbName, options.Force, env.BackupFilePath),
			var _ => HandleIncorrectUri(env.DbServer.Uri.Scheme)
		};
		_logger.WriteLine("Done");
		return result;
	}

	private int HandleIncorrectUri(string uri){
		_logger.WriteError($"Scheme {uri} is not supported.\r\n\tExample: mssql://user:pass@127.0.01:1433 or\r\n\tpgsql://user:pass@127.0.01:5432");
		return 1;
	}
	private int RestoreMs(DbServer dbServer, string dbName, bool force, string backUpFilePath){
		Credentials credentials  = dbServer.GetCredentials();
		IMssql mssql = _dbClientFactory.CreateMssql(dbServer.Uri.Host, dbServer.Uri.Port, credentials.Username, credentials.Password);
		if(mssql.CheckDbExists(dbName)) {
			bool shouldDrop = force;
			if(!shouldDrop) {
				_logger.WriteWarning($"Database {dbName} already exists, would you like to keep it ? (Y / N)");
				var newName = Console.ReadLine();
				shouldDrop = newName.ToUpper(CultureInfo.InvariantCulture) == "N";
			}
			if(!shouldDrop) {
				string backupDbName = $"{dbName}_{DateTime.Now:yyyyMMddHHmmss}";
				mssql.RenameDb(dbName, backupDbName);
				_logger.WriteInfo($"Renamed existing database from {dbName} to {backupDbName}");
			}else {
				mssql.DropDb(dbName);
				_logger.WriteInfo($"Dropped existing database {dbName}");
			}
		}
		_fileSystem.CopyFiles(new[]{backUpFilePath}, dbServer.WorkingFolder, true);
		_logger.WriteInfo($"Copied backup file to server \r\n\tfrom: {backUpFilePath} \r\n\tto  : {dbServer.WorkingFolder}");
		
		_logger.WriteInfo("Started db restore...");
		var result =  mssql.CreateDb(dbName, Path.GetFileName(backUpFilePath)) ? 0 : 1;
		_logger.WriteInfo($"Created database {dbName} from file {backUpFilePath}");
		return result;
	}

	private int RestorePg(DbServer dbServer, string dbName, bool force, string backUpFilePath){
		
		
		
		Credentials credentials  = dbServer.GetCredentials();
		Postgres psql= _dbClientFactory.CreatePostgres(dbServer.Uri.Host, dbServer.Uri.Port, credentials.Username, credentials.Password);
		psql.CreateDb(dbName);
		InternalExecuteDbRestore(dbName, credentials, dbServer, backUpFilePath);
		return 0;
	}

	private static readonly Func<OSPlatform, string> DownloadPgTools = (platform) =>{
		
		// DowanlodFile
		// Unzip File
		
		return "";
	};
	
	private static readonly Action<string, Credentials, DbServer, string> InternalExecuteDbRestore =(dbName, credentials, dbServer,backUpFilePath)=> {
		string pgRestorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pg_tools", OsDirName(), "16",PgRestoreName());
		new ProcessExecutor().Execute(
			program: pgRestorePath, 
			command : $"--dbname={dbName} --host={dbServer.Uri.Host} --port={dbServer.Uri.Port} --username={credentials.Username} --no-owner --no-privileges {backUpFilePath}",
			waitForExit: true, 
			workingDirectory: new FileInfo(backUpFilePath).DirectoryName, 
			showOutput:true,
			environmentVariables: new StringDictionary {
				{
					// pg_restore uses PGPASSWORD env variable to pass password
					// see docs https://www.postgresql.org/docs/current/libpq-envars.html
					"PGPASSWORD", credentials.Password
				}
			});
	};

	private static readonly Func<string> OsDirName = () => OSPlatformChecker.GetOSPlatform() switch {
		var platform when platform == OSPlatform.Windows => "win",
		var platform when platform == OSPlatform.OSX => "osx",
		var platform when platform == OSPlatform.Linux => throw new PlatformNotSupportedException("Linux is not supported yet."),
		var platform when platform == OSPlatform.FreeBSD => throw new PlatformNotSupportedException("FreeBSN is not supported yet."),
		var _ => throw new PlatformNotSupportedException("Platform is not supported yet.")
	};
	
	private static readonly Func<string> PgRestoreName = () => OSPlatformChecker.GetOSPlatform() switch {
		var platform when platform == OSPlatform.Windows => "pg_restore.exe",
		var _ => "pg_restore"
	};
	#endregion
}

#endregion

