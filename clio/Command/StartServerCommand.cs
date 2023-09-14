using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using Autofac.Extensions.DependencyInjection;
using Clio.Common;
using Clio.SignalR;
using CommandLine;
using Creatio.Client;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneOf.Types;
using HttpMethod = System.Net.Http.HttpMethod;
using Task = Microsoft.Build.Utilities.Task;

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
			
			
			// https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor?view=aspnetcore-7.0&tabs=visual-studio&pivots=webassembly
			builder.Services.AddSignalR();
			builder.Services.AddResponseCompression(opts =>
			{
				opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
					new[] { "application/octet-stream" });
			});
			
			
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
			
			
			_app.MapMethods("/proxy/{env}/{*proxystring}", 
				new[] {
					HttpMethods.Get,
					HttpMethods.Delete,
					HttpMethods.Head,
					HttpMethods.Options,
					HttpMethods.Trace,
					HttpMethods.Connect,
				},
				(HttpContext context,string env, string proxystring) => ProxyHandlerHandler(context, env, proxystring)
				);
			
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
			_app.MapGet("/test",GetTestHandler );
			_app.MapHub<ChatHub>("/Chat");
		}

		private async Task<object> ProxyHandlerHandler(HttpContext cx, string env, string proxystring) {
			EnvironmentSettings myEnv =  new SettingsRepository().GetEnvironment(env);
			
			var handler = new HttpClientHandler();
			HttpClient myclient = new HttpClient(handler) {
				BaseAddress = new Uri(myEnv.Uri)
			};
			
			var json = JsonSerializer.Serialize(new {
				UserName = myEnv.Login, UserPassword = myEnv.Password
			}, new JsonSerializerOptions(){AllowTrailingCommas = true});
			
			var content = new StringContent(json);
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
			
			var loginResponse = await myclient.PostAsync("/ServiceModel/AuthService.svc/Login", content);
			var bpmcsrf = handler
				.CookieContainer
				.GetCookies(myclient.BaseAddress)
				.Where(c=> c.Name =="BPMCSRF")
				.Select(x => new { x.Name, x.Value})
				.FirstOrDefault();
				
			
			if(bpmcsrf is not null) {
				myclient.DefaultRequestHeaders.Add(bpmcsrf.Name, bpmcsrf.Value);
			}
			
			string url = string.IsNullOrWhiteSpace(proxystring)? string.Empty : "/"+proxystring;
			IQueryCollection q = cx.Request.Query;
			string fullUrl= q.Count switch {
				0 => url,
				_ => url + new QueryBuilder(q).ToQueryString()
			};
			
			
			cx.Request.Headers.TryGetValue("Content-Type", out var contentType);
			cx.Request.Headers.TryGetValue("Accept", out var accept);
			
			
			var getContent = new StringContent(string.Empty);
			if(!string.IsNullOrWhiteSpace(contentType.ToString())) {
				getContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType.ToString());
			}
			

			var msg = new HttpRequestMessage() {
				Content = getContent,
				Method = HttpMethod.Get,
				RequestUri = new Uri(proxystring, UriKind.Relative)
			};
			
			if(proxystring.EndsWith("ServiceModel/EntityDataService.svc/")) { }
			else if(proxystring.EndsWith("ServiceModel/EntityDataService.svc/$metadata")) { }
			else if(proxystring.EndsWith("Collection")){
				msg.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json;odata=verbose"));
			}
			else if(cx.Request.RouteValues["proxystring"] is not null && cx.Request.RouteValues["proxystring"].ToString().EndsWith("Collection")) {
				msg.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json;odata=verbose"));
			}
			else {
				msg.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
			}
			
			var payload = await myclient.SendAsync(msg);
			var result = await payload.Content.ReadAsStringAsync();
			
			payload.Headers.TryGetValues("Content-Type", out var contentTypeHeader);
			
			
			return result.StartsWith("<?xml") switch {
				true => Results.Content(result.Replace(myEnv.Uri, $"http://localhost:17000/proxy/{env}"),"application/xml",Encoding.UTF8),
				_ => Results.Content(result.Replace(myEnv.Uri, $"http://localhost:17000/proxy/{env}"), "application/json;odata=verbose;", Encoding.UTF8)
			};
		}
		
		//= OData.Feed("http://localhost:17000/proxy/bsn/0/ServiceModel/EntityDataService.svc/", null, [Implementation="2.0",ODataVersion = 3])
		

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

		
		private async Task<int> GetTestHandler(IHubContext<ChatHub> chat) {
			await chat.Clients.All.SendAsync("ReceiveMessage", "arg1_value", "arg2_value");
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





