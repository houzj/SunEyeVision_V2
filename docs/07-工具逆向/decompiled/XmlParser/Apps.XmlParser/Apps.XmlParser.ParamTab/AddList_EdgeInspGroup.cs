using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class AddList_EdgeInspGroup : AddList_MultiSubModel, ISubChild, ISubInfoCollection, IUpdateGroupItems
{
	private bool isInit = false;

	public int SubModuleTabNum = 2;

	public bool IsFirstSetParamCurValue = false;

	public IList<object> SubChildren
	{
		get
		{
			List<object> childList = new List<object>();
			(from k in EnumEntrysA.SelectMany((SubData p) => (p as SubData_EdgeInspGroup).info.AlgorithmItems.Tabs).SelectMany((object q) => (q as Tab).Collection)
				where k is ISubChild || k is ISubInfo
				select k).ToList().ForEach((object o) =>
			{
				if (o is ISubInfo)
				{
					childList.Add(o);
				}
				else if (o is ISubChild { SubChildren: var subChildren })
				{
					subChildren.Where((object p) => p is ISubInfo).ToList().ForEach((object y) =>
					{
						childList.Add(y);
					});
				}
			});
			return childList;
		}
	}

	public string CurrentParamName => "";

	protected override void CloseTab(SubData_EdgeInspGroup item)
	{
		if (item != null && item.IsOpenDetail)
		{
			item.IsOpenDetail = false;
			(item.EnumEntrysA[1] as SubModuleInfoItem).Checked_Btn1 = false;
			(item.EnumEntrysA[1] as SubModuleInfoItem).Checked_Btn2 = false;
			if ((item.EnumEntrysA[1] as SubModuleInfoItem).TabNum == 3)
			{
				(item.EnumEntrysA[1] as SubModuleInfoItem).Checked_Btn3 = false;
			}
			item.SetListSource(null, 0);
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		Headers = null;
		string displayName = "Lang_Type";
		string displayName2 = "Params Settings";
		HeadItem headItem = new HeadItem
		{
			DisplayName = displayName,
			Width = 50.0
		};
		headItem.SetParentNode(this);
		HeadItem headItem2 = new HeadItem
		{
			DisplayName = displayName2,
			Width = 200.0
		};
		headItem2.SetParentNode(this);
		HeadersA = new ObservableCollection<HeadItem> { headItem, headItem2 };
		return this;
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
	}

	public override void OnInniterBusinessFinish()
	{
		base.OnInniterBusinessFinish();
		foreach (SubData item in EnumEntrysA)
		{
			if (item is SubData_EdgeInspGroup { SourceTab: not null } subData_EdgeInspGroup)
			{
				subData_EdgeInspGroup.SourceTab.DoFinish();
			}
		}
		CurItem = null;
	}

	public override void DoAdd(string value = null, string hashCode = null, int moduleInfo = -1)
	{
		bool flag = false;
		if (hashCode == null)
		{
			flag = true;
		}
		if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: IVmModule owner } algorithmTabRoot))
		{
			return;
		}
		if (owner.ModuleTypeName.Equals("IMVSLineFindGroupModu"))
		{
			int num = 3;
			if (Enumerable.Count(EnumEntrysA) >= num)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + num, MessageButtons.Normal, MessageIcons.Warning);
				return;
			}
		}
		if (AddItemTemplates == null)
		{
			return;
		}
		SubModuleInfo subModuleInfo = AddItemTemplates.FirstOrDefault((SubModuleInfo p) => p.Name == value);
		if (subModuleInfo == null)
		{
			return;
		}
		SubModuleInfo subModuleInfo2 = subModuleInfo.Clone<SubModuleInfo>();
		if (subModuleInfo2 != null)
		{
			subModuleInfo2.HashCode = hashCode ?? GenerateUniqueText();
			subModuleInfo2.SetParentNode(owner);
			if (subModuleInfo2.ModuleParamRoot != null)
			{
				subModuleInfo2.ModuleParamRoot.Owner = owner;
			}
			if (flag)
			{
				AddIo(subModuleInfo2);
				SetParamUnit("Add:" + subModuleInfo2.HashCode + "_" + subModuleInfo2.Name);
			}
			if (subModuleInfo2.AlgorithmRoot != null)
			{
				subModuleInfo2.AlgorithmRoot.Owner = owner;
				subModuleInfo2.AlgorithmRoot.UserStringData = owner.Params;
				subModuleInfo2.AlgorithmRoot.HashCode = subModuleInfo2.HashCode;
				subModuleInfo2.AlgorithmRoot.Root = algorithmTabRoot;
				subModuleInfo2.AlgorithmRoot.ChangeItemNames(subModuleInfo2.HashCode);
			}
			AddItem(subModuleInfo2);
		}
	}

	public void UpdateIo(string moduleName, Dictionary<string, string> dicName)
	{
		if (AddItemTemplates == null)
		{
			return;
		}
		SubModuleInfo subModuleInfo = AddItemTemplates.FirstOrDefault((SubModuleInfo p) => p.Name == moduleName);
		if (subModuleInfo == null)
		{
			return;
		}
		SubParamRoot moduleParamRoot = subModuleInfo.ModuleParamRoot;
		if (moduleParamRoot == null)
		{
			return;
		}
		Apps.XmlParser.Category paramCategoryByName = moduleParamRoot.GetParamCategoryByName("Input");
		if (paramCategoryByName == null)
		{
			return;
		}
		VmModule vmModule = Operation as VmModule;
		Dictionary<VmIO, Combination> dictionary = new Dictionary<VmIO, Combination>();
		foreach (VmIO input in vmModule.Inputs)
		{
			string name = input.Name;
			string[] array = name.Split('_');
			if (array.Length != 2)
			{
				continue;
			}
			string text = array[0];
			string text2 = array[1];
			if (text.Length <= 1 || text2.Length <= 1)
			{
				continue;
			}
			string text3 = text.Substring(1, text.Length - 1);
			string hashCode = text2.Substring(0, text2.Length - 1);
			if (dicName.Keys.Contains(text3))
			{
				string newName = dicName[text3];
				Combination combination = paramCategoryByName.ItemsA.OfType<Combination>().FirstOrDefault((Combination p) => p.Name == newName);
				if (combination != null)
				{
					ChangeParamName(combination, hashCode);
					dictionary.Add(input, combination);
				}
			}
		}
		foreach (VmIO key in dictionary.Keys)
		{
			vmModule.RemoveIo("Input", key);
			vmModule.AddIO(dictionary[key], vmModule, vmModule.Inputs);
		}
	}

	private void AddIo(SubModuleInfo newNode)
	{
		SubParamRoot moduleParamRoot = newNode.ModuleParamRoot;
		if (moduleParamRoot == null)
		{
			return;
		}
		Apps.XmlParser.Category paramCategoryByName = moduleParamRoot.GetParamCategoryByName("Input");
		if (paramCategoryByName == null)
		{
			return;
		}
		VmModule vmModule = moduleParamRoot.Owner as VmModule;
		foreach (object item in paramCategoryByName.ItemsA)
		{
			ChangeParamName(item, newNode.HashCode);
			DynamicVmIO dynamicVmIO = vmModule.AddIO(item, vmModule, vmModule.Inputs);
		}
	}

	private void ChangeParamName(object obj, string hashCode)
	{
		if (obj is Combination combination)
		{
			combination.Name = combination.Name + "_" + hashCode;
			{
				foreach (object filter2 in combination.Filters)
				{
					ChangeParamName(filter2, hashCode);
				}
				return;
			}
		}
		if (obj is Filter filter)
		{
			filter.Name = filter.Name + "_" + hashCode;
		}
	}

	internal void DeleteIo(string hashCode)
	{
		AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
		VmModule vmModule = algorithmTabRoot.Owner as VmModule;
		List<VmIO> list = vmModule.Inputs.Where((VmIO p) => p.Name.EndsWith(hashCode + "%")).ToList();
		foreach (VmIO item in list)
		{
			vmModule.RemoveIo("Input", item);
		}
	}

	private void AddItem(SubModuleInfo node)
	{
		SubModuleInfoItem subModuleInfoItem = new SubModuleInfoItem
		{
			DisplayType = "Lable_EdgeInspGroup"
		};
		SubModuleInfoItem subModuleInfoItem2 = new SubModuleInfoItem
		{
			DisplayType = "ButtonCommand_EdgeInspGroup",
			TabNum = SubModuleTabNum
		};
		SubData_EdgeInspGroup subData_EdgeInspGroup = new SubData_EdgeInspGroup
		{
			info = node,
			EnumEntrysA = new ObservableCollection<object> { subModuleInfoItem, subModuleInfoItem2 }
		};
		subModuleInfoItem.SetParentNode(subData_EdgeInspGroup);
		subModuleInfoItem2.SetParentNode(subData_EdgeInspGroup);
		subData_EdgeInspGroup.SetParentNode(this);
		EnumEntrysA.Add(subData_EdgeInspGroup);
	}

	public override int GetParamUnit()
	{
		try
		{
			if (!isInit)
			{
				IUserStringData moduleTagInfo = GetModuleTagInfo();
				Value = moduleTagInfo[ComName];
				LogHelper.objLog.Info("ComName：" + ComName + " Value--" + Value);
				ParseValue();
				isInit = true;
			}
			return 0;
		}
		catch (Exception ex)
		{
			LogHelper.Error("获取参数：" + ComName + "失败：" + ex.Message);
			return -1;
		}
	}

	private void ParseValue()
	{
		if (string.IsNullOrEmpty(Value))
		{
			return;
		}
		string[] array = Value.Split(' ');
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split('_');
			if (array3.Length == 2)
			{
				string hashCode = array3[0];
				string value = array3[1];
				DoAdd(value, hashCode);
			}
		}
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			VmModule vmModule = algorithmTabRoot.Owner as VmModule;
			vmModule.LoadModuleRelateIO();
		}
	}

	public int SetParamUnit(string val)
	{
		try
		{
			LogHelper.objLog.Info("ComName：" + ComName + " value--" + val);
			IUserBytesData moduleTagInfo = GetModuleTagInfo();
			moduleTagInfo[ComName] = StringUtil.UTF8GetBytesPadZero(val);
			return 0;
		}
		catch (VmException ex)
		{
			MyItemState = ItemState.SetParamErro;
			string errorInfo = ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			string lang = AppLocalizationService.GetLang(errorInfo);
			HikMessageBox.Show(lang, MessageIcons.Error);
			LogHelper.Error("设置参数：" + ComName + "失败：" + lang);
			return -1;
		}
		catch (Exception ex2)
		{
			MyItemState = ItemState.SetParamErro;
			int nErrorCode = 0;
			try
			{
				nErrorCode = Convert.ToInt32(ex2.Message, 16);
			}
			catch
			{
			}
			string errorInfo2 = ErrorCodeInfo.GetErrorInfo(nErrorCode);
			string lang2 = AppLocalizationService.GetLang(errorInfo2);
			HikMessageBox.Show(lang2, MessageIcons.Error);
			LogHelper.Error("设置参数：" + ComName + "失败：" + ex2.Message + lang2);
			return -1;
		}
	}

	public static string GenerateUniqueText(int num = 8)
	{
		string text = string.Empty;
		string text2 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		char[] array = new char[num];
		byte[] array2 = Guid.NewGuid().ToByteArray();
		for (int i = 0; i < num; i++)
		{
			array[i] = text2[(array2[i] + array2[num + i]) % 35];
		}
		char[] array3 = array;
		foreach (char c in array3)
		{
			text += c;
		}
		return text;
	}

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}

	void IUpdateGroupItems.SetItemsNull()
	{
		isInit = false;
		SetEnumEntrysNull();
	}
}
