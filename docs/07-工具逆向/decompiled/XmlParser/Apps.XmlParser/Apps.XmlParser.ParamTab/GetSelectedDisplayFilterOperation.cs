using System;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using Hik.Xml;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GetSelectedDisplayFilterOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetSelectedDisplayFilterOperation(string TName)
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
			if (vmIO == null)
			{
				vmIO = vmModule.Outputs.GetLeafIO(text);
			}
			if (vmIO == null)
			{
				vmIO = vmModule.Outputs.GetLeafIO("%" + text + "%");
			}
			if (vmIO == null)
			{
				GetDisplaySelectedFilter(vmModule, parentNode, TargetName);
			}
			else
			{
				GetOutputSelectedFilter(vmIO, parentNode, TargetName);
			}
		}
	}

	public static void GetDisplaySelectedFilter(VmModule node, NameValueItem parentItem, string TargetName)
	{
		try
		{
			Feature feature = ((VmSolution.Instance[((IVmFullPath)node).FullPath + ".[Display]"] is ParamRoot paramRoot) ? (from a in paramRoot.GetDisplayList().OfType<Object>()
				where a.Name == "Show Text"
				select a).FirstOrDefault() : null)?.Features.Where((FeatureBase a) => a.Name == parentItem.Name).FirstOrDefault() as Feature;
			parentItem.SetPropertyValueByNameNotrigger(TargetName + "A", (feature.Mapping == "NULL") ? feature.Value : node.GetIO(feature.Mapping)?.ModulePathName);
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	public static void GetOutputSelectedFilter(VmIO thisFilter, NameValueItem parentItem, string TargetName)
	{
		if (parentItem == null || thisFilter == null)
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
