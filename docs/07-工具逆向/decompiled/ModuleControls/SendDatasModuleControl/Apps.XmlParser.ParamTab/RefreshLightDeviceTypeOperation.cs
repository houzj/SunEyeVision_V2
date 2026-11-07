namespace Apps.XmlParser.ParamTab;

public class RefreshLightDeviceTypeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public Setter Executor { get; set; }

	public bool Working => _Working;

	public void GetOriginalValue()
	{
	}

	public RefreshLightDeviceTypeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		EnumerationLightForSendData parentNode = Executor.GetParentNode<EnumerationLightForSendData>();
		Enumeration enumeration = ((parentNode.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot) ? algorithmTabRoot.FindBaseNodeByName(TargetName) : null) as Enumeration;
		if (parentNode != null && enumeration != null && parentNode.CurValueA != 0)
		{
			string[] array = parentNode.Value?.Split('#');
			if (array != null && array.Length == 4 && int.TryParse(array[2], out var result))
			{
				enumeration.CurValueA = result;
			}
		}
	}

	public void ReOperate()
	{
	}
}
