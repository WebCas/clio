using Clio.Common;
using Clio.Project;
using Clio.UserEnvironment;
using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Clio.Command {

	[Verb("add-item", Aliases = new string[] { "create" }, HelpText = "Create item in project")]
	internal class ItemOptions : EnvironmentOptions {
		[Value(0, MetaName = "Item type", Required = true, HelpText = "Item type")]
		public string ItemType {
			get; set;
		}

		[Value(1, MetaName = "Item name", Required = true, HelpText = "Item name")]
		public string ItemName {
			get; set;
		}


		[Option('d', "DestinationPath", Required = false, HelpText = "Path to source directory.", Default = null)]
		public string DestinationPath {
			get; set;
		}

		[Option('n', "Namespace", Required = false, HelpText = "Name space for service classes.", Default = null)]
		public string Namespace {
			get; set;
		}

		[Option('f', "Fields", Required = false, HelpText = "Required fields for model class", Default = null)]
		public string Fields {
			get; set;
		}

		[Option('a', "All", Required = false, HelpText = "Create all models", Default = true)]
		public bool CreateAll {
			get; set;
		}

		[Option('x', "Culture", Required = false, HelpText = "Description custure", Default = "en-US")]
		public string Culture {
			get; set;
		}
	}


	internal class AddItemCommand : Command<ItemOptions> {
		private readonly IApplicationClient _applicationClient;
		private readonly ICreatioEnvironment _creatioEnvironment;

		public AddItemCommand(IApplicationClient applicationClient, ICreatioEnvironment creatioEnvironment) {
			_applicationClient = applicationClient;
			_creatioEnvironment = creatioEnvironment;
		}
		public override int Execute(ItemOptions options) {
			if (options.ItemType.ToLower(CultureInfo.InvariantCulture) == "model")
			{
				return AddModels(options);
			}
			else
			{
				return AddItemFromTemplate(options);
			}
		}

		private static int AddItemFromTemplate(ItemOptions options) {
			var project = new VSProject(options.DestinationPath, options.Namespace);
			var creatioEnv = new CreatioEnvironment();
			string tplPath = $"tpl{Path.DirectorySeparatorChar}{options.ItemType}-template.tpl";
			if (!File.Exists(tplPath))
			{
				var envPath = creatioEnv.GetAssemblyFolderPath();
				if (!string.IsNullOrEmpty(envPath))
				{
					tplPath = Path.Combine(envPath, tplPath);
				}
			}
			string templateBody = File.ReadAllText(tplPath);
			project.AddFile(options.ItemName, templateBody.Replace("<Name>", options.ItemName));
			project.Reload();
			return 0;
		}

		private int AddModels(ItemOptions opts) {

			if (opts.CreateAll)
			{
				Console.WriteLine("Generating models...");


				ModelBuilder mb = new(_applicationClient, _creatioEnvironment.AppUrl, opts);
				mb.GetModels();
				return 0;
			}

			var models = GetClassModels(opts.ItemName, opts.Fields);
			var project = new VSProject(opts.DestinationPath, opts.Namespace);
			foreach (var model in models)
			{
				project.AddFile(model.Key, model.Value);
			}
			project.Reload();
			Console.WriteLine("Done");
			return 0;

		}

		private Dictionary<string, string> GetClassModels(string entitySchemaName, string fields) {
			var url = string.Format(_creatioEnvironment.GetEntityModelsUrl, entitySchemaName, fields);
			string responseFormServer = _applicationClient.ExecuteGetRequest(url);
			var result = CorrectJson(responseFormServer);
			return JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
		}

		private string CorrectJson(string body) {
			body = body.Replace("\\\\r\\\\n", Environment.NewLine);
			body = body.Replace("\\r\\n", Environment.NewLine);
			body = body.Replace("\\\\n", Environment.NewLine);
			body = body.Replace("\\n", Environment.NewLine);
			body = body.Replace("\\\\t", Convert.ToChar(9).ToString());
			body = body.Replace("\\\"", "\"");
			body = body.Replace("\\\\", "\\");
			body = body.Trim(new Char[] { '\"' });
			return body;
		}
	}
}
