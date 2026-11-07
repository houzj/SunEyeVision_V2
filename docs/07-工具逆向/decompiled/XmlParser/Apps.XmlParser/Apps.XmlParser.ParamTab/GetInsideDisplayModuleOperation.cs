namespace Apps.XmlParser.ParamTab;

public class GetInsideDisplayModuleOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetInsideDisplayModuleOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
