using System;
using System.Collections;
using System.Collections.Generic;
using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontCombinationTreeByModuleSortOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontCombinationTreeByModuleSortOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		string text = "";
		if (ExtraParams.Length != 0)
		{
			text = ExtraParams[0];
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || string.IsNullOrEmpty(text))
		{
			return;
		}
		VmModule vmModule = null;
		object topBaseNode = parentNode.GetTopBaseNode();
		if (topBaseNode is AlgorithmTabRoot)
		{
			AlgorithmTabRoot algorithmTabRoot = topBaseNode as AlgorithmTabRoot;
			if (algorithmTabRoot.Owner != null)
			{
				vmModule = algorithmTabRoot.Owner as VmModule;
			}
		}
		else if (topBaseNode is BaseNode)
		{
			BaseNode baseNode = topBaseNode as BaseNode;
			vmModule = (baseNode as IOwner).Owner as VmModule;
		}
		if (vmModule == null)
		{
			return;
		}
		VmModule vmModule2 = vmModule;
		IList frontCombinationTree = vmModule.GetFrontCombinationTree((VmModule p) => true, "Output", text);
		List<Holder> list = new List<Holder>();
		if (vmModule2 != null)
		{
			DataSortParam dataSortParam = ((vmModule2.Params.Data as AlgorithmTabRoot).Tabs[0] as Tab).Collection[0] as DataSortParam;
			foreach (Holder item in frontCombinationTree)
			{
				if (item.HolderTarget == dataSortParam.SelectedModule)
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
