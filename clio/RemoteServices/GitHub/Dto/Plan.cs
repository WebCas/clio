namespace Clio.RemoteServices.GitHub.Dto;

public record Plan(
	string name,
	int space,
	int collaborators,
	int private_repos
);