namespace Apps.XmlParser.ParamTab;

public class HiddenOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	private bool isFirstFlag = true;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public HiddenOperation(string TName)
	{
		TargetName = TName;
	}

	public void ReOperate()
	{
		Executor.TargetObj.CustomVisibleA = OriginalValue;
		_Working = false;
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
			Executor.TargetObj.CustomVisibleA = false;
			_Working = true;
		}
	}

	public void GetOriginalValue()
	{
		SetOriginalValue(init: true);
	}

	private void SetOriginalValue(bool init = false)
	{
		if (Executor != null && Executor.TargetObj != null)
		{
			OriginalValue = init || Executor.TargetObj.CustomVisibleA;
		}
		else
		{
			OriginalValue = (init ? true : false);
		}
	}
}
