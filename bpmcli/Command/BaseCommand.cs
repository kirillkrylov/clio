using Bpmonline.Client;

namespace bpmcli.Command
{

	abstract class BpmonlineCommand<T>
	{
		protected BpmonlineClient BpmonlineClient { get; private set; }

		protected string _commandUrl;

	}
}