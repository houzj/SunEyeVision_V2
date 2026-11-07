using System;
using Apps.Interface;
using Apps.Localization;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetSelectedFilterProcedureCommOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetSelectedFilterProcedureCommOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null)
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		int num = 0;
		if (parentNode is ButtonSelecter)
		{
			num = (parentNode as ButtonSelecter).Type;
		}
		string text = "";
		if (ExtraParams.Length != 0)
		{
			text = ExtraParams[0];
		}
		if (parentNode == null)
		{
			return;
		}
		BaseNode baseNode = parentNode.GetTopBaseNode() as BaseNode;
		if ((baseNode as IOwner)?.Owner is VmModule vmModule)
		{
			VmIO vmIO = null;
			vmIO = vmModule.Inputs.GetLeafIO(text);
			if (vmIO == null && baseNode is SubAlgorithmTabRoot subAlgorithmTabRoot)
			{
				vmIO = vmModule.Inputs.GetLeafIO(AppLocalizationService.UnDoTranslate(text + "_" + subAlgorithmTabRoot.HashCode));
			}
			if (vmIO == null && vmModule is VmModuleSet vmModuleSet)
			{
				vmModule = vmModuleSet.ProcedureCommunicaitonModule as VmModule;
				vmIO = vmModule.Inputs.GetLeafIO(text);
			}
			if (vmIO == null)
			{
				vmIO = vmModule.Outputs.GetLeafIO(text);
			}
			GetOutputSelectedFilter(vmIO, parentNode, TargetName);
		}
	}

	public static void GetOutputSelectedFilter(VmIO thisFilter, NameValueItem parentItem, string TargetName)
	{
		if (thisFilter == null || parentItem == null)
		{
			return;
		}
		string value = null;
		VmIO relateIO = thisFilter.RelateIO;
		if (relateIO != null)
		{
			value = ((!(relateIO is VmConstantIO)) ? thisFilter.SubscriptionInfo : relateIO.Name);
		}
		try
		{
			parentItem.SetPropertyValueByNameNotrigger(TargetName + "A", value);
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
