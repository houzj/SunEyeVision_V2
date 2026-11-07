using System;
using Apps.Localization;
using Hik.Util;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GetCategoryProcedureCommOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetCategoryProcedureCommOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (ExtraParams.Length != 0)
		{
			string text = ExtraParams[0];
			if (parentNode != null && parentNode.GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
			{
				VmModule vmModule = algorithmTabRoot.Owner as VmModule;
				object inOutputByName = algorithmTabRoot.GetInOutputByName(text);
				if (inOutputByName == null)
				{
					string typeName = GetType().Namespace + "." + ((vmModule as VmModuleSet).ProcedureCommunicaitonModule as IVmModule).ModuleTypeName + "DynamicIO";
					object obj = ActivatorWrap.CreateInstance(typeName, new object[2] { text, algorithmTabRoot });
					inOutputByName = algorithmTabRoot.GetInOutputByName(text, obj);
				}
				try
				{
					parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", inOutputByName);
					return;
				}
				catch (Exception ex)
				{
					throw new Exception(ex.Message);
				}
			}
			return;
		}
		throw new Exception(AppLocalizationService.GetLang(parentNode?.Name) + AppLocalizationService.GetLang("缺少参数") + " :" + AppLocalizationService.GetLang(Executor.OperationName));
	}

	public object GetCategory(VmModule node, string objectName)
	{
		return node?.GetInOutputByName(objectName);
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
