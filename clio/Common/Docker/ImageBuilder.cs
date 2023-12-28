using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Clio.Command;
using Docker.DotNet;
using Docker.DotNet.Models;
using SharpCompress.Common;
using SharpCompress.Writers;
using Version = System.Version;

namespace Clio.Common.Docker;

public class ImageBuilder
{

	#region Fields: Private

	private static readonly Func<ReadOnlyMemory<string>, Version> GetVersionFromDirName = memory => {
		if (memory.Length > 0) {
			Version.TryParse(memory.Span[0], out Version ver);
			return ver ?? new Version("0.0.0.0");
		}
		return new Version("0.0.0.0");
	};

	private static readonly Func<ReadOnlyMemory<string>, string> GetNameFromDirName = memory => {
		IEnumerable<string> values = memory.Span[1..].ToArray()
			.Select(word => {
				string sWord = word.Replace("Net6", "");
				ImageMap.TryGetValue(sWord, out string value);
				return value;
			}).Where(v => !string.IsNullOrEmpty(v));
		string tagName = string.Join('_', values);
		return tagName;
	};

	private static readonly Func<string, ReadOnlyMemory<string>> GetMemFromDirName
		= dirName => dirName.Split('_').AsMemory();

	private static readonly Func<string, bool> FileSearchFunc = fileName => Path.GetExtension(fileName) != ".backup";
	private readonly IDockerClient _dockerClient;
	private readonly IFileSystem _fileSystem;
	private readonly ILogger _logger;

	#endregion

	#region Constructors: Public

	public ImageBuilder(IDockerClient dockerClient, IFileSystem fileSystem, ILogger logger){
		_dockerClient = dockerClient;
		_fileSystem = fileSystem;
		_logger = logger;
	}

	#endregion

	#region Properties: Private

	private static ReadOnlyDictionary<string, string> ImageMap =>
		new(new Dictionary<string, string> {
			{"SalesEnterprise", "se"},
			{"ServiceEnterprise", "se"},
			{"Marketing", "m"},
			{"Studio", "studio"},
			{"BankSales", "bs"},
			{"BankCustomerJourney", "bcj"},
			{"Lending", "l"}
		});

	#endregion

	#region Properties: Public

	public Func<Stream, IWriter> WriterFactoryWrapper { get; set; } = stream =>
		WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None);

	#endregion

	#region Methods: Private

	private static IDictionary<string, IList<PortBinding>> GetPortBindings(int httpPort, int httpsPort){
		IDictionary<string, IList<PortBinding>> portBindings = new Dictionary<string, IList<PortBinding>>();
		portBindings.Add("5000/tcp", new List<PortBinding> {new() {HostPort = $"{httpPort}"}});
		portBindings.Add("5002/tcp", new List<PortBinding> {new() {HostPort = $"{httpsPort}"}});
		return portBindings;
	}

	private async Task<CreateContainerResponse> CreateContainerFull(ContainerParams containerParams){
		IDictionary<string, IList<PortBinding>> portBindings
			= GetPortBindings(containerParams.HttpPort, containerParams.HttpsPort);
		CreateContainerParameters containerParameters = new() {
			Image = containerParams.ImageTag.ToString(),
			HostConfig = new HostConfig {
				Binds = new List<string> {
					$"{containerParams.ContentFolder.FullName}/:/app/"
				},
				PortBindings = portBindings,
				RestartPolicy = new RestartPolicy {
					Name = RestartPolicyKind.UnlessStopped
				}
			},
			Name = containerParams.ContainerName
		};
		return await _dockerClient.Containers.CreateContainerAsync(containerParameters);
	}

	private async Task<CreateContainerResponse> CreateContainerMinimal(ContainerParams containerParams){
		IDictionary<string, IList<PortBinding>> portBindings
			= GetPortBindings(containerParams.HttpPort, containerParams.HttpsPort);
		CreateContainerParameters containerParameters = new() {
			Image = containerParams.ImageTag.ToString(),
			HostConfig = new HostConfig {
				Binds = new List<string> {
					$"{containerParams.ContentFolder.FullName}/ConnectionStrings.config:/app/ConnectionStrings.config",
					$"{containerParams.ContentFolder.FullName}/Terrasoft.WebHost.dll.config:/app/Terrasoft.WebHost.dll.config"
				},
				PortBindings = portBindings,
				RestartPolicy = new RestartPolicy {
					Name = RestartPolicyKind.UnlessStopped
				}
			},
			Name = containerParams.ContainerName
		};
		return await _dockerClient.Containers.CreateContainerAsync(containerParameters);
	}

	private void OnProgressOnProgressChanged(object sender, JSONMessage args){
		_logger.WriteLine(args.Stream);
	}

	#endregion

	#region Methods: Public

	[Pure]
	public static ImageTag BuildTagFromDirName(IDirectoryInfo dirInfo) =>
		new(GetVersionFromDirName(GetMemFromDirName(dirInfo.Name)),
			GetNameFromDirName(GetMemFromDirName(dirInfo.Name)));

	public async Task<bool> CheckDockerImageExistsAsync(ImageTag tag){
		ImagesListParameters parameters = new() {
			Filters = new Dictionary<string, IDictionary<string, bool>>()
		};
		parameters.Filters.Add("reference", new Dictionary<string, bool> {
			{$"{tag.Name}:{tag.Ver?.ToString() ?? "*"}", true}
		});
		IList<ImagesListResponse> images = await _dockerClient.Images.ListImagesAsync(parameters);
		return images.Any();
	}

	public async Task<CreateContainerResponse> CreateContainer(ContainerParams containerParams) =>
		containerParams.FsContent switch {
			FsContent.FULL => await CreateContainerFull(containerParams),
			FsContent.MINIMAL => await CreateContainerMinimal(containerParams),
			var _ => throw new ArgumentOutOfRangeException(nameof(containerParams.FsContent), containerParams.FsContent,
				"Unknown FsParam")
		};

	public async Task CreateNewDockerImageAsync(ImageTag tag, string directoryPath){
		string imageTag = $"{tag.Name}:{tag.Ver?.ToString() ?? string.Empty}";
		ImageBuildParameters createParam = new() {
			Tags = new[] {imageTag}
		};
		await using Stream stream = new MemoryStream();
		using IWriter writer = WriterFactoryWrapper(stream);
		//using IWriter writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None);
		writer.WriteAll(directoryPath, "*", FileSearchFunc, SearchOption.AllDirectories);
		stream.Seek(0, SeekOrigin.Begin);
		Progress<JSONMessage> progress = new();
		progress.ProgressChanged += OnProgressOnProgressChanged;
		await _dockerClient.Images.BuildImageFromDockerfileAsync(createParam, stream, null,
			null, progress);
	}

	public async Task<bool> StartContainer(string containerId){
		return await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
	}

	#endregion

}

public record ImageTag(Version Ver, string Name)
{

	#region Methods: Public

	public override string ToString() => $"{Name}:{Ver}";

	#endregion

}

public record ContainerParams(ImageTag ImageTag, string ContainerName, int HttpPort, int HttpsPort,
	IDirectoryInfo ContentFolder, FsContent FsContent);