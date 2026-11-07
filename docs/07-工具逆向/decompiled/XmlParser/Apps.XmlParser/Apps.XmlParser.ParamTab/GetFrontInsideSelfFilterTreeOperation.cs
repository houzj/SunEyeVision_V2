using System;
using System.Collections;
using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontInsideSelfFilterTreeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontInsideSelfFilterTreeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null || ExtraParams.Length < 1)
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || !(parentNode.GetTopBaseNode() is BaseNode baseNode) || (baseNode as IOwner).Owner == null)
		{
			return;
		}
		VmModule vmModule = (baseNode as IOwner).Owner as VmModule;
		IList frontFilterTree = vmModule.GetFrontFilterTree((VmModule p) => true, "Output", ExtraParams, IsIncludeSelf: true, IsIncludeInside: true, IsNeedNull: false);
		try
		{
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", frontFilterTree);
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
