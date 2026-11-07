using DynamicObjectConfigControl;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GroupInputParam : GroupParam
{
	private const string _DefaultStartName = "in";

	public override VmIOCollection TargetVmIOs => Owner?.Inputs;

	public override string DefaultStartName => "in";

	public override string CurrentParamName => "DynamicInputParam";
}
