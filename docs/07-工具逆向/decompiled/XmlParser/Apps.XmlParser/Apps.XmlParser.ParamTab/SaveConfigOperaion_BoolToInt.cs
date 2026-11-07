using System;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class SaveConfigOperaion_BoolToInt : IOperation
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

	public SaveConfigOperaion_BoolToInt(string TName)
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
		bool flag = Convert.ToBoolean(parentNode.Value);
		ConfigurationHelper.SaveConfig(key, (flag ? 1 : 0).ToString());
	}

	public void ReOperate()
	{
	}
}
