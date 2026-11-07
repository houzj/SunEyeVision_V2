using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VMControls.Interface;

namespace DynamicObjectConfigControl.UI.Data;

public class ExpendHolder : Holder
{
	public List<string> filterTypes;

	public string searchWord;

	private bool _IsExpanded;

	private bool isLoaded;

	public IList RunParamList { get; set; }

	public bool IsExpanded
	{
		get
		{
			return _IsExpanded;
		}
		set
		{
			if (_IsExpanded != value)
			{
				_IsExpanded = value;
				OnPropertyChanged("IsExpanded");
				if (!isLoaded & value)
				{
					OnPropertyChanged("EnumEntrysA");
				}
			}
		}
	}

	public override ObservableCollection<object> EnumEntrysA
	{
		get
		{
			if (!isLoaded)
			{
				isLoaded = true;
				List<NameValueItem> list = null;
				if (HolderTarget is VmModuleSet vmModuleSet)
				{
					list = new List<NameValueItem>();
					IEnumerable<NameValueItem> enumerable = (vmModuleSet.Params.GetParamRoot().FindBaseNodeByName(VmModuleExtern.GroupParamsName) as IChildren).Children.OfType<NameValueItem>();
					foreach (NameValueItem item2 in enumerable)
					{
						if (filterTypes.Contains(item2.GetPropertyValueByName("CloneSource").GetType().Name) && item2.MultiLanguageDisplayNameA.Contains(searchWord))
						{
							list.Add(item2);
						}
					}
				}
				else
				{
					AlgorithmTabRoot paramRoot = (HolderTarget as VmModule).Params.GetParamRoot();
					paramRoot.GetItemsValueUnit();
					list = paramRoot.GetAllItems().Where((NameValueItem p) =>
					{
						NameValueItem nameValueItem;
						return p is IGroupRunParam groupRunParam && (nameValueItem = p) != null && nameValueItem.CustomVisibleA && nameValueItem.GroupRunParamEnable && filterTypes.Contains(p.GetType().Name) && nameValueItem.MultiLanguageDisplayNameA.Contains(searchWord) && (nameValueItem.AccessMode?.ToLower() == "rw" || !string.IsNullOrEmpty(groupRunParam.DisplayForGroupType));
					}).ToList();
				}
				foreach (NameValueItem item in list)
				{
					Holder holder = new Holder
					{
						DisplayNameA = item.DisplayNameA,
						HolderTarget = item
					};
					holder.ParentTree = this;
					if (RunParamList != null && RunParamList.Count > 0)
					{
						Tuple<string, string> tuple = RunParamList.OfType<Tuple<string, string>>().FirstOrDefault((Tuple<string, string> p) => p.Item1 == (item.ParamsSource as VmModule).Name && p.Item2.Contains(AppLocalizationService.GetLang(item.DisplayNameA)));
						if (tuple != null)
						{
							holder.CheckEnabled = true;
						}
					}
					EnumEntrys.Add(holder);
				}
				EnumEntrys.RemoveAt(0);
			}
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
			}
		}
	}

	public void SetDefaultChild()
	{
		EnumEntrys.Add(new Holder());
	}
}
