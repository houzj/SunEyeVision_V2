namespace Apps.XmlParser.ParamTab;

public class SetFilterForcePropertyOperation : IOperation
{
	private string frontFilterName;

	private Filter frontFilter;

	public string TargetName = "";

	private bool _Working = false;

	public Filter FrontFilter => frontFilter;

	public bool Working => _Working;

	public bool OriginalValue { get; set; }

	public Setter Executor { get; set; }

	public SetFilterForcePropertyOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams != null && ExtraParams.Length >= 1)
		{
			frontFilterName = ExtraParams[0];
			if (FrontFilter != null)
			{
				FrontFilter.IsForceA = Executor.TargetObj.CustomVisibleA;
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
