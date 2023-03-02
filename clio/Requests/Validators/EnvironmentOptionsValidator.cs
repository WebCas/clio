using FluentValidation;
using FluentValidation.Results;
using System;
using System.Linq;

namespace Clio.Requests.Validators
{
	public class EnvironmentOptionsValidator<T> : AbstractValidator<T> where T : EnvironmentOptions
	{
		public EnvironmentOptionsValidator()
		{
			RuleFor(r => r.Environment)
			.Cascade(CascadeMode.Stop)
			.Custom(Main);
		}

		/// <summary>
		/// Validates that either Environment name or Credentials are not empty
		/// Valid: clio command TTT
		/// Valid clio command -l Supervisor -p Supervisor -u https://
		/// Valid clio command -l ClientId -p Supervisor -u https://
		/// </summary>
		virtual public Action<string, ValidationContext<T>> Main => (value, context) =>
		{

			bool isLoginEmpty = string.IsNullOrWhiteSpace(context.InstanceToValidate.Login);
			bool isUriEmpty = string.IsNullOrWhiteSpace(context.InstanceToValidate.Uri);
			bool isPasswordEmpty = string.IsNullOrWhiteSpace(context.InstanceToValidate.Password);
			bool isClientIdEmpty = string.IsNullOrWhiteSpace(context.InstanceToValidate.ClientId);
			bool isClientSecretEmpty = string.IsNullOrWhiteSpace(context.InstanceToValidate.ClientSecret);

			if (!string.IsNullOrWhiteSpace(value))
			{
				if (!isLoginEmpty)
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO1",
						ErrorMessage = "Cannot provide Login when environment name is specified",
						Severity = Severity.Error
					});
				}

				if (!isUriEmpty)
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO2",
						ErrorMessage = "Cannot provide Uri when environment name is specified",
						Severity = Severity.Error
					});
				}

				if (!isPasswordEmpty)
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO1",
						ErrorMessage = "Cannot provide Password when environment name is specified",
						Severity = Severity.Error
					});
				}

				if (!isClientIdEmpty)
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO1",
						ErrorMessage = "Cannot provide ClientId when environment name is specified",
						Severity = Severity.Error
					});
				}

				if (!isClientSecretEmpty)
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO1",
						ErrorMessage = "Cannot provide ClientSecret when environment name is specified",
						Severity = Severity.Error
					});
				}
			}
			else
			{
				//No environment name, credentials must be provided
				int score = 0;
				score += (isLoginEmpty) ? 0 : 1;
				score += (isPasswordEmpty) ? 0 : 1;
				//score can only be 0, 1, 2

				score += (isClientIdEmpty) ? 0 : 2;
				score += (isClientSecretEmpty) ? 0 : 2;

				//score can be
				//0 (OK)- Default environment, nothing is provided
				//1 (INVALID) - OneOf(Username or Password)
				//2 (OK) - Both(Username and Password)
				//3 (INVALID) - OneOf (Username/Password) and OneOf(ClientId/ClientSecret)
				//4  (OK) - Both (ClientId and ClientSecret)
				//5 (INVALID) - Both (ClientId and ClientSecret) + OneOf(username or password)
				//6 (INVALID) - Both (ClientId and ClientSecret) + Both(Username and Password) provided
				bool isInvalidScore = (new[] { 1, 3, 5 }).Contains(score);

				if (isInvalidScore)
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO1",
						ErrorMessage = "Either Username/Password or ClientId/CLientSecret must be provided",
						Severity = Severity.Error
					});
				}

				if (isUriEmpty)
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO1",
						ErrorMessage = "Uri missing, Uri has to be provided when using credentials",
						Severity = Severity.Error
					});
				}

				if (!isUriEmpty && !Uri.TryCreate(context.InstanceToValidate.Uri, UriKind.Absolute, out _))
				{
					context.AddFailure(new ValidationFailure
					{
						ErrorCode = "ARGOO2",
						ErrorMessage = "Environment Url invalid format",
						Severity = Severity.Error
					});
				}
			}
		};

	}
}
