using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class GeneralParamListHiddenOperation : IOperation
{
	public string TargetName = "";

	public bool OriginalValue { get; set; }

	public bool Working { get; private set; } = false;

	public Setter Executor { get; set; }

	public GeneralParamListHiddenOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Executor?.TargetObj?.GetVmModule() != null && Params.Length != 0)
		{
			PropertyManager.DisplayExecute("ImageSourceType", Params[0].ToString());
		}
		Working = true;
	}

	public void ReOperate()
	{
		if (Executor?.TargetObj?.GetVmModule() != null)
		{
		}
		Working = false;
	}

	public void GetOriginalValue()
	{
		OriginalValue = false;
	}
}
