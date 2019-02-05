using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using bpmcli.environment;

namespace bpmcli.tests.EnvironmentTests
{
	public class BpmcliEnvironmentTests
	{
		private string _oroginalUserPath;
		private string _oroginalMachinePath;

		private bool EnvPathExists(string path, EnvironmentVariableTarget target) {
			return Environment.GetEnvironmentVariable(BpmcliEnvironment.PathVariableName, target)
				.Contains(path);
		}

		private string GenerateTestPath() {
			return string.Concat("C://", Guid.NewGuid(), "/", Guid.NewGuid());
		}

		[SetUp]
		public void Setup() {
			_oroginalUserPath = Environment.GetEnvironmentVariable(BpmcliEnvironment.PathVariableName,
				EnvironmentVariableTarget.User);
			_oroginalMachinePath = Environment.GetEnvironmentVariable(BpmcliEnvironment.PathVariableName,
				EnvironmentVariableTarget.Machine);
		}

		[TearDown]
		public void TearDovn() {
			Environment.SetEnvironmentVariable(BpmcliEnvironment.PathVariableName, _oroginalUserPath,
				EnvironmentVariableTarget.User);
			Environment.SetEnvironmentVariable(BpmcliEnvironment.PathVariableName, _oroginalMachinePath,
				EnvironmentVariableTarget.Machine);
		}

		[Test, Category("Integration")]
		public void BpmcliEnvironment_UserRegisterPath_AddTestPath() {
			var testPath = GenerateTestPath();
			var env = new BpmcliEnvironment();
			env.UserRegisterPath(testPath);
			EnvPathExists(testPath, EnvironmentVariableTarget.User).Should().BeTrue();
		}

		[Test, Category("Integration")]
		public void BpmcliEnvironment_MachineRegisterPath_AddTestPath() {
			var testPath = GenerateTestPath();
			var env = new BpmcliEnvironment();
			env.MachineRegisterPath(testPath);
			EnvPathExists(testPath, EnvironmentVariableTarget.Machine).Should().BeTrue();
		}
	}
}
