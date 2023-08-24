using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Clio.RemoteServices.GitHub.Dto;
using OneOf;
using OneOf.Types;

namespace Clio.RemoteServices;

public interface IGitHubService
{

	#region Methods: Public

	/// <summary>
	/// Creates a new repository in the specified organization. The authenticated user must be a member of the organization.
	/// </summary>
	/// <param name="organizationName">GitHub organization name</param>
	/// <param name="repositoryName">GitHub repository name</param>
	/// <param name="repositoryDescription">GitHub repository description</param>
	/// <param name="isPrivate">Indicates if repository is private, default false</param>
	/// <returns><see cref="Repository"/> object</returns>
	/// <remarks>
	/// <a href="https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#create-an-organization-repository">Create a repository for the authenticated user</a>
	/// </remarks>
	Task<OneOf<Repository, Error>> CreateOrganizationRepositoryAsync(string organizationName, string repositoryName,
		string repositoryDescription, bool isPrivate = false);

	/// <summary>
	/// Creates a new repository for the authenticated user.
	/// </summary>
	/// <param name="repositoryName">GitHub repository name</param>
	/// <param name="repositoryDescription">GitHub repository description</param>
	/// <param name="isPrivate">Indicates if repository is private, default false</param>
	/// <returns><see cref="Repository"/> object</returns>
	/// <remarks>
	/// <a href="https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#create-a-repository-for-the-authenticated-user">Create a repository for the authenticated user</a>
	/// </remarks>
	Task<OneOf<Repository, Error>> CreateUserRepositoryAsync(string repositoryName, string repositoryDescription,
		bool isPrivate = false);

	/// <summary>
	/// Lists public and private profile information
	/// </summary>
	/// <returns><see cref="User"/></returns>
	/// <remarks>
	/// <a href="https://docs.github.com/en/rest/reference/users#get-the-authenticated-user">Get the authenticated user</a>
	/// </remarks>
	Task<OneOf<User, Error>> GetAuthenticatedUserAsync();

	/// <summary>
	/// Lists repositories for the specified organization.
	/// </summary>
	/// <param name="organizationName">username </param>
	/// <returns>List of <see cref="Repository"/></returns>
	/// <remarks>
	/// <a href="https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#list-organization-repositories">Lists repositories for the specified organization.</a>
	/// </remarks>
	Task<OneOf<List<Repository>, Error>> ListOrganizationRepositoriesAsync(string organizationName);

	/// <summary>
	/// Lists repositories that the authenticated user has explicit permission (:read, :write, or :admin) to access.
	/// </summary>
	/// <param name="userName">username </param>
	/// <returns>List of <see cref="Repository"/></returns>
	/// <remarks>
	/// <a href="https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#list-repositories-for-the-authenticated-user">List repositories for a user</a>
	/// </remarks>
	Task<OneOf<List<Repository>, Error>> ListUserRepositoriesAsync(string userName);

	#endregion

}

public class GitHubService : IGitHubService
{

	#region Fields: Private

	private SettingsRepository _settingsRepository;
	private readonly HttpClient _httpClient;

	#endregion

	#region Constructors: Public

	public GitHubService(IHttpClientFactory httpClientFactory) {
		_httpClient = httpClientFactory.CreateClient("GitHub");
	}

	#endregion

	#region Properties: Private

	private static Func<Uri, HttpMethod, HttpContent, HttpRequestMessage> CreateRequestMessage =>
		(uri, httpMethod, httpContent) => new HttpRequestMessage {
			Method = httpMethod,
			RequestUri = uri,
			Content = httpContent
		};

	private SettingsRepository SettingsRepository => _settingsRepository ??= new SettingsRepository();

	private string Token => SettingsRepository.GetGithubToken();

	#endregion

	#region Methods: Private

	private async Task<OneOf<Repository, Error>> CreateRepositoryAsync(string organizationName, string repositoryName,
		string repositoryDescription, bool isPrivate = false) {
		string url = string.IsNullOrWhiteSpace(organizationName) ? "user/repos" : $"orgs/{organizationName}/repos";
		var request = new {
			name = repositoryName,
			description = repositoryDescription,
			@private = isPrivate,
			is_template = false
		};
		HttpRequestMessage message = CreateRequestMessage(new Uri(url, UriKind.Relative), HttpMethod.Post,
			new StringContent(JsonSerializer.Serialize(request)));
		message.Headers.Authorization = new AuthenticationHeaderValue("token", Token);
		HttpResponseMessage response = await _httpClient.SendAsync(message);
		return response.StatusCode == HttpStatusCode.Created
			? await JsonSerializer.DeserializeAsync<Repository>(await response.Content.ReadAsStreamAsync())
			: new Error();
	}

	private async Task<OneOf<List<Repository>, Error>> ListRepositories(string name, bool isUser) {
		string url = isUser ? $"users/{name}/repos" : $"orgs/{name}/repos";
		HttpRequestMessage message = CreateRequestMessage(new Uri(url, UriKind.Relative), HttpMethod.Get,
			new StringContent(string.Empty));
		message.Headers.Authorization = new AuthenticationHeaderValue("token", Token);
		HttpResponseMessage response = await _httpClient.SendAsync(message);

		return response.StatusCode == HttpStatusCode.OK
			? await JsonSerializer.DeserializeAsync<List<Repository>>(await response.Content.ReadAsStreamAsync())
			: new Error();
	}

	#endregion

	#region Methods: Public

	public async Task<OneOf<Repository, Error>> CreateOrganizationRepositoryAsync
		(string organizationName, string repositoryName, string repositoryDescription, bool isPrivate = false) =>
		await CreateRepositoryAsync(organizationName, repositoryName, repositoryDescription, isPrivate);

	public async Task<OneOf<Repository, Error>> CreateUserRepositoryAsync
		(string repositoryName, string repositoryDescription, bool isPrivate = false) =>
		await CreateRepositoryAsync(string.Empty, repositoryName, repositoryDescription, isPrivate);

	public async Task<OneOf<User, Error>> GetAuthenticatedUserAsync() {
		HttpRequestMessage message = CreateRequestMessage(new Uri("user", UriKind.Relative), HttpMethod.Get,
			new StringContent(string.Empty));
		message.Headers.Authorization = new AuthenticationHeaderValue("token", Token);
		HttpResponseMessage response = await _httpClient.SendAsync(message);
		return response.StatusCode == HttpStatusCode.OK
			? await JsonSerializer.DeserializeAsync<User>(await response.Content.ReadAsStreamAsync())
			: new Error();
	}

	public async Task<OneOf<List<Repository>, Error>> ListOrganizationRepositoriesAsync
		(string organizationName) => await ListRepositories(organizationName, false);

	public async Task<OneOf<List<Repository>, Error>> ListUserRepositoriesAsync
		(string userName) => await ListRepositories(userName, true);

	#endregion

}