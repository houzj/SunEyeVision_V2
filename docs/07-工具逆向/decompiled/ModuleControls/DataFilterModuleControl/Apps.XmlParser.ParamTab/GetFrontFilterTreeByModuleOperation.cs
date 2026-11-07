using System;
using System.Collections;
using System.Collections.Generic;
using Apps.Interface;
using Apps.Log;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontFilterTreeByModuleOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontFilterTreeByModuleOperation(string TName)
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
		if (leafIO == null)
		{
			return;
		}
		bool isNeedNull = true;
		VmModule vmModule2 = vmModule.GetType().GetProperty("SelectedModule")?.GetValue(vmModule) as VmModule;
		IList frontFilterTreeByOuputIO = leafIO.GetFrontFilterTreeByOuputIO((VmModule p) => true, "Output", IsIncludeSelf: false, IsIncludeInside: false, isNeedNull);
		List<Holder> list = new List<Holder>();
		if (vmModule2 != null)
		{
			foreach (Holder item in frontFilterTreeByOuputIO)
			{
				if (item.HolderTarget == vmModule2)
				{
					list.Add(item);
				}
			}
		}
		try
		{
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", list);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
