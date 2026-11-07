namespace Apps.XmlParser.ParamTab;

public class ReadyChangeOperation : IOperation
{
	private bool _Working = false;

	public Setter Executor { get; set; }

	public bool Working => _Working;

	public void GetOriginalValue()
	{
	}

	public ReadyChangeOperation(string TName)
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		Executor.GetParentNode<NameValueItem>()?.GetVmModule()?.OnPropertyChanged("IsReady");
	}

	public void ReOperate()
	{
	}
}
