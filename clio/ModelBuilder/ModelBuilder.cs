using Clio.Command;
using Clio.Common;
using Clio.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Clio {
	internal class ModelBuilder {
		private readonly IApplicationClient _creatioClient;
		private readonly string _appUrl;
		private readonly ItemOptions _opts;

		private string EntitySchemaManagerRequestUrl => _appUrl + @"/DataService/json/SyncReply/EntitySchemaManagerRequest";
		private string RuntimeEntitySchemaRequestUrl => _appUrl + @"/DataService/json/SyncReply/RuntimeEntitySchemaRequest";
		private readonly Dictionary<string, Schema> _schemas = new Dictionary<string, Schema>();

		public ModelBuilder(IApplicationClient creatioClient, string appUrl, ItemOptions opts) {
			_creatioClient = creatioClient;
			_appUrl = appUrl;
			_opts = opts;
		}

		public void GetModels() {
			GetEntitySchemasAsync();

			Parallel.ForEach(_schemas, new ParallelOptions() { MaxDegreeOfParallelism = 16 },
			a =>
			{
				GetRuntimeEntitySchema(a);
			});

			foreach (var schema in _schemas)
			{
				var di = new DirectoryInfo(_opts.DestinationPath);
				if (!di.Exists)
				{
					di.Create();
				}

				var filePath = Path.Combine(_opts.DestinationPath, schema.Key + ".cs");
				File.WriteAllText(filePath, CreateClassFileText(schema));
			}
		}

		private void GetEntitySchemasAsync() {
			var responseJson = _creatioClient.ExecutePostRequest(EntitySchemaManagerRequestUrl, string.Empty);
			var col = JsonConvert.DeserializeObject<EntitySchemaResponse>(responseJson);
			foreach (var item in col.Collection)
			{
				if (!_schemas.ContainsKey(item.Name))
				{
					_schemas.Add(item.Name, new Schema
					{
						Name = item.Name,
					});
				}
			}
		}

		private void GetRuntimeEntitySchema(KeyValuePair<string, Schema> schema) {
			string definition = _creatioClient.ExecutePostRequest(RuntimeEntitySchemaRequestUrl, "{\"Name\" : \"" + schema.Key + "\"}");

			JToken jt = JToken.Parse(definition);
			var items = jt.SelectToken("$.schema.columns.Items");

			schema.Value.Description = jt.SelectToken($"$.schema.description.{_opts.Culture}")?.ToString();
			Dictionary<string, Column> dict = new Dictionary<string, Column>();
			var columns = items.ToObject<Dictionary<Guid, JObject>>();

			foreach (var item in columns)
			{
				var col = new Column
				{
					Name = (string)item.Value.GetValue("name"),
					DataValueType = (int)item.Value.GetValue("dataValueType")
				};
				if (item.Value.TryGetValue("referenceSchemaName", out JToken value))
				{
					col.ReferenceSchemaName = (string)value;
				}

				col.Description = item.Value.SelectToken($"$.description.{_opts.Culture}")?.ToString();
				schema.Value.Columns.Add(col.Name, col);
			}
		}

		private string CreateClassFileText(KeyValuePair<string, Schema> schema) {
			StringBuilder sb = new StringBuilder();

			sb.AppendLine("using ATF.Repository;")
				.AppendLine("using ATF.Repository.Attributes;")
				.AppendLine("using System;")
				.AppendLine("using System.Diagnostics.CodeAnalysis;")
				.AppendLine()
				.Append("namespace ").AppendLine(_opts.Namespace)
				.AppendLine("{")
				.AppendLine();


			if (!string.IsNullOrEmpty(schema.Value.Description))
			{
				sb
				.Append("\t").AppendLine("/// <summary>")
				.Append("\t").Append("/// ").AppendLine(schema.Value.Description)
				.Append("\t").AppendLine("/// </summary>");
			}
			sb.Append("\t").AppendLine("[ExcludeFromCodeCoverage]");
			sb.Append("\t").Append("[Schema(\"").Append(schema.Value.Name).AppendLine("\")]")
				.Append("\t").Append("public class ").Append(schema.Value.Name).AppendLine(": BaseModel")
				.Append("\t").AppendLine("{")
				.AppendLine();
			foreach (var column in schema.Value.Columns)
			{
				if (column.Key == "Id")
					continue;

				if (!string.IsNullOrEmpty(column.Value.Description))
				{
					sb
					.Append("\t\t").AppendLine("/// <summary>")
					.Append("\t\t").Append("/// ").AppendLine(column.Value.Description)
					.Append("\t\t").AppendLine("/// </summary>");
				}

				sb.Append("\t\t").Append("[SchemaProperty(\"").Append(column.Value.Name).AppendLine("\")]");
				if (string.IsNullOrEmpty(column.Value.ReferenceSchemaName))
				{
					sb.Append("\t\t").Append("public ").Append(GetTypeFromDataValueType(column.Value.DataValueType)).Append(" ").Append(column.Value.Name).AppendLine(" { get; set; }");
				}
				else
				{
					sb.Append("\t\t").Append("public ").Append(GetTypeFromDataValueType(column.Value.DataValueType)).Append(" ").Append(column.Value.Name).Append("Id").AppendLine(" { get; set; }");
					sb.AppendLine();

					if (!string.IsNullOrEmpty(column.Value.Description))
					{
						sb.Append("\t\t").Append("/// <inheritdoc cref=\"").Append(column.Value.Name).Append("Id").AppendLine("\"/>");
					}

					sb.Append("\t\t").Append("[LookupProperty(\"").Append(column.Value.Name).AppendLine("\")]");
					sb.Append("\t\t").Append("public virtual ").Append(column.Value.ReferenceSchemaName).Append(" ").Append(column.Value.Name).AppendLine(" { get; set; }");
				}
				sb.AppendLine();
			}
			sb.AppendLine("\t}");
			sb.AppendLine("}");
			var x = sb.ToString();
			return sb.ToString();
		}

		private string GetTypeFromDataValueType(int dataValueType) {
			return dataValueType switch
			{
				0 => nameof(Guid),
				1 => nameof(String),
				4 => nameof(Int32),
				5 => nameof(Single),
				6 => nameof(Decimal),
				7 => nameof(DateTime),
				8 => nameof(DateTime),
				9 => nameof(DateTime),
				10 => nameof(Guid),
				11 => nameof(Guid),
				12 => nameof(Boolean),
				13 => "Byte[]",
				//case 14: return nameof(byte[]); what is IMAGE
				//CUSTOM_OBJECT	15
				//IMAGELOOKUP	16
				//COLLECTION	17
				//COLOR	18
				//LOCALIZABLE_STRING	19
				//ENTITY	20
				//ENTITY_COLLECTION	21
				//ENTITY_COLUMN_MAPPING_COLLECTION	22
				23 => nameof(String),
				24 => nameof(String),
				//case 25: return nameof(String); FILE	25
				//MAPPING	26
				27 => nameof(String),
				28 => nameof(String),
				29 => nameof(String),
				30 => nameof(String),
				31 => nameof(Decimal),
				32 => nameof(Decimal),
				33 => nameof(Decimal),
				34 => nameof(Decimal),
				//LOCALIZABLE_PARAMETER_VALUES_LIST	35
				//METADATA_TEXT	36
				//STAGE_INDICATOR	37
				//OBJECT_LIST	38
				//COMPOSITE_OBJECT_LIST	39
				40 => nameof(Decimal),
				//FILE_LOCATOR	41
				42 => nameof(String),
				_ => nameof(String),
			};
		}
	}

	public class Column {
		public string Name {
			get; set;
		}
		public int DataValueType {
			get; set;
		}
		public string ReferenceSchemaName {
			get; set;
		}
		public string Description {
			get; set;
		}
	}

	public class Schema {
		public Schema() {
			Columns = new Dictionary<string, Column>();
		}
		public string Name {
			get; set;
		}
		public string Description {
			get; set;
		}
		public Dictionary<string, Column> Columns {
			get; set;
		}
	}
}
