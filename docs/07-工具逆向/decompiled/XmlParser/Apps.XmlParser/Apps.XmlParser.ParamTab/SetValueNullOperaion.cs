using System;

namespace Apps.XmlParser.ParamTab;

public class SetValueNullOperaion : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetValueNullOperaion(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		string text = "";
		if (ExtraParams.Length != 0)
		{
			text = ExtraParams[0];
		}
		if (Executor != null && Executor.TargetObj != null)
		{
			try
			{
				Executor.TargetObj.SetPropertyValueByName(text + "A", null);
			}
			catch (Exception ex)
			{
				throw new Exception(ex.Message);
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
