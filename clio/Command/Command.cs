using Autofac;
using Clio.Logger;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clio.Command
{
	/// <summary>
	/// Abstraction over all commands
	/// </summary>
	/// <typeparam name="T">Any command option</typeparam>
	public abstract class Command<T> : IRequestHandler<T, int> where T : IRequest<int>
	{

		protected ILogger _logger;
		public abstract int Execute(T options);

		public virtual Task<int> Handle(T request, CancellationToken cancellationToken)
		{
			if (_logger is null)
			{
				//HACK: Wen can receive proper logger from constructor, this is a temp measure to POC
				//Changing constructor would require change in all commands.
				var generic = typeof(Logger<>);
				Type[] typeArgs = { GetType().UnderlyingSystemType };
				var constructed = generic.MakeGenericType(typeArgs);
				var ii = constructed.GetInterfaces().FirstOrDefault(t => t.IsGenericType);
				_logger = new BindingsModule().Register().Resolve(ii) as ILogger;
			}
			return PreflightCheck(request);
		}

		protected virtual Task<int> PreflightCheck(T request)
		{
			string message = $"{Environment.NewLine}PREFLIGHT CHECK IS MISSING" +
			$"{Environment.NewLine}Consider overwriting PreflightCheck method for {this.GetType().Name}";

			if (_logger is object)
			{
				_logger.LogWarningAsync(message);
			}
			return Task.FromResult(Execute(request));
		}
	}
}