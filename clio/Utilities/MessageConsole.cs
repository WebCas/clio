using System;

namespace Clio.Utilities {
	public interface IMessageConsole {
		void WriteFailure(string text);
		void WriteFailure(string text, ConsoleColor color);
		void WriteSuccess(string text);
		void WriteSuccess(string text, ConsoleColor color);
	}

	public class MessageConsole : IMessageConsole {


		public void WriteSuccess(string text) {
			WriteSuccess(text, ConsoleColor.Green);
		}

		public void WriteSuccess(string text, ConsoleColor color) {
			var currentColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine(text);
			Console.ForegroundColor = currentColor;
		}

		public void WriteFailure(string text) {
			WriteFailure(text, ConsoleColor.Red);
			Console.WriteLine(text);
		}

		public void WriteFailure(string text, ConsoleColor color) {
			var currentColor = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(text);
			Console.ForegroundColor = currentColor;
		}
	}
}
