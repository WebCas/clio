using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Clio.Common;
using CommandLine;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Build.Construction;

namespace Clio.Command;

#region Class: CreatePropsFileOptions

[Verb("create-props", Aliases = new[] {"cp"}, HelpText = "Create .props file")]
public class CreatePropsFileOptions : EnvironmentOptions
{

	#region Properties: Public

	[Option("CsprojPath", Required = true, HelpText = "Path to csproj file")]
	public string CsprojPath { get; set; }

	[Option('d', "DestinationPath", Required = false, HelpText = "Destination path to  .prop fodler")]
	public string DestinationPath { get; set; }

	[Option("LibPath", Required = true, HelpText = "Path to Libs folder")]
	public string LibPath { get; set; }

	#endregion

}

#endregion

#region Class: CreateUiProjectOptionsValidator

public class CreatePropsFileOptionsValidator : AbstractValidator<CreatePropsFileOptions>
{

	#region Constructors: Public

	public CreatePropsFileOptionsValidator(IFileSystem fileSystem) {
		RuleFor(x => x.LibPath).NotEmpty().WithMessage("LibPath is required.");
		RuleFor(x => x.LibPath).Must(fileSystem.ExistsDirectory).WithMessage("LibPath does not exists");
		RuleFor(x => x.DestinationPath).NotEmpty().WithMessage("DestinationPath for .props file is required.");
	}

	#endregion

}

#endregion

#region Class: CreateUiProjectCommand

internal class CreatePropsFileCommand : Command<CreatePropsFileOptions>
{

	private enum TargetFra
	{

		net472,netstandard

	}
	
	#region Constants: Private

	private const string Net472 = "net472";
	private const string NetStandard = "netstandard2.0";

	#endregion

	#region Fields: Private

	private static readonly Func<CreatePropsFileOptions, IValidator<CreatePropsFileOptions>, ILogger,
			Func<CreatePropsFileOptions, int>, int>
		Validate = (options, optionsValidator, logger, next) => {
			ValidationResult validationResult = optionsValidator.Validate(options);
			return optionsValidator.Validate(options).IsValid ? next(options) : OnError(validationResult, logger);
		};

	private static readonly Func<ValidationResult, ILogger, int>
		OnError = (validationResult, logger) => {
			validationResult.Errors.ForEach(error => logger.WriteLine(error.ErrorMessage));
			return 1;
		};

	private static readonly Func<ProjectRootElement, IEnumerable<string>> GetCommonRefs = projectRootElement => {
		return projectRootElement.ItemGroups
			.Where(group => group.Items.Any(item => item.ItemType == "Reference"))
			.SelectMany(group => group.Items)
			.Select(item => item.Include);
	};

	private static readonly Func<ProjectRootElement, string, IEnumerable<string>> GetTargetRefs =
		(projectRootElement, targetFramework) => {
			return projectRootElement.ChooseElements
				.SelectMany(choose => choose.WhenElements.Where(when => when.Condition.Contains(targetFramework)))
				.SelectMany(when => when.ItemGroups
					.Where(group => group.Items.Any(item => item.ItemType == "Reference"))
					.SelectMany(group => group.Items)
					.Select(item => item.Include)
				);
		};

	private static readonly Func<IFileSystem, CreatePropsFileOptions, string, IEnumerable<string>> GetFiles =
		(fileSystem, options, targetFramework) => new List<string>(
			fileSystem.GetFilesInfos(Path.Join(options.LibPath, targetFramework), "*.dll",
					SearchOption.TopDirectoryOnly)
				.Select(fileSystem.GetFileNameWithoutExtension));

	private static readonly Func<List<string>, string> CreateNet472Content = dlls => {
		StringBuilder sb = new StringBuilder()
			.AppendLine("<!-- THIS FILE IS AUTO GENERATED USE CLIO CLI FOR HELP-->")
			.AppendLine("<Project>")
			.AppendJoin(null, dlls.SelectMany(d => CreateNet472Segment(d)))
			.AppendLine("</Project>");
		return sb.ToString();
	};

	private static readonly Func<List<string>, string> CreateNetStandardContent = dlls => {
		StringBuilder sb = new StringBuilder()
			.AppendLine("<!-- THIS FILE IS AUTO GENERATED USE CLIO CLI FOR HELP-->")
			.AppendLine("<Project>")
			.AppendJoin(null, dlls.SelectMany(d => CreateNetStandardSegment(d)))
			.AppendLine("</Project>");
		return sb.ToString();
	};

	private static readonly Func<string, string> CreateNetStandardSegment = dll => @$"	<Choose>
		<!--Used when dll already exists in Core-Lib folder-->
		<When Condition=""Exists('$(CoreLibPath)\{dll}.dll')"">
			<ItemGroup>
				<Reference Include=""{dll}"">
					<HintPath>$(CoreLibPath)\{dll}.dll</HintPath>
					<SpecificVersion>False</SpecificVersion>
					<Private>False</Private>
				</Reference>
			</ItemGroup>
		</When>
		
		<!--Used when building for netstandrad 2.0-->
		<When Condition=""Exists('$(RelativeCurrentPkgFolderPath)\Files\Libs\net472\{dll}.dll') and '$(TargetFramework)' == 'netstandard2.0'"">
			<ItemGroup>
				<Reference Include=""{dll}"">
					<HintPath>Libs\netstandard\{dll}.dll</HintPath>
					<SpecificVersion>False</SpecificVersion>
					<Private>False</Private>
				</Reference>
			</ItemGroup>
		</When>
	</Choose>
";

	private static readonly Func<string, string> CreateNet472Segment = dll => @$"	<Choose>
		<!--Used when dll already exists in Core-Lib folder-->
		<When Condition=""Exists('$(CoreLibPath)\{dll}.dll')"">
			<ItemGroup>
				<Reference Include=""{dll}"">
					<HintPath>$(CoreLibPath)\{dll}.dll</HintPath>
					<SpecificVersion>False</SpecificVersion>
					<Private>False</Private>
				</Reference>
			</ItemGroup>
		</When>

		<!--Used when building for NetFramework-->
		<When Condition=""Exists('$(RelativeCurrentPkgFolderPath)\Files\Libs\net472\{dll}.dll') and '$(TargetFramework)' == 'net472'"">
			<ItemGroup>
				<Reference Include=""{dll}"">
					<HintPath>Libs\net472\{dll}.dll</HintPath>
					<SpecificVersion>False</SpecificVersion>
					<Private>False</Private>
				</Reference>
			</ItemGroup>
		</When>
	</Choose>
";

	private readonly IValidator<CreatePropsFileOptions> _optionsValidator;
	private readonly ILogger _logger;
	private readonly IFileSystem _fileSystem;

	#endregion

	#region Constructors: Public

	public CreatePropsFileCommand(IValidator<CreatePropsFileOptions> optionsValidator, ILogger logger,
		IFileSystem fileSystem) {
		optionsValidator.CheckArgumentNull(nameof(optionsValidator));
		_optionsValidator = optionsValidator;
		_logger = logger;
		_fileSystem = fileSystem;
	}

	#endregion

	#region Methods: Private

	private int InternalExecute(CreatePropsFileOptions options) {
		IEnumerable<string> net472Dlls = GetFiles(_fileSystem, options, Net472);
		IEnumerable<string> netStandardDls = GetFiles(_fileSystem, options, NetStandard);

		ProjectRootElement root = ProjectRootElement.Open(options.CsprojPath);
		IEnumerable<string> commonRefs = GetCommonRefs(root);
		IEnumerable<string> net472Refs = GetTargetRefs(root, Net472);
		IEnumerable<string> netStandardRefs = GetTargetRefs(root, "netstandard2.0");

		List<string> dllsNet472 = new(net472Dlls.Where(dll => !commonRefs.Contains(dll) && !net472Refs.Contains(dll)));
		List<string> dllsNetStandard = new(netStandardDls.Where(dll => !commonRefs.Contains(dll) && !netStandardRefs.Contains(dll)));

		_fileSystem.CreateDirectoryIfNotExists(options.DestinationPath);
		_fileSystem.WriteAllTextToFile(Path.Join(options.DestinationPath, "net472.props"), CreateNet472Content(dllsNet472));
		_fileSystem.WriteAllTextToFile(Path.Join(options.DestinationPath, "netstandard.props"), CreateNetStandardContent(dllsNetStandard));

		return 0;
	}

	#endregion

	#region Methods: Public

	public override int Execute(CreatePropsFileOptions options) {
		return Validate(options, _optionsValidator, _logger, InternalExecute);
	}

	#endregion

}

#endregion