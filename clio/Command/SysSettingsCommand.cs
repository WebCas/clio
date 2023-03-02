using Clio.Common;
using CommandLine;
using System;

namespace Clio.Command
{
	[Verb("set-syssetting", Aliases = new string[] { "syssetting" }, HelpText = "Set setting value")]
	internal class SysSettingsOptions : EnvironmentOptions
	{
		[Value(0, MetaName = "Code", Required = true, HelpText = "Syssetting code")]
		public string Code {
			get; set;
		}

		[Value(1, MetaName = "Value", Required = true, HelpText = "Syssetting Value")]
		public string Value {
			get; set;
		}

		[Value(2, MetaName = "Type", Required = false, HelpText = "Type", Default = "Boolean")]
		public string Type {
			get; set;
		}

	}

	class SysSettingsCommand : RemoteCommand<SysSettingsOptions>
	{

		public SysSettingsCommand(IApplicationClient applicationClient, EnvironmentSettings settings)
			: base(applicationClient, settings)
		{ }

		private string InsertSysSettingsUrl => RootPath + @"/DataService/json/SyncReply/InsertSysSettingRequest";
		private string PostSysSettingsValuesUrl => RootPath + @"/DataService/json/SyncReply/PostSysSettingsValues";

		private void CreateSysSetting(SysSettingsOptions opts)
		{
			//BUG: Use proper Json Serializer
			Guid id = Guid.NewGuid();
			string requestData = "{" + string.Format("\"id\":\"{0}\",\"name\":\"{1}\",\"code\":\"{1}\",\"valueTypeName\":\"{2}\",\"isCacheable\":true",
				id, opts.Code, opts.Type) + "}";
			try
			{
				ApplicationClient.ExecutePostRequest(InsertSysSettingsUrl, requestData);
				_logger.LogInfo($"SysSettings with code: {opts.Code} created.");
			}
			catch
			{
				Console.WriteLine("SysSettings with code: {0} already exists.", opts.Code);
			}
		}

		public void UpdateSysSetting(SysSettingsOptions opts, EnvironmentSettings settings = null)
		{
			//BUG: Use proper Json Serializer
			string requestData = string.Empty;
			if (opts.Type.Contains("Text"))
			{
				//Enclosed opts.Value in "", otherwise update fails for all text settings
				requestData = "{\"isPersonal\":false,\"sysSettingsValues\":{" + string.Format("\"{0}\":\"{1}\"", opts.Code, opts.Value) + "}}";
			}
			else
			{
				requestData = "{\"isPersonal\":false,\"sysSettingsValues\":{" + string.Format("\"{0}\":{1}", opts.Code, opts.Value) + "}}";
			}
			ApplicationClient.ExecutePostRequest(PostSysSettingsValuesUrl, requestData);
			_logger.LogInfo($"SysSettings with code: {opts.Code} updated.");
		}

		public override int Execute(SysSettingsOptions opts)
		{

			//TODO: This is not clear, why Create and then Update ?
			try
			{
				CreateSysSetting(opts);
				UpdateSysSetting(opts);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error during set setting value occured with message: {ex.Message}");
				return 1;
			}
			return 0;
		}

	}
}
