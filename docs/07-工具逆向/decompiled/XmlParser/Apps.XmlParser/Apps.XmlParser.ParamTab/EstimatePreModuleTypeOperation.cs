using System.Collections;
using Apps.Interface;
using Apps.Localization;
using VM.Controls;
using VM.Core;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class EstimatePreModuleTypeOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj = null;

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public EstimatePreModuleTypeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		IOwner owner = null;
		if (parentNode != null)
		{
			owner = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
		}
		if (parentNode == null || string.IsNullOrEmpty(TargetName) || owner == null || owner.Owner == null)
		{
			return;
		}
		VmModule vmModule = owner.Owner as VmModule;
		IList frontModules = ((IVmModule)vmModule).FrontModules;
		foreach (VmModule item in frontModules)
		{
			if (item.StrModuleName == "ImageAcquisitionModule")
			{
				int num = (int)Params[1];
				string text = ((IUserStringData)item.Params)["Mode"];
				if (num == 1 && text == "2")
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MultiImageFusionModuleModeMatchError12"));
				}
				else if (num == 2 && text == "1")
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MultiImageFusionModuleModeMatchError21"));
				}
			}
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
