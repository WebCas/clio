using System;
using System.Collections.Generic;
using Clio.RemoteServices;
using Clio.RemoteServices.GitHub.Dto;
using CommandLine;
using OneOf;
using Error = OneOf.Types.Error;

namespace Clio.Command;

[Verb("test-bench", Aliases = new[] {"test", "tb"}, HelpText = "Test bench command")]
public class TestBenchCommandOptions : EnvironmentOptions
{

}

public class TestBenchCommand : Command<TestBenchCommandOptions>
{

	#region Fields: Private

	private readonly IGitHubService _gitHubService;

	#endregion

	#region Constructors: Public

	public TestBenchCommand(IGitHubService gitHubService) {
		_gitHubService = gitHubService;
	}

	#endregion

	#region Properties: Private

	private static Func<OneOf<List<Repository>, Error>, string, bool> CheckRepositoryNameDoesNotExist =>
		(repos, name) =>
			repos.Value is List<Repository> repoList && !repoList.Exists(r => r.name == name);

	#endregion

	#region Methods: Public

	public override int Execute(TestBenchCommandOptions options) {
		OneOf<User, Error> result = _gitHubService.GetAuthenticatedUserAsync().Result;
		User user = result.Value is User u ? u : throw new Exception("Error");
		OneOf<List<Repository>, Error> repos = _gitHubService.ListUserRepositoriesAsync(user.login).Result;

		const string newRepoName = "clio-repo-from-console";
		if (CheckRepositoryNameDoesNotExist(repos, newRepoName)) {
			OneOf<Repository, Error> newRopository = _gitHubService
				.CreateUserRepositoryAsync(newRepoName, "Repository from clio").Result;
			
			return 0;
		}
		return 1;
	}

	#endregion

}