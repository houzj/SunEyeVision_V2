using System;

namespace Apps.XmlParser.ParamTab;

public class SetInitValueByCurValue : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetInitValueByCurValue(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode != null && !string.IsNullOrEmpty(parentNode.Value) && Executor != null)
		{
			if (Executor.TargetObj is Boolean boolean)
			{
				boolean.CurValueA = true;
			}
			if (Executor.TargetObj is RadioSelecter { CurValueA: <=0 } radioSelecter)
			{
				radioSelecter.CurValueA = Convert.ToInt32(ExtraParams[0]);
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
