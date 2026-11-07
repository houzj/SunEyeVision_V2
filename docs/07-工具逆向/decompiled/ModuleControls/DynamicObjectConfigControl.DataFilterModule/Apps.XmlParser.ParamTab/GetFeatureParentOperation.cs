using System;
using System.Linq;

namespace Apps.XmlParser.ParamTab;

public class GetFeatureParentOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFeatureParentOperation(string TName)
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
		string name = "";
		if (ExtraParams.Length != 0)
		{
			name = ExtraParams[0];
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
		FeatureBase value = parentNode2.FeaturesB.FirstOrDefault((FeatureBase p) => p.NameA == name);
		try
		{
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", value);
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
