using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VMControls.Interface;

namespace DynamicObjectConfigControl.UI.Data;

public class ExpendHolder : Holder
{
	public static List<string> CheckedParams;

	public List<string> filterTypes;

	public string searchWord;

	private bool _IsExpanded;

	private bool isLoaded;

	public IParamList ParamList { get; set; }

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
					AlgorithmTabRoot paramRoot = (HolderTarget as VmModule).Params.GetParamRoot();
					try
					{
						paramRoot.GetItemsValueUnit();
					}
					catch (Exception ex)
					{
						LogHelper.Error((HolderTarget as VmModule).Name + " GetItemsValueUnit error: " + ex.Message);
					}
					list = paramRoot.GetAllItems().Where((NameValueItem p) =>
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
					}).ToList();
				}
				foreach (NameValueItem item2 in list)
				{
					Holder holder = new Holder
					{
						DisplayNameA = item2.DisplayNameA,
						HolderTarget = item2
					};
					holder.ParentTree = this;
					if (ParamList != null && ParamList.ParamList.Count > 0)
					{
						foreach (string param in ParamList.ParamList)
						{
							bool flag = ((CheckedParams != null && CheckedParams.Contains(param)) ? true : false);
							if (!ParamConfigDataBase.CheckParam(holder.HolderTarget, param, flag))
							{
								continue;
							}
							if (!flag)
							{
								holder.CheckEnabled = true;
								if (CheckedParams != null)
								{
									CheckedParams.Add(param);
								}
							}
							break;
						}
					}
					EnumEntrys.Add(holder);
				}
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
}
