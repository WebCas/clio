using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clio.Logger
{
	public interface ILogger
	{
		void LogInfo(string message);
		Task LogInfoAsync(string message);
		void LogError(string message);
		Task LogErrorAsync(string message);
		void LogWarning(string message);
		Task LogWarningAsync(string message);
	}

	public interface ILogger<out T> : ILogger
	{

	}

	internal class Logger<T> : ILogger<T>
	{
		private readonly IMediator _mediator;
		public Logger(IMediator mediator)
		{
			_mediator = mediator;
			Caller = typeof(T);
		}

		public Type Caller {
			get;
		}

		public void LogInfo(string message)
		{
			Task.Run(async () =>
			{
				await LogInfoAsync(message).ConfigureAwait(false);
			}).Wait();
		}

		public async Task LogInfoAsync(string message)
		{
			await _mediator.Publish(new LogMessage(message, MessageType.Info, Caller));
		}

		public void LogError(string message)
		{
			Task.Run(async () =>
			{
				await LogErrorAsync(message).ConfigureAwait(false);
			}).Wait();
		}

		public async Task LogErrorAsync(string message)
		{
			await _mediator.Publish(new LogMessage(message, MessageType.Error, Caller));
		}

		public void LogWarning(string message)
		{
			Task.Run(async () =>
			{
				await LogWarningAsync(message).ConfigureAwait(false);
			}).Wait();
		}

		public async Task LogWarningAsync(string message)
		{
			await _mediator.Publish(new LogMessage(message, MessageType.Warning, Caller));
		}
	}

	public class LogMessageHandler : INotificationHandler<LogMessage>
	{
		public Task Handle(LogMessage notification, CancellationToken cancellationToken)
		{
			string callerName = notification.Caller.IsGenericType ? notification.Caller.GenericTypeArguments.FirstOrDefault().Name : notification.Caller.Name;
			switch (notification.MessageType)
			{
				case MessageType.Error:
					Console.ForegroundColor = ConsoleColor.DarkRed;
					Console.Out.WriteLineAsync($"******** ERROR ({callerName}) ********{Environment.NewLine}{notification.Message}");
					Console.ResetColor();
					break;
				case MessageType.Info:
					Console.Out.WriteLineAsync($"{callerName}: {notification.Message}");
					break;
				case MessageType.Warning:
					Console.ForegroundColor = ConsoleColor.DarkYellow;
					Console.Out.WriteLineAsync($"******** WARNING ({callerName}) ********{notification.Message}");
					Console.ResetColor();
					break;
				default:
					break;
			}
			return Task.CompletedTask;
		}
	}

	public class LogMessage : INotification
	{

		public LogMessage(string messsage, MessageType messageType, Type caller)
		{
			Message = messsage;
			MessageType = messageType;
			Caller = caller;
		}

		public string Message {
			get; init;
		}

		public MessageType MessageType {
			get; init;
		}

		public Type Caller {
			get; init;
		}
	}

	public enum MessageType
	{
		Error,
		Info,
		Warning
	}

}
