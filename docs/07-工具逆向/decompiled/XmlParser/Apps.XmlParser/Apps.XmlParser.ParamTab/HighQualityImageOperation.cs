using System;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class HighQualityImageOperation : IOperation
{
	public string TargetName = "";

	public Setter Executor { get; set; }

	public bool Working
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public HighQualityImageOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		GlobalRenderVariable.IsHighQualityImage = Convert.ToBoolean(parentNode.Value);
	}

	public void ReOperate()
	{
	}
}
