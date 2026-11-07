using System;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ModelModifyOperation : IOperation
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

	public ModelModifyOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		VmSolution.IsModelModifyExecute = Convert.ToBoolean(parentNode.Value);
	}

	public void ReOperate()
	{
	}
}
