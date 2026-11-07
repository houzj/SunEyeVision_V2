namespace Apps.XmlParser.ParamTab;

public class SetBrightnessRangeOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj = null;

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetBrightnessRangeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Executor == null || Executor.TargetObj == null || ExtraParams.Length == 0)
		{
			return;
		}
		string text = ExtraParams[0];
		if (!(Executor.TargetObj is Integer integer))
		{
			return;
		}
		string text2 = text;
		if (!(text2 == "1"))
		{
			if (text2 == "2")
			{
				integer.MaxValueA = 255;
			}
		}
		else
		{
			integer.MaxValueA = 100;
		}
	}

	private void GetTargetObj()
	{
		if (!string.IsNullOrEmpty(TargetName))
		{
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			if (Executor.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				TargetObj = algorithmTabRoot.FindBaseNodeByName(TargetName);
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
