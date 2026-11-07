namespace Apps.XmlParser.ParamTab;

public class DisEnableOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public DisEnableOperation(string TName)
	{
		TargetName = TName;
	}

	public void ReOperate()
	{
		Executor.TargetObj.EnableA = OriginalValue;
		_Working = false;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Executor != null && Executor.TargetObj != null)
		{
			Executor.TargetObj.EnableA = false;
			_Working = true;
		}
	}

	public void GetOriginalValue()
	{
		OriginalValue = true;
	}
}
