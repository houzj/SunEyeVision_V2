using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Xml;
using Apps.BaseData;
using Apps.Localization;
using Apps.Log;
using GlobalCameraModuleControl;
using GlobalCameraModuleControl.XmlData;
using Hik.Xml;
using HikExternalCall.Common;
using HikPcUI.Commom;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class DynamicCameraParamList : NameValueItem, ICameraDynamicParam, IDynamicChild
{
	private ObservableCollection<DynamicCameraNameItemOfGroupCategory> groupCollection;

	private ObservableCollection<DynamicCameraNameItemOfGroup> collection;

	private ICommand _ConfigCommand = null;

	private VmModule owner;

	private object _RealTimeContent;

	private object _MVSContent;

	ObservableCollection<DynamicCameraNameItemOfGroupCategory> ICameraDynamicParam.GroupCollection => GroupCollection;

	ObservableCollection<DynamicCameraNameItemOfGroup> ICameraDynamicParam.Collections => Collection;

	VmModule ICameraDynamicParam.Owner => Owner;

	public ObservableCollection<DynamicCameraNameItemOfGroupCategory> GroupCollection
	{
		get
		{
			if (groupCollection == null)
			{
				groupCollection = new ObservableCollection<DynamicCameraNameItemOfGroupCategory>();
				ObservableCollection<DynamicCameraNameItemOfGroup> observableCollection = Collection;
			}
			return groupCollection;
		}
	}

	public ObservableCollection<DynamicCameraNameItemOfGroup> Collection
	{
		get
		{
			if (collection == null)
			{
				collection = new ObservableCollection<DynamicCameraNameItemOfGroup>();
				IEnumerable<DynamicCameraNameItemOfGroup> allNameItemValue = GetAllNameItemValue();
				foreach (DynamicCameraNameItemOfGroup item3 in allNameItemValue)
				{
					collection.Add(item3);
				}
				groupCollection = new ObservableCollection<DynamicCameraNameItemOfGroupCategory>();
				List<string> groupNames = GetGroupNames();
				for (int i = 0; i < groupNames.Count; i++)
				{
					DynamicCameraNameItemOfGroupCategory item = new DynamicCameraNameItemOfGroupCategory
					{
						Name = groupNames[i]
					};
					groupCollection.Add(item);
				}
				foreach (DynamicCameraNameItemOfGroup item2 in collection)
				{
					DynamicCameraNameItemOfGroupCategory dynamicCameraNameItemOfGroupCategory = groupCollection.FirstOrDefault((DynamicCameraNameItemOfGroupCategory x) => x.Name == item2.TreeGroupName);
					if (dynamicCameraNameItemOfGroupCategory != null)
					{
						item2.Group = dynamicCameraNameItemOfGroupCategory;
						if (item2.Source.VisibilityA == "True")
						{
							dynamicCameraNameItemOfGroupCategory.collection.Add(item2);
						}
					}
				}
			}
			return collection;
		}
	}

	public ICommand ConfigCommand => _ConfigCommand ?? (_ConfigCommand = (ICommand)new DelegateCommand<DialogHandler>((Action<DialogHandler>)ConfigCommandExecute));

	internal VmModule Owner
	{
		get
		{
			if (owner == null)
			{
				owner = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
			}
			return owner;
		}
	}

	public object RealTimeContent
	{
		get
		{
			if (_RealTimeContent == null && GetTopBaseNode() != null)
			{
				_RealTimeContent = (GetTopBaseNode() as AlgorithmTabRoot).FindBaseNodeByName("RealTimeImgAcqCmd");
			}
			return _RealTimeContent;
		}
	}

	public object MVSContent
	{
		get
		{
			if (_MVSContent == null && GetTopBaseNode() != null)
			{
				_MVSContent = (GetTopBaseNode() as AlgorithmTabRoot).FindBaseNodeByName("CamerParam");
			}
			return _MVSContent;
		}
	}

	internal void SetNull()
	{
		collection = null;
	}

	private void ConfigCommandExecute(DialogHandler method)
	{
		DynamicCameraPanelList dynamicCameraPanelList = new DynamicCameraPanelList();
		dynamicCameraPanelList.Owner = Owner;
		foreach (DynamicCameraNameItemOfGroup item in Collection)
		{
			item.Parent = dynamicCameraPanelList;
			dynamicCameraPanelList.Collection.Add(item);
		}
		foreach (DynamicCameraNameItemOfGroupCategory item2 in GroupCollection)
		{
			item2.Parent = dynamicCameraPanelList;
			dynamicCameraPanelList.GroupCollection.Add(item2);
		}
		method.Invoke((object)dynamicCameraPanelList);
		Collection.Clear();
		GroupCollection.Clear();
		string text = string.Empty;
		foreach (DynamicCameraNameItemOfGroupCategory item3 in dynamicCameraPanelList.GroupCollection)
		{
			item3.Parent = null;
			text = text + item3.Name + ";";
			GroupCollection.Add(item3);
		}
		if (text != null && text.Contains(";"))
		{
			text = text.TrimEnd(';');
			Owner.UserData["groupNames"] = StringUtil.UTF8GetBytes(text);
		}
		else
		{
			Owner.UserData["groupNames"] = null;
		}
		foreach (DynamicCameraNameItemOfGroup item4 in dynamicCameraPanelList.Collection)
		{
			item4.Parent = null;
			Collection.Add(item4);
		}
		OnPropertyChanged("Collection");
		OnPropertyChanged("GroupCollection");
		GetParamUnit();
	}

	internal List<string> GetGroupNames()
	{
		List<string> result = new List<string>();
		byte[] array = Owner.UserData["groupNames"];
		if (array != null)
		{
			string text = StringUtil.UTF8GetString(array);
			result = text.Split(';').ToList();
		}
		return result;
	}

	private List<DynamicCameraNameItemOfGroup> GetAllNameItemValueRe()
	{
		List<DynamicCameraNameItemOfGroup> list = new List<DynamicCameraNameItemOfGroup>();
		List<NameValueItem> list2 = new List<NameValueItem>();
		IntegerBx integerBx = new IntegerBx();
		integerBx.MaxValueA = 100;
		integerBx.MinValueA = 1;
		integerBx.IncValueA = 5;
		integerBx.Value = "20";
		integerBx.VisibilityA = "True";
		integerBx.AccessModeA = "w";
		integerBx.NameA = "CESHI";
		integerBx.DisplayNameA = "测试1";
		list2.Add(integerBx);
		IntegerBx integerBx2 = new IntegerBx();
		integerBx2.MaxValueA = 100;
		integerBx2.MinValueA = 1;
		integerBx2.IncValueA = 5;
		integerBx2.Value = "20";
		integerBx2.VisibilityA = "Flase";
		integerBx2.AccessModeA = "w";
		integerBx2.NameA = "CESHI1";
		integerBx2.DisplayNameA = "测试2";
		list2.Add(integerBx2);
		FloatBx floatBx = new FloatBx();
		floatBx.MaxValueA = 200.0;
		floatBx.MinValueA = 1.0;
		floatBx.IncValueA = 5.0;
		floatBx.Value = "20";
		floatBx.VisibilityA = "True";
		floatBx.AccessModeA = "w";
		floatBx.NameA = "CESHI2";
		floatBx.DisplayNameA = "测试3";
		list2.Add(floatBx);
		EnumerationBxDynamic enumerationBxDynamic = new EnumerationBxDynamic();
		enumerationBxDynamic.DisplayNameA = "测试4";
		enumerationBxDynamic.NameA = "CESHI3";
		enumerationBxDynamic.EnumEntrysA = new ObservableCollection<object>();
		enumerationBxDynamic.EnumEntrysA.Add(new EnumEntry
		{
			DisplayNameA = "下拉1",
			ValueA = 1
		});
		enumerationBxDynamic.EnumEntrysA.Add(new EnumEntry
		{
			DisplayNameA = "下拉2",
			ValueA = 2
		});
		enumerationBxDynamic.Value = "2";
		enumerationBxDynamic.Value = "1";
		list2.Add(enumerationBxDynamic);
		EnumerationBxDynamic enumerationBxDynamic2 = new EnumerationBxDynamic();
		enumerationBxDynamic2.DisplayNameA = "测试5";
		enumerationBxDynamic2.NameA = "CESHI4";
		enumerationBxDynamic2.EnumEntrysA = new ObservableCollection<object>();
		enumerationBxDynamic2.EnumEntrysA.Add(new EnumEntry
		{
			DisplayNameA = "下拉1",
			ValueA = 1
		});
		enumerationBxDynamic2.EnumEntrysA.Add(new EnumEntry
		{
			DisplayNameA = "下拉2",
			ValueA = 2
		});
		enumerationBxDynamic2.Value = "2";
		list2.Add(enumerationBxDynamic2);
		int num = 0;
		foreach (NameValueItem item in list2)
		{
			DynamicCameraNameItemOfGroup dynamicCameraNameItemOfGroup = new DynamicCameraNameItemOfGroup
			{
				Source = item,
				Index = 0,
				TreeGroupName = ((num > 2) ? "分组2" : AppLocalizationService.GetLang("默认分组"))
			};
			string nameA = dynamicCameraNameItemOfGroup.NameA;
			dynamicCameraNameItemOfGroup.SetParentNode(this);
			dynamicCameraNameItemOfGroup.Source.SetParentNode(dynamicCameraNameItemOfGroup);
			list.Add(dynamicCameraNameItemOfGroup);
			num++;
		}
		return list;
	}

	private List<DynamicCameraNameItemOfGroup> GetAllNameItemValue()
	{
		List<DynamicCameraNameItemOfGroup> list = new List<DynamicCameraNameItemOfGroup>();
		bool flag = false;
		if (Owner != null)
		{
			byte[] array = ((IUserBytesData)Owner.Params)["DynamicCameraParamList"];
			ImvsSdkDefine.KEY_NODE_INFO_LIST list2 = ExternalCallHelper.BytesToStruct<ImvsSdkDefine.KEY_NODE_INFO_LIST>(array, 0, array.Length);
			ImvsSdkDefine.KEY_NODE_INFO_LIST_EX structure = new ImvsSdkDefine.KEY_NODE_INFO_LIST_EX(list2);
			int num = Marshal.SizeOf(typeof(ImvsSdkDefine.KEY_NODE_INFO_LIST));
			int num2 = Marshal.SizeOf(typeof(ImvsSdkDefine.KEY_NODE_INFO_LIST_EX));
			int num3 = Marshal.SizeOf(typeof(ImvsSdkDefine.KEY_NODE_INFO));
			int num4 = Marshal.SizeOf(structure);
			LogHelper.Info($"list的size为{num},listex的size为{num2},单个节点的size为{num3},实际接收的ex结构体的大小为{num4}");
			for (int i = 0; i < structure.nNodeNum; i++)
			{
				NameValueItem nameValueItem = BuildNameValueItem(structure.kEY_NODE_INFOs[i]);
				if (nameValueItem != null)
				{
					string text = string.Empty;
					if (structure.kEY_NODE_INFOs[i].strGroupName != null)
					{
						text = StringUtil.UTF8GetString(structure.kEY_NODE_INFOs[i].strGroupName);
					}
					if (string.IsNullOrEmpty(text))
					{
						text = AppLocalizationService.GetLang("默认分组");
						Owner.Params["ModifyParamGroupName"] = nameValueItem.Name + ";" + AppLocalizationService.GetLang("默认分组");
						flag = true;
					}
					DynamicCameraNameItemOfGroup dynamicCameraNameItemOfGroup = new DynamicCameraNameItemOfGroup
					{
						Source = nameValueItem,
						TreeGroupName = text
					};
					string nameA = dynamicCameraNameItemOfGroup.NameA;
					dynamicCameraNameItemOfGroup.SetParentNode(this);
					dynamicCameraNameItemOfGroup.Source.SetParentNode(dynamicCameraNameItemOfGroup);
					list.Add(dynamicCameraNameItemOfGroup);
				}
			}
		}
		if (flag)
		{
			UpdateOrAddUserData("groupNames", AppLocalizationService.GetLang("默认分组"));
		}
		return list;
	}

	private void UpdateOrAddUserData(string keyName, string Value)
	{
		byte[] array = Owner.UserData[keyName];
		if (array != null)
		{
			string text = StringUtil.UTF8GetString(array);
			if (!string.IsNullOrEmpty(text))
			{
				string[] source = text.Split(';');
				if (!source.ToList().Contains(Value))
				{
					text = Value + ";" + text;
					Owner.UserData[keyName] = StringUtil.UTF8GetBytes(text);
				}
			}
			else
			{
				Owner.UserData[keyName] = StringUtil.UTF8GetBytes(Value);
			}
		}
		else
		{
			Owner.UserData[keyName] = StringUtil.UTF8GetBytes(Value);
		}
	}

	private NameValueItem BuildNameValueItem(ImvsSdkDefine.KEY_NODE_INFO node)
	{
		NameValueItem nameValueItem = null;
		string text = StringUtil.UTF8GetString(node.strValue);
		switch (node.nNodeType)
		{
		case 2:
			nameValueItem = new Integer64BxDynamic();
			break;
		case 5:
			nameValueItem = new FloatBxDynamic();
			break;
		case 9:
		{
			nameValueItem = new EnumerationBxDynamic();
			string[] array = text.Split('$');
			break;
		}
		case 4:
			nameValueItem = new CommandSetWidth();
			(nameValueItem as CommandSetWidth).WidthA = 356.0;
			break;
		case 6:
			nameValueItem = new StringBxDynamic();
			nameValueItem.Value = text;
			break;
		case 3:
			nameValueItem = new BooleanBxDynamic();
			nameValueItem.Value = text;
			break;
		}
		if (nameValueItem == null)
		{
			return nameValueItem;
		}
		nameValueItem.SubscribeVisibleA = false;
		nameValueItem.NameA = StringUtil.UTF8GetString(node.strNodeName);
		nameValueItem.DisplayNameA = StringUtil.UTF8GetString(node.strDisplayName);
		nameValueItem.DescriptionA = StringUtil.UTF8GetString(node.strDisplayName);
		if (nameValueItem is IDynamicCameraProperty dynamicCameraProperty)
		{
			dynamicCameraProperty.IsNotifyRefreshParam = true;
		}
		return nameValueItem;
	}

	public override int GetParamUnit()
	{
		try
		{
			ObservableCollection<DynamicCameraNameItemOfGroup> observableCollection = Collection;
			foreach (DynamicCameraNameItemOfGroup item2 in Collection)
			{
				if (item2.Source is IDynamicCameraProperty)
				{
					(item2.Source as IDynamicCameraProperty).IsTriggerChanged = true;
				}
			}
			string text = Owner.Params["DynamicParamDisplayStatus"];
			string[] array = text.Split('$');
			if (array[0] != "NULL")
			{
				for (int i = 0; i < array.Length; i++)
				{
					string[] sss = array[i].Split(';');
					DynamicCameraNameItemOfGroup item = Collection.FirstOrDefault((DynamicCameraNameItemOfGroup x) => x.NameA == sss[0]);
					if (item == null)
					{
						continue;
					}
					if (sss[1] == "2" || sss[1] == "3" || sss[1] == "4")
					{
						item.Source.VisibilityA = "True";
						CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
						{
							if (!item.Group.Collection.Contains(item))
							{
								item.Group.Collection.Add(item);
							}
						});
					}
					else
					{
						item.Source.VisibilityA = "False";
						CurrentUIHelper.Instance.Dispatcher.Invoke(() =>
						{
							if (item.Group.Collection.Contains(item))
							{
								item.CheckEnabled = false;
								item.Group.Collection.Remove(item);
							}
						});
					}
					if (sss[1] == "4")
					{
						item.Source.AccessModeA = "rw";
						item.EnableA = true;
						if (item.Source is StringBx stringBx)
						{
							stringBx.IsReadOnlyA = false;
						}
					}
					else
					{
						item.Source.AccessModeA = "r";
						item.EnableA = false;
					}
				}
			}
			foreach (DynamicCameraNameItemOfGroup item3 in Collection)
			{
				try
				{
					item3.GetParamUnit();
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception ex2)
		{
			LogHelper.Info(ex2.Message);
		}
		finally
		{
			foreach (DynamicCameraNameItemOfGroup item4 in Collection)
			{
				if (item4.Source is IDynamicCameraProperty)
				{
					(item4.Source as IDynamicCameraProperty).IsTriggerChanged = false;
				}
			}
			OnPropertyChanged("GroupCollection");
		}
		return 0;
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			algorithmTabRoot.PropertyChanged += Root_PropertyChanged;
		}
		return this;
	}

	private void Root_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (!(e.PropertyName == "TabIndex") || !(sender is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		try
		{
			Tab tab = GetParentNode<Tab>();
			if (algorithmTabRoot.TabsA[algorithmTabRoot.TabIndex] is Tab tab2 && tab2 == tab)
			{
				tab2.GetItemsValueUnit();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Info(ex.Message);
		}
	}

	IList IDynamicChild.GetDynamicChild()
	{
		return collection;
	}
}
