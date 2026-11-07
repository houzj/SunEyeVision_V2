using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VMControls.Interface;

namespace DynamicObjectConfigControl.UI.Data;

public static class RunParamPickDataExtern
{
	private static List<Tuple<string, string>> _runParamList = new List<Tuple<string, string>>();

	public static void SetConfigData(this VmModule vmModule, GroupRunParamConfigData configData)
	{
		_runParamList.Clear();
		if (configData == null)
		{
			return;
		}
		foreach (RunParamConfigDecorate item in configData.Collection)
		{
			if (!item.IsForGroup)
			{
				_runParamList.Add(new Tuple<string, string>(item.RelationModulesStr, item.Name));
			}
		}
	}

	public static IList GetInsideRunParamTree(this VmModule vmModule, List<string> filterTypes, string searchWord)
	{
		List<VmModule> list = (vmModule as IVmModuleSet).Children.OfType<VmModule>().ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddRunParamTreeNode(list2, filterTypes, searchWord);
		}
		return list2;
	}

	public static void AddRunParamTreeNode(this VmModule vmModule, List<Holder> holderList, List<string> filterTypes, string searchWord)
	{
		Holder runParamTreeNode = vmModule.GetRunParamTreeNode(filterTypes, searchWord);
		if (runParamTreeNode != null)
		{
			holderList.Add(runParamTreeNode);
		}
	}

	public static Holder GetRunParamTreeNode(this VmModule vmModule, List<string> filterTypes, string searchWord)
	{
		List<NameValueItem> list = null;
		if (vmModule is VmModuleSet vmModuleSet)
		{
			list = new List<NameValueItem>();
			IEnumerable<NameValueItem> enumerable = (vmModuleSet.Params.GetParamRoot().FindBaseNodeByName(VmModuleExtern.GroupParamsName) as IChildren).Children.OfType<NameValueItem>();
			foreach (NameValueItem item in enumerable)
			{
				if (filterTypes.Contains(item.GetPropertyValueByName("CloneSource").GetType().Name) && item.MultiLanguageDisplayNameA.Contains(searchWord))
				{
					list.Add(item);
				}
			}
		}
		else
		{
			list = vmModule.Params.GetParamRoot().GetAllItems().Where((NameValueItem p) =>
			{
				NameValueItem nameValueItem;
				return p is IGroupRunParam groupRunParam && (nameValueItem = p) != null && nameValueItem.CustomVisibleA && nameValueItem.GroupRunParamEnable && filterTypes.Contains(p.GetType().Name) && nameValueItem.MultiLanguageDisplayNameA.Contains(searchWord) && (nameValueItem.AccessMode?.ToLower() == "rw" || !string.IsNullOrEmpty(groupRunParam.DisplayForGroupType));
			})
				.ToList();
		}
		if (list != null && list.Count > 0)
		{
			ExpendHolder expendHolder = new ExpendHolder
			{
				DisplayNameA = "%" + vmModule.Name + "%",
				HolderTarget = vmModule,
				filterTypes = filterTypes,
				searchWord = searchWord,
				RunParamList = _runParamList
			};
			expendHolder.SetDefaultChild();
			return expendHolder;
		}
		return null;
	}
}
