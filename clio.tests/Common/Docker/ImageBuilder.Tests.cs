using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Clio.Command;
using Clio.Common;
using Clio.Common.Docker;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Internal.Commands;
using SharpCompress.Common;
using SharpCompress.Writers;
using StackExchange.Redis;
using Ms = System.IO.Abstractions;
using Version = System.Version;

namespace Clio.Tests.Common.Docker;

[Author("Kirill Krylov", "k.krylov@creatio.com")]
[Category("UnitTests")]
[TestFixture]
public class ImageBuilderTests
{
	private readonly IFileSystem _mockFileSystem = Substitute.For<IFileSystem>();
	private readonly IDockerClient _dockerClientMock = Substitute.For<IDockerClient>();
	private readonly ILogger _loggerMock = Substitute.For<ILogger>();
	
	[TestCase("8.1.0.6704_SalesEnterprise_Marketing_ServiceEnterpriseNet6_Softkey_PostgreSQL_ENU","se_m_se", "8.1.0.6704")]
	[TestCase("8.1.1.3480_Studio_Softkey_PostgreSQL_ENU","studio", "8.1.1.3480")]
	[TestCase("8.1.1.3592_BankSales_BankCustomerJourney_Lending_MarketingNet6_Softkey_PostgreSQL_ENU","bs_bcj_l_m", "8.1.1.3592")]
	[TestCase("aaa.aaa.aaa_BankSales_BankCustomerJourney_Lending_MarketingNet6_Softkey_PostgreSQL_ENU","bs_bcj_l_m", "0.0.0.0")]
	public void Test(string dirName, string expectedName, Version expectedVersion){

		//Arrange
		MockFileSystem mfs = new ();
		var dirInfo = mfs.Directory.CreateDirectory(dirName);

		//Act
		var actual = ImageBuilder.BuildTagFromDirName(dirInfo);
		
		//Assert
		actual.Name.Should().Be(expectedName);
		actual.Ver.Should().Be(expectedVersion);
	}

	
	[Test]
	public async Task GetImages(){
		//Arrange
		var sut = new ImageBuilder(_dockerClientMock, _mockFileSystem, _loggerMock);
		
		ImagesListResponse ilr1  = new ImagesListResponse {
			RepoTags = new List<string> {"se_m_se:9.9.9.9"}
		}; 
		ImagesListResponse ilr2 = new ImagesListResponse {
			RepoTags = new List<string> {"se_m_se:8.8.8.8"}
		}; 
		List<ImagesListResponse> responseMock = new List<ImagesListResponse>() {
			ilr1, ilr2
		};
		
		_dockerClientMock.Images
			.ListImagesAsync(Arg.Is<ImagesListParameters>(i => 
				i.Filters["reference"].ContainsKey("se_m_se:*")))
			.Returns(responseMock);
		
		//Act
		var actual = await sut.CheckDockerImageExistsAsync(
			new ImageTag(null,"se_m_se"));
		
		//Assert
		await _dockerClientMock.Images.Received(1)
			.ListImagesAsync(Arg.Is<ImagesListParameters>(i => 
				i.Filters["reference"].ContainsKey("se_m_se:*")));
		
		actual.Should().BeTrue();
	}
	
	[Test]
	public async Task CreateImage(){

		//Arrange
		var sut = new ImageBuilder(_dockerClientMock, _mockFileSystem, _loggerMock);
		var tag = new ImageTag(new Version("9.9.9.9"),"kirill");
		var writerMock = Substitute.For<IWriter>();
		sut.WriterFactoryWrapper = stream => writerMock;
		
		//Act
		await sut.CreateNewDockerImageAsync(tag, "");
		
		//Assert
		await _dockerClientMock.Images.Received(1)
			.BuildImageFromDockerfileAsync(
				Arg.Is<ImageBuildParameters>(i=> i.Tags == new []{$"{tag.Name}:{tag.Ver.ToString()}"}),
				Arg.Any<Stream>(),
				Arg.Any<IEnumerable<AuthConfig>>(),
				Arg.Any<IDictionary<string, string>>(),
				Arg.Any<IProgress<JSONMessage>>(), 
				Arg.Any<CancellationToken>()
				);
	}
	
	
	[Test]
	public async Task CreateContainer(){
		//Arrange
		DockerClient dockerClient = new DockerClientConfiguration().CreateClient();
		var sut = new ImageBuilder(dockerClient, _mockFileSystem, _loggerMock);
		
		var fs = new Ms.FileSystem();
		var contentFolder = fs.DirectoryInfo.New(@"D:\Projects\CreatioProductBuild\Docker_dockerone");
		
		//Act
		ImageTag it = new (new Version("8.1.2.629"),"se_m_se");
		var cp = new ContainerParams(it, "MyContainerName", 4001, 4002, contentFolder, FsContent.FULL);
		var actual = await sut.CreateContainer(cp);
		
		//Assert
		
		actual.ID.Should().NotBeNull();
		await sut.StartContainer(actual.ID);
		
	}
	
	
}

