using System;

namespace Apps.XmlParser.ParamTab;

public class GetObjectParentOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetObjectParentOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null)
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		int num = 0;
		if (parentNode is ButtonSelecter)
		{
			num = (parentNode as ButtonSelecter).Type;
		}
		string text = "";
		if (ExtraParams.Length != 0)
		{
			text = ExtraParams[0];
		}
		if (parentNode == null)
		{
			return;
		}
		Object parentNode2 = parentNode.GetParentNode<Object>();
		if (parentNode2 == null)
		{
			return;
		}
		try
		{
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", parentNode2);
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
