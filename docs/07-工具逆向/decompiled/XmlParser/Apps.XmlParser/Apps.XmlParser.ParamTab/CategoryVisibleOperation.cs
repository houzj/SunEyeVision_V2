namespace Apps.XmlParser.ParamTab;

public class CategoryVisibleOperation : IOperation
{
	public string TargetName = "";

	private Category TargetObj = null;

	private bool _Working = false;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public CategoryVisibleOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (TargetObj != null)
		{
			TargetObj.CustomVisibleA = true;
			_Working = true;
		}
	}

	private void GetTargetObj()
	{
		if (!string.IsNullOrEmpty(TargetName))
		{
			TargetObj = (Executor.GetTopBaseNode() as AlgorithmTabRoot).FindCategoryByName(TargetName);
		}
	}

	public void ReOperate()
	{
		TargetObj.CustomVisibleA = OriginalValue;
		_Working = false;
	}

	public void GetOriginalValue()
	{
		OriginalValue = false;
		GetTargetObj();
	}
}
