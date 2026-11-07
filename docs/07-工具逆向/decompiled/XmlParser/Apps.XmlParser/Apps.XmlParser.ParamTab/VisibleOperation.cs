namespace Apps.XmlParser.ParamTab;

public class VisibleOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj = null;

	private bool _Working = false;

	private bool isFirstFlag = true;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public VisibleOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Executor != null && Executor.TargetObj != null)
		{
			if (isFirstFlag)
			{
				SetOriginalValue(init: true);
			}
			else
			{
				isFirstFlag = false;
				SetOriginalValue();
			}
			Executor.TargetObj.CustomVisibleA = true;
			_Working = true;
		}
	}

	private void GetTargetObj()
	{
		if (string.IsNullOrEmpty(TargetName))
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode != null)
		{
			object topBaseNode = parentNode.GetTopBaseNode();
			if (topBaseNode is AlgorithmTabRoot algorithmTabRoot)
			{
				TargetObj = algorithmTabRoot.FindBaseNodeByName(TargetName);
			}
		}
	}

	public void ReOperate()
	{
		Executor.TargetObj.CustomVisibleA = OriginalValue;
		_Working = false;
	}

	public void GetOriginalValue()
	{
		SetOriginalValue(init: true);
	}

	private void SetOriginalValue(bool init = false)
	{
		if (Executor != null && Executor.TargetObj != null)
		{
			OriginalValue = !init && Executor.TargetObj.CustomVisibleA;
		}
		else
		{
			OriginalValue = false;
		}
	}
}
