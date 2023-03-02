using Clio.Common;
using System.Threading.Tasks;

namespace Clio.Command
{
	public abstract class RemoteCommand<TEnvironmentOptions> : Command<TEnvironmentOptions>
		where TEnvironmentOptions : EnvironmentOptions
	{
		protected string RootPath => EnvironmentSettings.IsNetCore
			? EnvironmentSettings.Uri : EnvironmentSettings.Uri + @"/0";

		protected virtual string ServicePath {
			get; set;
		}

		protected string ServiceUri => RootPath + ServicePath;

		protected IApplicationClient ApplicationClient {
			get;
		}
		protected EnvironmentSettings EnvironmentSettings {
			get;
		}

		protected RemoteCommand(IApplicationClient applicationClient,
				EnvironmentSettings environmentSettings)
		{
			ApplicationClient = applicationClient;
			EnvironmentSettings = environmentSettings;
		}

		protected RemoteCommand(EnvironmentSettings environmentSettings)
		{
			EnvironmentSettings = environmentSettings;
		}

		protected int Login()
		{
			_logger?.LogInfo($"Try login to {EnvironmentSettings.Uri} with {EnvironmentSettings.Login ?? EnvironmentSettings.ClientId}");
			ApplicationClient.Login();
			_logger?.LogInfo("Login done");
			return 0;
		}


		public override int Execute(TEnvironmentOptions options)
		{
			ExecuteRemoteCommand(options);
			return 0;
		}

		private void ExecuteRemoteCommand(TEnvironmentOptions options)
		{
			ApplicationClient.ExecutePostRequest(ServiceUri, GetRequestData(options));
		}

		protected virtual string GetRequestData(TEnvironmentOptions options)
		{
			return "{}";
		}

		/// <summary>
		/// Validates that the environment is ready to start executing <paramref name="request"/> of type <typeparamref name="RestartOptions" />
		/// </summary>
		/// <param name="request"></param>
		/// <returns>
		/// <list type="bullet">
		/// <item>
		/// <term>When checks passes</term>
		/// <description>Result of <see cref="RemoteCommand{TEnvironmentOptions}.Execute(TEnvironmentOptions)"/></description>
		/// </item>
		/// <item>
		/// <term>When checks fails</term>
		/// <description>Never calls execute and immediately returns 1</description>
		/// </item>
		/// </list>
		/// </returns>
		protected override Task<int> PreflightCheck(TEnvironmentOptions request)
		{
			/* *******************************
			 Even though input parameters are valid,(see validation pipeline) this command may still be unable to execute.
			 For instance `clio restart TTT` will fail if TTT environment does not exit or incorrectly configured.
			 
			 We have few options:
			 1 - simply call Login and see if it fails, this does not provide proper feedback to the user because it returns
			 
					 RestartCommand: Try login to with  credentials...
					 Invalid URI: The format of the URI could not be determined.

			 2 - Try to validate dependencies
			 
			 
			 ******************************* */

			if (EnvironmentSettings.Uri is null)
			{
				_logger.LogError($"\tERROR (PF001) - Uri cannot be empty");
				return Task.FromResult(1);
			}

			if (string.IsNullOrWhiteSpace(EnvironmentSettings.Login) && string.IsNullOrWhiteSpace(EnvironmentSettings.ClientId))
			{
				_logger.LogError($"\tERROR (PF002) - Login and ClientId are empty, expected to see value ");
				return Task.FromResult(1);
			}

			if (string.IsNullOrWhiteSpace(EnvironmentSettings.Password) && string.IsNullOrWhiteSpace(EnvironmentSettings.ClientSecret))
			{
				_logger.LogError($"\tERROR (PF003) - Password and ClientSecret are empty, expected to see value ");
				return Task.FromResult(1);
			}

			if (Login() == 0)
			{
				return Task.FromResult(Execute(request));
			}
			else
			{
				return Task.FromResult(1);
			}
		}

	}
}
