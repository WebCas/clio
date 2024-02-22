namespace Clio.Common
{
	using System.Collections.Specialized;

	public interface IProcessExecutor
	{
		string Execute(string program, string command, bool waitForExit, string workingDirectory = null, bool showOutput = false, StringDictionary environmentVariables = null);

	}
}