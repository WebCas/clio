using Clio.Command;
using Clio.Common;
using Clio.UserEnvironment;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Clio.Requests {
	public class ValidationBehaviour<TRequest, TResponse> :
		IPipelineBehavior<TRequest, TResponse> where TRequest : ExternalLinkOptions, IRequest<TResponse> {
		private readonly IValidator<TRequest> _validator;

		public ValidationBehaviour(IValidator<TRequest> validator) {
			_validator = validator;
		}


		public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) {
			ValidationResult validationResult = _validator.Validate(request);

			if (!validationResult.IsValid)
			{
				throw new ValidationException(validationResult.Errors);
			}
			return next();
		}
	}

	//CommandValidationBehaviour

	public class CommandValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		where TRequest : AllCommandsRequest, IRequest<TResponse> {

		private readonly IApplicationClientFactory _applicationClientFactory;
		private readonly ISettingsRepository _settingsRepository;

		public CommandValidationBehaviour(IApplicationClientFactory applicationClientFactory, ISettingsRepository settingsRepository) {
			_applicationClientFactory = applicationClientFactory;
			_settingsRepository = settingsRepository;
		}

		public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) {


			//string optionName = request.GetType().Name;
			//var allTypes = typeof(Program).Assembly.GetTypes();
			//var command = allTypes.Where(t =>
			//{
			//	return t.BaseType != null && t.BaseType?.GenericTypeArguments.Where(t => t.Name == optionName).Count() != 0 && t.BaseType.Name.StartsWith("RemoteCommand");
			//}).ToList();

			//bool isRemoteCommand = command.Count > 0;

			//if (isRemoteCommand)
			//{
			//	var options = request as EnvironmentOptions;
			//	var env = _settingsRepository.GetEnvironment(options.Environment);

			//	try
			//	{
			//		var creatioClient = _applicationClientFactory.CreateClient(env);
			//		creatioClient.Login();
			//	}
			//	catch (System.Exception ex)
			//	{
			//		System.Console.WriteLine("COULD NOT LOG-IN");
			//		throw;
			//	}
			//	return next();

			//}

			return next();

		}
	}


}
