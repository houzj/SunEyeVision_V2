using System;

namespace Apps.XmlParser.ParamTab;

public class SetFloatValueZeroOperaion : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetFloatValueZeroOperaion(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		float result = 0f;
		if (ExtraParams.Length != 0)
		{
			float.TryParse(ExtraParams[0], out result);
		}
		if (Executor == null || Executor.TargetObj == null)
		{
			return;
		}
		try
		{
			if (Executor.TargetObj is Float obj)
			{
				obj.CurValueA = result;
			}
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
