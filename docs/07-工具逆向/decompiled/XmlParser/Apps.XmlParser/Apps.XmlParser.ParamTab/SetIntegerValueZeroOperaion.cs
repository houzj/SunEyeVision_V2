using System;

namespace Apps.XmlParser.ParamTab;

public class SetIntegerValueZeroOperaion : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetIntegerValueZeroOperaion(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		int result = 0;
		if (ExtraParams.Length != 0)
		{
			int.TryParse(ExtraParams[0], out result);
		}
		if (Executor == null || Executor.TargetObj == null)
		{
			return;
		}
		try
		{
			if (Executor.TargetObj is Integer integer)
			{
				integer.CurValueA = result;
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
