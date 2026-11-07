namespace Apps.XmlParser.ParamTab;

public class FUpdateImageConfigOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj;

	private bool _Working;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public FUpdateImageConfigOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode != null && !string.IsNullOrEmpty(TargetName) && parentNode.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			TargetObj = algorithmTabRoot.FindBaseNodeByName(TargetName);
			if (TargetObj != null)
			{
				(TargetObj as ImageConfig).ChangeVisibleF();
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
