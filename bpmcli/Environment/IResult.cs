using System.IO;

namespace bpmcli.environment
{
	internal interface IResult
	{
		void ShowMessagesTo(TextWriter writer);
		void AppendMessage(string message);
	}
}
