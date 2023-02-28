using FluentValidation;
using FluentValidation.Results;
using MediatR;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clio.Command {

	public abstract class Command<TEnvironmentOptions> : IRequestHandler<TEnvironmentOptions, int>
		where TEnvironmentOptions : IRequest<int> {

		protected IValidator<TEnvironmentOptions> _validator;
		protected ValidationResult _validationResult;

		public abstract int Execute(TEnvironmentOptions options);

		public virtual Task<int> Handle(TEnvironmentOptions request, CancellationToken cancellationToken) {

			if (_validator is object)
			{
				_validationResult = _validator.Validate(request);
				if (!_validationResult.IsValid)
				{
					PrintErrorMessages();
					return Task.FromResult(1);
				}
			}
			return Task.FromResult(Execute(request));
		}

		public virtual void PrintErrorMessages() {

			var defColor = Console.ForegroundColor;

			var header = _validationResult.Errors.Count == 1 ? "INPUT ERROR" : "INPUT ERRORS";
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(header);
			Console.WriteLine();

			_validationResult.Errors.Select(e => new { e.ErrorMessage, e.ErrorCode, e.Severity })
				.ToList().ForEach(e =>
				{
					Console.WriteLine($"\t{e.Severity.ToString().ToUpper(CultureInfo.InvariantCulture)} ({e.ErrorCode}) - {e.ErrorMessage}");
				});
			Console.ForegroundColor = defColor;
		}

	}
}