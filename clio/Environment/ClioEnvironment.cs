﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Clio.UserEnvironment {
	internal class CreatioEnvironment : ICreatioEnvironment {
		private const string PathVariableName = "PATH";

		public static bool IsNetCore => Settings.IsNetCore;
		public static string EnvironmentName {
			get; set;
		}
		public static EnvironmentSettings Settings {
			get; set;
		}

		public string GetZipPackageUrl {
			get => AppUrl + @"/ServiceModel/PackageInstallerService.svc/GetZipPackages";
		}
		public string DeleteExistsPackagesZipUrl {
			get => AppUrl + @"/rest/PackagesGateway/DeleteExistsPackagesZip";
		}
		public string ExistsPackageZipUrl {
			get => AppUrl + @"/rest/PackagesGateway/ExistsPackageZip";
		}
		public string DownloadExistsPackageZipUrl {
			get => AppUrl + @"/rest/PackagesGateway/DownloadExistsPackageZip";
		}
		public string ApiVersionUrl {
			get => AppUrl + @"/rest/CreatioApiGateway/GetApiVersion";
		}
		public string GetEntityModelsUrl {
			get => AppUrl + @"/rest/CreatioApiGateway/GetEntitySchemaModels/{0}/{1}";
		}

		public string AppUrl {
			get {
				return Settings.IsNetCore ? Settings.Uri : Settings.Uri + @"/0";
			}
		}

		private IResult RegisterPath(string path, EnvironmentVariableTarget target) {
			var result = new EnvironmentResult();
			string pathValue = Environment.GetEnvironmentVariable(PathVariableName, target);
			if (string.IsNullOrEmpty(pathValue))
			{
				pathValue = string.Empty;
			}
			if (pathValue.Contains(path))
			{
				result.AppendMessage($"{PathVariableName} variable already registered!");
				return result;
			}
			result.AppendMessage($"register path {path} in {PathVariableName} variable.");
			var value = string.Concat(pathValue, Path.PathSeparator + path.Trim(Path.PathSeparator));
			Environment.SetEnvironmentVariable(PathVariableName, value, target);
			result.AppendMessage($"{PathVariableName} variable registered.");
			return result;
		}

		private IResult UnregisterPath(EnvironmentVariableTarget target) {
			var result = new EnvironmentResult();
			string pathValue = Environment.GetEnvironmentVariable(PathVariableName, target);
			var paths = pathValue.Split(Path.PathSeparator);
			string clioPath = string.Empty;
			foreach (var path in paths)
			{
				if (Directory.Exists(path))
				{
					var dir = new DirectoryInfo(path);
					var files = dir.GetFiles("clio.cmd");
					if (files.Length > 0)
					{
						clioPath = path;
						break;
					}
				}
			}
			if (string.IsNullOrEmpty(clioPath))
			{
				result.AppendMessage($"Application already unregistered!");
				return result;
			}
			result.AppendMessage($"Unregister path {clioPath} in {PathVariableName} variable.");
			string newValue = pathValue.Replace(clioPath, string.Empty).Replace(String.Concat(Path.PathSeparator, Path.PathSeparator), Path.PathSeparator.ToString());
			Environment.SetEnvironmentVariable(PathVariableName, newValue, target);
			result.AppendMessage($"{PathVariableName} variable unregistered.");
			return result;
		}

		public string GetRegisteredPath() {
			var environmentPath = Environment.GetEnvironmentVariable(PathVariableName);
			string[] cliPath = (environmentPath?.Split(Path.PathSeparator));
			return cliPath?.FirstOrDefault(p => p.Contains("clio"));
		}

		public IResult UserRegisterPath(string path) {
			return RegisterPath(path, EnvironmentVariableTarget.User);
		}

		public IResult MachineRegisterPath(string path) {
			return RegisterPath(path, EnvironmentVariableTarget.Machine);
		}

		public IResult MachineUnregisterPath() {
			return UnregisterPath(EnvironmentVariableTarget.Machine);
		}

		public IResult UserUnregisterPath() {
			return UnregisterPath(EnvironmentVariableTarget.User);
		}

		public string GetAssemblyFolderPath() {
			return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		}

	}
}
