using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VMControls.Interface;

namespace DynamicObjectConfigControl.UI.Data;

public static class RunParamPickDataExtern
{
	public static IList GetInsideRunParamTree(this VmModule vmModule, List<string> filterTypes, string searchWord, IParamList paramList)
	{
		List<VmModule> list = (vmModule as IVmModuleSet).Children.OfType<VmModule>().ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddRunParamTreeNode(list2, filterTypes, searchWord, paramList);
		}
		return list2;
	}

	public static void AddRunParamTreeNode(this VmModule vmModule, List<Holder> holderList, List<string> filterTypes, string searchWord, IParamList paramList)
	{
		Holder runParamTreeNode = vmModule.GetRunParamTreeNode(filterTypes, searchWord, paramList);
		if (runParamTreeNode != null)
		{
			holderList.Add(runParamTreeNode);
		}
	}

	public static Holder GetRunParamTreeNode(this VmModule vmModule, List<string> filterTypes, string searchWord, IParamList paramList)
	{
		List<NameValueItem> list = null;
		if (vmModule is VmModuleSet vmModuleSet)
		{
			list = new List<NameValueItem>();
			IEnumerable<NameValueItem> enumerable = (vmModuleSet.Params.GetParamRoot().FindBaseNodeByName(VmModuleExtern.GroupParamsName) as IChildren).Children.OfType<NameValueItem>();
			foreach (NameValueItem item in enumerable)
			{
				if (filterTypes.Contains(item.GetPropertyValueByName("CloneSource")?.GetType().Name) && (item.MultiLanguageDisplayNameA?.Contains(searchWord)).Value)
				{
					list.Add(item);
				}
			}
		}
		else
		{
			list = vmModule.Params.GetParamRoot().GetAllItems().Where((NameValueItem p) =>
			{
				int result;
				if (p is IGroupRunParam groupRunParam && p != null)
				{
					if (p.CustomVisibleA && p.GroupRunParamEnable && filterTypes.Contains(p.GetType().Name) && (p.MultiLanguageDisplayNameA?.Contains(searchWord)).Value)
					{
						result = ((p.AccessMode?.ToLower() == "rw" || !string.IsNullOrEmpty(groupRunParam.DisplayForGroupType)) ? 1 : 0);
						goto IL_00a1;
					}
				}
				result = 0;
				goto IL_00a1;
				IL_00a1:
				return (byte)result != 0;
			})
				.ToList();
		}
		if (list != null && list.Count > 0)
		{
			return new ExpendHolder
			{
				DisplayNameA = "%" + vmModule.Name + "%",
				HolderTarget = vmModule,
				filterTypes = filterTypes,
				searchWord = searchWord,
				ParamList = paramList
			};
		}
		return null;
	}
}
