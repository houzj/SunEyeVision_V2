namespace Apps.XmlParser.ParamTab;

public class DoInitOperaion : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public DoInitOperaion(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Executor != null && Executor.TargetObj != null)
		{
			Executor.TargetObj.InniterBusiness();
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
