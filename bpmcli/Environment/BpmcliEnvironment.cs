﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("bpmcli.tests")]

namespace bpmcli.environment
{
	
	internal class BpmcliEnvironment : IBpmcliEnvironment
	{
		public const string PathVariableName = "PATH";

		private IResult RegisterPath(string path, EnvironmentVariableTarget target) {
			var result = new EnvironmentResult();
			string pathValue = Environment.GetEnvironmentVariable(PathVariableName, target);
			if (string.IsNullOrEmpty(pathValue)) {
				result.AppendMessage($"{PathVariableName} variable is empty!");
				return result;
			}
			if (pathValue.Contains(path)) {
				result.AppendMessage($"{PathVariableName} variable already registered!");
				return result;
			}
			result.AppendMessage($"register path {path} in {PathVariableName} variable.");
			var value = string.Concat(pathValue, ";" + path.Trim(';'));
			Environment.SetEnvironmentVariable(PathVariableName, value, target);
			result.AppendMessage($"{PathVariableName} variable registered.");
			return result;
		}

		public string GetRegisteredPath() {
			string[] cliPath = (Environment.GetEnvironmentVariable(PathVariableName)?.Split(';'));
			return cliPath?.First(p => p.Contains("bpmcli"));
		}

		public IResult UserRegisterPath(string path) {
			return RegisterPath(path, EnvironmentVariableTarget.User);
		}

		public IResult MachineRegisterPath(string path) {
			return RegisterPath(path, EnvironmentVariableTarget.Machine);
		}

	}
}
