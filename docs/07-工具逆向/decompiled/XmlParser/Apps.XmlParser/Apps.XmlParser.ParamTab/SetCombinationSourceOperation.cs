using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class SetCombinationSourceOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetCombinationSourceOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem nameValueItem = Params[0] as NameValueItem;
		string text = ExtraParams[0];
		int num = 0;
		if (nameValueItem is ButtonSelecter buttonSelecter)
		{
			buttonSelecter.ReadOnlyA = false;
			num = buttonSelecter.Type;
		}
		if (nameValueItem == null)
		{
			return;
		}
		object topBaseNode = nameValueItem.GetTopBaseNode();
		IOwner owner = null;
		if (topBaseNode is AlgorithmTabRoot)
		{
			owner = nameValueItem.GetTopBaseNode() as AlgorithmTabRoot;
		}
		else if (topBaseNode is ParamRoot)
		{
			owner = topBaseNode as ParamRoot;
		}
		if (owner == null || owner.Owner == null)
		{
			return;
		}
		VmModule vmModule = owner.Owner as VmModule;
		string text2 = Params[1] as string;
		string bindingIndex = null;
		int num2 = -1;
		VmIO iO = vmModule.GetIO(text);
		if (iO == null && topBaseNode is SubAlgorithmTabRoot subAlgorithmTabRoot)
		{
			iO = vmModule.GetIO(AppLocalizationService.UnDoTranslate(text + "_" + subAlgorithmTabRoot.HashCode));
		}
		if (!string.IsNullOrEmpty(text2))
		{
			int num3 = text2.IndexOf('[', 0);
			if (num3 > 0)
			{
				bindingIndex = text2.Substring(num3);
				bindingIndex = bindingIndex.Replace("[", "").Replace("]", "");
				text2 = text2.Remove(text2.IndexOf('[', 0));
			}
			VmIO ioByPathFromAround = vmModule.GetIoByPathFromAround(text2);
			if (ioByPathFromAround == null)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("订阅失败"), MessageIcons.Warning);
				return;
			}
			if (iO != null)
			{
				iO.RelateIO = ioByPathFromAround;
				iO.RelateIndex = SetFilterSourceOperation.GetBindIndex(vmModule, bindingIndex);
			}
		}
		if (string.IsNullOrEmpty(text2) && iO != null)
		{
			iO.RelateIO = null;
		}
		if (iO == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("模块输入无法找到"), MessageIcons.Warning);
		}
	}

	private void ClearDicByCombination(Dictionary<Filter, IndexFilter> dic, Combination inputCombination)
	{
		if (inputCombination.Filters == null)
		{
			return;
		}
		for (int i = 0; i < inputCombination.Filters.Count; i++)
		{
			if (inputCombination.Filters[i] is Filter)
			{
				if (dic.Keys.Contains(inputCombination.Filters[i]))
				{
					dic[inputCombination.Filters[i] as Filter] = null;
				}
			}
			else
			{
				ClearDicByCombination(dic, inputCombination.Filters[i] as Combination);
			}
		}
	}

	private void SetDicByCombination(Dictionary<Filter, IndexFilter> dic, Combination inputCombination, Combination outputCombination, string bindingIndex = null)
	{
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
