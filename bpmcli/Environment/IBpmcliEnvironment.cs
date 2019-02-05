namespace bpmcli.environment
{

	internal interface IBpmcliEnvironment
	{
		string GetRegisteredPath();
		IResult UserRegisterPath(string path);
		IResult MachineRegisterPath(string path);

	}
}
