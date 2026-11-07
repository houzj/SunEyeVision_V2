using System;

namespace Apps.XmlParser.ParamTab;

public class SetBooleanValueOperaion : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetBooleanValueOperaion(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		bool result = false;
		if (ExtraParams.Length != 0)
		{
			bool.TryParse(ExtraParams[0], out result);
		}
		if (Executor == null || Executor.TargetObj == null)
		{
			return;
		}
		try
		{
			if (Executor.TargetObj is Boolean boolean)
			{
				boolean.CurValueA = result;
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
