using System;
using System.IO;
using Bpmonline.Client;
using CommandLine;

namespace bpmcli.Command
{
	[Verb("execute-assembly-code", Aliases = new string[] { "exec" }, HelpText = "Execute an assembly code which implements the IExecutor interface")]
	internal class ExecuteAssemblyOptions: EnvironmentOptions
	{
		[Value(0, MetaName = "Name", Required = true, HelpText = "Path to executed assembly")]
		public string Name { get; set; }

		[Option('t', "ExecutorType", Required = true, HelpText = "Assembly type name for proceed")]
		public string ExecutorType { get; set; }
	}

	internal class ExecuteAssemblyCommand: BpmonlineCommand<ExecuteAssemblyOptions> {

		public ExecuteAssemblyCommand(): base() {
			_commandUrl = @"IDE/ExecuteScript";
		}

		public override void Execute(ExecuteAssemblyOptions options) {
			string filePath = options.Name;
			string executorType = options.ExecutorType;
			var fileContent = File.ReadAllBytes(filePath);
			string body = Convert.ToBase64String(fileContent);
			string requestData = @"{""Body"":""" + body + @""",""LibraryType"":""" + executorType + @"""}";
			var responseFromServer = BpmonlineClient.ExecutePostRequest(_commandUrl, requestData);
			Console.WriteLine(responseFromServer);
		}
	}
}
