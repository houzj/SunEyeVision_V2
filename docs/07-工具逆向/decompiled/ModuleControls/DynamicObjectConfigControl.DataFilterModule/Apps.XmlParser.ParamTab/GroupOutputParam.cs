using DynamicObjectConfigControl;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GroupOutputParam : GroupParam
{
	private const string _DefaultStartName = "out";

	public override string DisplayType => "GroupInputParam";

	public override VmIOCollection TargetVmIOs => Owner?.Outputs;

	public override string DefaultStartName => "out";

	public override string CurrentParamName => "DynamicOutputParam";
}
