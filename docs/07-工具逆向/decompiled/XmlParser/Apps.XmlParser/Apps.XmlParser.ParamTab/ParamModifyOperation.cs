using System;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ParamModifyOperation : IOperation
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

	public ParamModifyOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		VmSolution.IsParamModifyExecute = Convert.ToBoolean(parentNode.Value);
	}

	public void ReOperate()
	{
	}
}
