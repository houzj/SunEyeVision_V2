using System;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class ShapeNumOperation : IOperation
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

	public ShapeNumOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		GlobalRenderVariable.ShapeRenderingMaxNumber = Convert.ToInt32(parentNode.Value);
	}

	public void ReOperate()
	{
	}
}
