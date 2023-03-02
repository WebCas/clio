using Clio.Logger;
using FluentValidation;
using MediatR;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clio.Requests.Behaviours
{

	/// <summary>
	/// Validates command options when available
	/// </summary>
	/// <typeparam name="TRequest">Command TEnvironmentOptionr</typeparam>
	/// <typeparam name="TResponse">Int</typeparam>
	public class CommandValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AllCommandsRequest
	{
		private readonly ILogger<CommandValidationBehaviour<TRequest, TResponse>> _logger;
		private readonly IValidator<TRequest> _validator;

		public CommandValidationBehaviour(ILogger<CommandValidationBehaviour<TRequest, TResponse>> logger, IValidator<TRequest> validator = null)
		{
			_logger = logger;
			_validator = validator;
		}

		public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
		{
			if (_validator is null)
			{
				string message = $"{Environment.NewLine}VALIDATOR IS MISSING" +
				$"{Environment.NewLine}Consider creating validator of type AbstractValidator<{request.GetType().Name}>" +
				$"{Environment.NewLine}You can create public class {request.GetType().Name}Validator : EnvironmentOptionsValidator<{request.GetType().Name}>" +
				$"{Environment.NewLine}";
				_logger.LogWarningAsync(message);
				return next();
			}

			var _validationResult = _validator.Validate(request);
			if (_validationResult.IsValid)
			{
				return next();
			}

			_validationResult.Errors.Select(e => new { e.ErrorMessage, e.ErrorCode, e.Severity })
				.ToList().ForEach(e =>
				{
					_logger.LogError($"\t{e.Severity.ToString().ToUpper(CultureInfo.InvariantCulture)} ({e.ErrorCode}) - {e.ErrorMessage}");
				});

			dynamic result = 1;
			return Task.FromResult(result);
		}
	}
}
