using System;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class SaveConfigOperation : IOperation
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

	public SaveConfigOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		string key = parentNode.ComName;
		if (ExtraParams != null && ExtraParams.Length != 0)
		{
			key = ExtraParams[0];
		}
		ConfigurationHelper.SaveConfig(key, parentNode.Value.ToString());
	}

	public void ReOperate()
	{
	}
}
