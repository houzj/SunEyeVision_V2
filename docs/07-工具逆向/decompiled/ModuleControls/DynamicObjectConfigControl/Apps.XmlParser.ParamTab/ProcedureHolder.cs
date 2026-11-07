using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ProcedureHolder : Holder
{
	internal IGlobalData GlobalData;

	public List<string> filterTypes;

	public string searchWord;

	private bool _IsExpanded;

	private bool isLoaded;

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
				if (HolderTarget is VmProcedure vmProcedure)
				{
					List<GroupHolder> list = new List<GroupHolder>();
					List<string> groupNames = GroupRunParam.GetGroupNames(vmProcedure, "ParamGroupNamesList");
					List<int> groupIDs = GroupRunParam.GetGroupIDs(vmProcedure, "ParamGroupIDsList");
					if (groupNames.Count <= groupIDs.Count)
					{
						for (int i = 0; i < groupNames.Count; i++)
						{
							GroupHolder groupHolder = new GroupHolder
							{
								DisplayNameA = groupNames[i],
								ID = groupIDs[i],
								HolderTarget = vmProcedure
							};
							groupHolder.ParentTree = this;
							list.Add(groupHolder);
						}
						ObservableCollection<DynamicNameValueItemOfGroup> procedureParams = GlobalData.GetProcedureParams(vmProcedure);
						if (procedureParams != null)
						{
							foreach (DynamicNameValueItemOfGroup item in procedureParams)
							{
								int groupID = item.GetGroupID();
								GroupHolder groupHolder2 = list.FirstOrDefault((GroupHolder p) => p.ID == groupID);
								if (groupHolder2 == null || !filterTypes.Contains(item.CloneSource.GetType().Name) || !(item.CloneSource.DisplayNameA?.Contains(searchWord)).Value)
								{
									continue;
								}
								Holder holder = new Holder
								{
									DisplayNameA = item.NameA,
									HolderTarget = item
								};
								holder.ParentTree = groupHolder2;
								if (GlobalData.ParamList.Count > 0)
								{
									string text = GlobalData.ParamList.FirstOrDefault((string p) => ParamConfigDataBase.CheckParam(holder.HolderTarget, p));
									if (text != null)
									{
										holder.CheckEnabled = true;
									}
								}
								groupHolder2.EnumEntrysA.Add(holder);
							}
						}
					}
					foreach (GroupHolder item2 in list)
					{
						if (item2.EnumEntrysA.Count > 0)
						{
							EnumEntrys.Add(item2);
						}
					}
				}
				else
				{
					AlgorithmTabRoot paramRoot = (HolderTarget as VmModule).Params.GetParamRoot();
					paramRoot.GetItemsValueUnit();
					List<NameValueItem> list2 = paramRoot.GetAllItems().Where((NameValueItem p) =>
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
					foreach (NameValueItem item3 in list2)
					{
						Holder holder2 = new Holder
						{
							DisplayNameA = item3.DisplayNameA,
							HolderTarget = item3
						};
						holder2.ParentTree = this;
						if (GlobalData.ParamList.Count > 0)
						{
							string text2 = GlobalData.ParamList.FirstOrDefault((string p) => ParamConfigDataBase.CheckParam(holder2.HolderTarget, p));
							if (text2 != null)
							{
								holder2.CheckEnabled = true;
							}
						}
						EnumEntrys.Add(holder2);
					}
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
