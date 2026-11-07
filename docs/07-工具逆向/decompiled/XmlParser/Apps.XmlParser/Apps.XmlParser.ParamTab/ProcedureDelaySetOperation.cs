using System;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ProcedureDelaySetOperation : IOperation
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

	public ProcedureDelaySetOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		try
		{
			VmSolution.SetProcedureDelayTime(uint.Parse(parentNode.Value));
		}
		catch (Exception)
		{
		}
	}

	public void ReOperate()
	{
	}
}
