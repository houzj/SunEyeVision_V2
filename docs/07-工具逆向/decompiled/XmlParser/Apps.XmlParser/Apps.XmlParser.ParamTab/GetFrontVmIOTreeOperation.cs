using System;
using System.Collections;
using Apps.Interface;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontVmIOTreeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontVmIOTreeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || string.IsNullOrEmpty(TargetName))
		{
			return;
		}
		object topBaseNode = parentNode.GetTopBaseNode();
		IOwner owner = null;
		if (topBaseNode is AlgorithmTabRoot)
		{
			owner = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
		}
		if (owner == null || owner.Owner == null)
		{
			return;
		}
		VmModule vmModule = owner.Owner as VmModule;
		VmIO leafIO = vmModule.Inputs.GetLeafIO(ExtraParams[0]);
		if (leafIO == null)
		{
			leafIO = vmModule.Outputs.GetLeafIO(ExtraParams[0]);
		}
		try
		{
			bool isNeedNull = true;
			IList frontFilterTree = leafIO.GetFrontFilterTree((VmModule p) => true, "Output", IsIncludeSelf: false, IsIncludeInside: false, isNeedNull);
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
