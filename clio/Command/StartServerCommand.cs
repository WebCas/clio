using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.Json;
using System.Threading;
using System.Web;
using Autofac.Extensions.DependencyInjection;
using Clio.Common;
using CommandLine;
using Creatio.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Clio.Command
{
	[Verb("start-server", Aliases = new string[] { "ss" }, HelpText = "Start web server")]
	public class StartServerOptions : EnvironmentNameOptions
	{

		//[Option("Port", Required = false, HelpText = "server port", Default = 9090)]
		[Value(0)]
		public string Content {
			get; set;
		}
		
		[Option("Port", Required = false, HelpText = "Default server port", Default = 19_999)]
		public int Port {
			get; set;
		}
	}

	public class StartServerCommand : RemoteCommand<StartServerOptions>
	{
		
		private readonly Microsoft.AspNetCore.Builder.WebApplication _app;
		public StartServerCommand(IApplicationClient applicationClient, EnvironmentSettings settings) :base(applicationClient, settings) {
			WebApplicationBuilder builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
			builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
			builder.Services.AddCors(options =>
			{
				options.AddPolicy("CorsPolicy",
					builder =>
					{
						builder.AllowAnyOrigin()
							.AllowAnyMethod()
							.AllowAnyHeader();
					});
			});
			
			_app = builder.Build();
			_app.UseCors("CorsPolicy");
			_app.MapGet("/ping", () => "pong");
			_app.MapGet("/environments", GetEnvironments);
			_app.MapGet("/restart/{env}", ([FromRoute] string env) => RestartHandler(env));
			_app.MapGet("/flush-redis/{env}", ([FromRoute] string env) => FlushRedisHandler(env));
			_app.MapGet("/install-gate/{env}", ([FromRoute] string env) => InstallGateHandler(env));
			
			//There is is a better way to do this, but since this is a prototype, I'll leave it as is
			_app.MapGet("/stop", ()=> {
				try {
					return "Terminating clio";
				} finally {
					Thread.Sleep(1000);
					_app.StopAsync();
				}
			});
			_app.MapGet("/packages/{env}", ([FromRoute] string env) => GetPackagesHandler(env));
		}

		public override int Execute(StartServerOptions options) {
			//Start with powershell: Start-Process -FilePath "clio-dev" -ArgumentList "start-server", "--Port 9000" -WindowStyle Hidden
			//clio-dev://start-server?port=9000
			Uri.TryCreate(options.Content, UriKind.Absolute, out Uri clioUri);
			if(clioUri is null) {
				_app.Urls.Add($"http://*:{options.Port}");
			}else {
				NameValueCollection qCollection = HttpUtility.ParseQueryString(clioUri.Query);
				var port = qCollection["port"];
				_app.Urls.Add($"http://*:{port}");
				
			}
			_app.Run();
			return 0;
		}

		private static int RestartHandler(string env) {
			EnvironmentSettings myEnv =  new SettingsRepository().GetEnvironment(env);
			CreatioClient client = string.IsNullOrEmpty(myEnv.ClientId) switch {
				true => new CreatioClient(myEnv.Uri, myEnv.Login, myEnv.Password, true, myEnv.IsNetCore),
				false => CreatioClient.CreateOAuth20Client(myEnv.Uri, myEnv.AuthAppUri, myEnv.ClientId, myEnv.ClientSecret, myEnv.IsNetCore)
			};
			RestartCommand restartCommand = new (new CreatioClientAdapter(client), myEnv);
			return restartCommand.Execute(null);
		}
		
		private static int FlushRedisHandler(string env) {
			EnvironmentSettings myEnv =  new SettingsRepository().GetEnvironment(env);
			CreatioClient client = string.IsNullOrEmpty(myEnv.ClientId) switch {
				true => new CreatioClient(myEnv.Uri, myEnv.Login, myEnv.Password, true, myEnv.IsNetCore),
				false => CreatioClient.CreateOAuth20Client(myEnv.Uri, myEnv.AuthAppUri, myEnv.ClientId, myEnv.ClientSecret, myEnv.IsNetCore)
			};
			
			RedisCommand restartCommand = new (new CreatioClientAdapter(client), myEnv);
			return restartCommand.Execute(null);
		}
		
		private static int InstallGateHandler(string env) {
			EnvironmentSettings myEnv =  new SettingsRepository().GetEnvironment(env);
			CreatioClient client = string.IsNullOrEmpty(myEnv.ClientId) switch {
				true => new CreatioClient(myEnv.Uri, myEnv.Login, myEnv.Password, true, myEnv.IsNetCore),
				false => CreatioClient.CreateOAuth20Client(myEnv.Uri, myEnv.AuthAppUri, myEnv.ClientId, myEnv.ClientSecret, myEnv.IsNetCore)
			};
			
			RedisCommand restartCommand = new (new CreatioClientAdapter(client), myEnv);
			return restartCommand.Execute(null);
		}
		
		private static Dictionary<string, EnvironmentSettings> GetEnvironments() => new SettingsRepository().GetEnvironments();
		
		private static PackagesDto GetPackagesHandler(string env) {
			EnvironmentSettings myEnv =  new SettingsRepository().GetEnvironment(env);
			CreatioClient client = string.IsNullOrEmpty(myEnv.ClientId) switch {
				true => new CreatioClient(myEnv.Uri, myEnv.Login, myEnv.Password, true, myEnv.IsNetCore),
				false => CreatioClient.CreateOAuth20Client(myEnv.Uri, myEnv.AuthAppUri, myEnv.ClientId, myEnv.ClientSecret, myEnv.IsNetCore)
			};
			
			const string route = "/ServiceModel/PackageService.svc/GetPackages";
			string url = myEnv.IsNetCore switch {
				true=> myEnv.Uri+route,
				false=> myEnv.Uri+"/0"+route
			};
			try {
				var data = client.ExecutePostRequest(url, string.Empty);
				return JsonSerializer.Deserialize<PackagesDto>(data);

			} catch (Exception e) {
				return default;
			}
			
		}
		private record PackagesDto(bool success, Package[] packages,object errorInfo);
		private record Package(
			string createdBy,
			string createdOn,
			string description,
			string id,
			bool isReadOnly,
			string maintainer,
			string modifiedBy,
			string modifiedOn,
			string name,
			int position,
			int type,
			string uId,
			string version
		);
	}
}





