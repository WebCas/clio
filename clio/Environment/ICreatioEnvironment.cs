using System.IO;

namespace Clio.UserEnvironment {
	internal interface IResult {
		void ShowMessagesTo(TextWriter writer);
		void AppendMessage(string message);
	}

	internal interface ICreatioEnvironment {
		string GetZipPackageUrl {
			get;
		}

		string DeleteExistsPackagesZipUrl {
			get;
		}

		string ExistsPackageZipUrl {
			get;
		}

		string DownloadExistsPackageZipUrl {
			get;
		}

		string ApiVersionUrl {
			get;
		}

		string GetEntityModelsUrl {
			get;
		}

		string AppUrl {
			get;
		}
		string GetRegisteredPath();
		IResult UserRegisterPath(string path);
		IResult MachineRegisterPath(string path);

		string GetAssemblyFolderPath();
	}
}
