namespace Apps.XmlParser.ParamTab;

public class LightControlVisibleOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public LightControlVisibleOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Executor == null || Executor.TargetObj == null || ExtraParams.Length <= 1)
		{
			return;
		}
		int num = int.Parse(ExtraParams[0]);
		int num2 = int.Parse(ExtraParams[1]);
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || !(parentNode.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		int num3 = int.Parse(algorithmTabRoot.GetParamByName("LightDeviceParamGet"));
		int num4 = int.Parse(algorithmTabRoot.GetParamByName("InputType"));
		if (num4 == num)
		{
			switch (num3)
			{
			case 103:
				if (num2 == 1)
				{
					Executor.TargetObj.CustomVisibleA = true;
				}
				else
				{
					Executor.TargetObj.CustomVisibleA = false;
				}
				break;
			case 104:
				if (num2 == 1 || num2 == 2)
				{
					Executor.TargetObj.CustomVisibleA = true;
				}
				else
				{
					Executor.TargetObj.CustomVisibleA = false;
				}
				break;
			case 102:
				Executor.TargetObj.CustomVisibleA = true;
				break;
			case 114:
				Executor.TargetObj.CustomVisibleA = true;
				break;
			default:
				if (num2 == 1 || num2 == 2 || num2 == 3 || num2 == 4)
				{
					Executor.TargetObj.CustomVisibleA = true;
				}
				else
				{
					Executor.TargetObj.CustomVisibleA = false;
				}
				break;
			}
		}
		else
		{
			Executor.TargetObj.CustomVisibleA = false;
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
