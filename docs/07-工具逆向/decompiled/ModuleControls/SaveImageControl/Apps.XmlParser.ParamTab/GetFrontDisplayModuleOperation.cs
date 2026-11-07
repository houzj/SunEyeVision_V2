using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Hik.Xml;
using SaveImageCs;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFrontDisplayModuleOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFrontDisplayModuleOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null || !(parentNode.GetTopBaseNode() is BaseNode baseNode) || (baseNode as IOwner).Owner == null)
		{
			return;
		}
		VmModule vmModule = (baseNode as IOwner).Owner as VmModule;
		List<uint> relateModuleId = SaveImageHelper.GetRelateModuleId(vmModule);
		List<VmModule> frontParamItems = vmModule.GetFrontParamItems();
		List<Holder> list = new List<Holder>();
		foreach (VmModule module in frontParamItems)
		{
			if (module is VmProcedure || module.StrModuleName == "ImageSourceModule" || module.StrModuleName == "SaveImage" || module.StrModuleName == "ImageBufferModule" || module.StrModuleName == "ImageAcquisitionModule" || module == vmModule.Parent)
			{
				continue;
			}
			if (relateModuleId.Contains(module.ID))
			{
				List<VmIO> source = vmModule.Inputs.Where((VmIO p) => p.Name.StartsWith("%" + module.ID + "_")).ToList();
				if (source.All((VmIO p) => p.RelateIO != null))
				{
					continue;
				}
			}
			Holder item = new Holder
			{
				DisplayNameA = "%" + module.GetIdName() + "%",
				HolderTarget = module
			};
			list.Add(item);
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

	private void GetFrontModule(VmModule node, List<VmModule> list)
	{
		foreach (VmModule frontModule in node.FrontModules)
		{
			if (!list.Contains(frontModule))
			{
				list.Add(frontModule);
			}
			GetFrontModule(frontModule, list);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
