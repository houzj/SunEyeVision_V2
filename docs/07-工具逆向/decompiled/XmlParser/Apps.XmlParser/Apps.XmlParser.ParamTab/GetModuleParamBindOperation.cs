using Apps.Interface;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetModuleParamBindOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetModuleParamBindOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Params[0] is NameValueItem nameValueItem)
		{
			string paramName = "";
			if (ExtraParams.Length != 0)
			{
				paramName = ExtraParams[0];
			}
			if (nameValueItem != null && ((nameValueItem.GetTopBaseNode() is IOwner owner) ? owner.Owner : null) is VmModule vmModule)
			{
				string moduleParamBinding = vmModule.ParamSubManager.GetModuleParamBinding(paramName);
				nameValueItem.SetPropertyValueByNameNotrigger(TargetName + "A", moduleParamBinding);
			}
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
