namespace Apps.XmlParser.ParamTab;

public class EnableOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public EnableOperation(string TName)
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
			Executor.TargetObj.EnableA = true;
			_Working = true;
		}
	}

	public void GetOriginalValue()
	{
		OriginalValue = false;
	}
}
