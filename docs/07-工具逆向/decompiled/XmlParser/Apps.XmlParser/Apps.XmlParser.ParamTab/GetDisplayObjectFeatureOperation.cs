using System;
using System.Linq;
using Apps.Localization;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GetDisplayObjectFeatureOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetDisplayObjectFeatureOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (ExtraParams.Length > 1)
		{
			string objectName = ExtraParams[0];
			string featureName = ExtraParams[1];
			if (parentNode != null && parentNode.GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
			{
				IVmModule node = algorithmTabRoot.Owner as IVmModule;
				object displayObjectFeature = GetDisplayObjectFeature(node, objectName, featureName);
				try
				{
					parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", displayObjectFeature);
					return;
				}
				catch (Exception ex)
				{
					throw new Exception(ex.Message);
				}
			}
			return;
		}
		throw new Exception(AppLocalizationService.GetLang(parentNode.Name) + AppLocalizationService.GetLang("缺少参数") + " :" + AppLocalizationService.GetLang(Executor.OperationName));
	}

	public static object GetDisplayObjectFeature(IVmModule node, string objectName, string featureName)
	{
		if (node == null)
		{
			return null;
		}
		if (!(VmSolution.Instance[(node as IVmFullPath).FullPath + ".[Display]"] is ParamRoot paramRoot))
		{
			return null;
		}
		Object obj = paramRoot.GetDisplayList().OfType<Object>().Single((Object p) => p.NameA == objectName);
		return obj.GetFeatureByName(featureName);
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
