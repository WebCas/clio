using Clio.Requests;
using CommandLine;

namespace Clio {
	public class EnvironmentOptions : AllCommandsRequest {
		[Option('u', "uri", Required = false, HelpText = "Application uri")]
		public string Uri {
			get; set;
		}

		[Option('p', "Password", Required = false, HelpText = "User password")]
		public string Password {
			get; set;
		}

		[Option('l', "Login", Required = false, HelpText = "User login (administrator permission required)")]
		public string Login {
			get; set;
		}

		[Option('i', "IsNetCore", Required = false, HelpText = "Use NetCore application)", Default = null)]
		public bool? IsNetCore {
			get; set;
		}

		[Option('e', "Environment", Required = false, HelpText = "Environment name")]
		public string Environment {
			get; set;
		}

		[Option('m', "Maintainer", Required = false, HelpText = "Maintainer name")]
		public string Maintainer {
			get; set;
		}

		[Option('c', "dev", Required = false, HelpText = "Developer mode state for environment")]
		public string DevMode {
			get; set;
		}

		public bool? DeveloperModeEnabled {
			get {
				if (!string.IsNullOrEmpty(DevMode))
				{
					if (bool.TryParse(DevMode, out bool result))
					{
						return result;
					}
				}
				return null;
			}
		}

		[Option('s', "Safe", Required = false, HelpText = "Safe action in this enviroment")]
		public string Safe {
			get; set;
		}

		[Option("clientId", Required = false, HelpText = "OAuth client id")]
		public string ClientId {
			get; set;
		}

		[Option("clientSecret", Required = false, HelpText = "OAuth client secret")]
		public string ClientSecret {
			get; set;
		}

		[Option("authAppUri", Required = false, HelpText = "OAuth app URI")]
		public string AuthAppUri {
			get; set;
		}

		public bool? SafeValue {
			get {
				if (!string.IsNullOrEmpty(Safe))
				{
					if (bool.TryParse(Safe, out bool result))
					{
						return result;
					}
				}
				return null;
			}
		}

	}

	public class EnvironmentNameOptions : EnvironmentOptions {
		[Value(0, MetaName = "Name", Required = false, HelpText = "Application name")]
		public string Name {
			get => Environment; set {
				Environment = value;
			}
		}
	}



	[Verb("install-gate", Aliases = new string[] { "update-gate", "gate", "installgate" }, HelpText = "Install clio api gateway to application")]
	internal class InstallGateOptions : EnvironmentNameOptions {
	}


}
