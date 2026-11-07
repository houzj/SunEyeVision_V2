namespace Apps.XmlParser.ParamTab;

public class GetSelectedLogicTypeOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj;

	private bool _Working;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetSelectedLogicTypeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams != null)
		{
			GetTargetObj();
			Executor.TargetObj = TargetObj as NameValueItem;
			if (Executor != null && Executor.TargetObj != null)
			{
				int curValueA = (Executor.TargetObj as EnumerationLogicType).CurValueA;
				(Executor.TargetObj as EnumerationLogicType).SetSelectedLogicType(curValueA);
				_Working = true;
			}
		}
	}

	private void GetTargetObj()
	{
		if (!string.IsNullOrEmpty(TargetName) && Executor.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			TargetObj = algorithmTabRoot.FindBaseNodeByName(TargetName);
		}
	}

	public void ReOperate()
	{
		Executor.TargetObj.CustomVisibleA = OriginalValue;
		_Working = false;
	}

	public void GetOriginalValue()
	{
		OriginalValue = Executor.TargetObj.CustomVisibleA;
	}
}
