using System;
using System.ComponentModel;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Clio.Common;
using CommandLine;
using Creatio.Client.Dto;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Clio.Command;

#region Class: ListenOptions

[Verb("listen", HelpText = "Subscribe to a websocket")]
public class ListenOptions : EnvironmentOptions
{

	[Option("loglevel", Required = false, HelpText = "Log level (ALL, Debug, Error, Fatal, Info, Trace, Warn)", Default = "All")]
	public string LogLevel { get; set; }

	[Option("logPattern", Required = false, HelpText = "Log pattern (i.e. ExceptNoisyLoggers)", Default = "")]
	public string LogPattern { get; set; }
	
	[Option("FileName", Required = false, HelpText = "File path to save logs into")]
	public string FileName { get; set; }

	[Option("Silent", Required = false, HelpText = "Disable messages in console", Default = false)]
	public bool Silent { get; set; }

}

#endregion

#region Class: ListenCommand

public class ListenCommand : Command<ListenOptions>
{
	
	private readonly IApplicationClient _applicationClient;
	private readonly ILogger _logger;
	private readonly EnvironmentSettings _environmentSettings;
	private readonly IFileSystem _fileSystem;
	//private const string StartLogBroadcast = "/rest/ATFLogService/StartLogBroadcast";
	//private const string StopLogBroadcast = "/rest/ATFLogService/ResetConfiguration";
	private string LogFilePath = string.Empty;
	private bool Silent;
	private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
	
	#region Constructors: Public
	
	public ListenCommand(IApplicationClient applicationClient,ILogger logger,EnvironmentSettings environmentSettings, IFileSystem fileSystem){
		_applicationClient = applicationClient;
		_logger = logger;
		_environmentSettings = environmentSettings;
		_fileSystem = fileSystem;
		_applicationClient.ConnectionStateChanged += OnConnectionStateChanged;
		_applicationClient.MessageReceived += OnMessageReceived;
	}
	#endregion

	#region Methods: Public

	public override int Execute(ListenOptions options){
		CancellationToken token = _cancellationTokenSource.Token;
		LogFilePath = options.FileName;
		Silent = options.Silent;
		_applicationClient.Listen(token, options.LogLevel, options.LogPattern);
		Console.ReadKey();
		_cancellationTokenSource.Cancel();
		return 0;
	}
	

	private void OnMessageReceived(object sender, WsMessage message){
		switch (message.Header.Sender)
		{
			case "TelemetryService":
				HandleTelemetryServiceMessages(message);
				break;
			default:
				//_logger.WriteLine(message.Body);
				break;
		}
	}
	
	private void HandleTelemetryServiceMessages(WsMessage message){
		if(!Silent) {
			_logger.WriteLine(message.Body);
		}
		if(!LogFilePath.IsNullOrEmpty()) {
			System.IO.File.AppendAllText(LogFilePath, Environment.NewLine+message.Body);
		}
	}
	
	private void OnConnectionStateChanged(object sender, WebSocketState state){
		_logger.WriteLine($"Connection state changed to {state}");
	}
	
	#endregion

}

#endregion

public record TelemetryMessage(LogPortion[] logPortion, int cpu, int ramMb);

public record LogPortion(
    string date,
    string level,
    object thread,
    string logger,
    string message,
    object stackTrace
);

