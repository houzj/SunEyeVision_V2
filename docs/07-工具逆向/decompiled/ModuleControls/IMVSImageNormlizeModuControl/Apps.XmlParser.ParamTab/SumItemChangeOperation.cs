using System;

namespace Apps.XmlParser.ParamTab;

public class SumItemChangeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SumItemChangeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
		{
			Float parentNode = Executor.GetParentNode<Float>();
			if (parentNode != null && parentNode.GetParentNode() is FloatSum floatSum)
			{
				Float theOtherFloat = floatSum.GetTheOtherFloat(parentNode);
				if (theOtherFloat != null && parentNode.CurValueA + theOtherFloat.CurValueA > floatSum.SumA)
				{
					object value = floatSum.SumA - parentNode.CurValueA;
					theOtherFloat.SetPropertyValueByNameNotrigger("CurValueA", value);
					theOtherFloat.ValueChangedExecute();
				}
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
