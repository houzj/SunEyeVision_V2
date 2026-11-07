using System;
using System.Linq;
using Apps.Localization;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GetDisplayObjectOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetDisplayObjectOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (ExtraParams.Length != 0)
		{
			string objectName = ExtraParams[0];
			if (parentNode != null && parentNode.GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
			{
				IVmModule node = algorithmTabRoot.Owner as IVmModule;
				object displayObject = GetDisplayObject(node, objectName);
				try
				{
					parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", displayObject);
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

	public object GetDisplayObject(IVmModule node, string objectName)
	{
		if (node == null)
		{
			return null;
		}
		if (VmSolution.Instance[(node as IVmFullPath).FullPath + ".[Display]"] is ParamRoot paramRoot)
		{
			return paramRoot.GetDisplayList().OfType<Object>().Single((Object p) => p.NameA == objectName);
		}
		return null;
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
