using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Hik.Util;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class EnumerationDataCenter : Enumeration
{
	public new delegate void SelectedChangedHandler(object sender, EventArgs e);

	public const int ModuleNodeIDBase = 0;

	public const int ModuleNodeIDBaseBefore = 12000;

	private Dictionary<int, string> dictNodeID4ModuleName = new Dictionary<int, string>();

	private Dictionary<int, string> dictNodeID4DisplayName = new Dictionary<int, string>();

	private bool flagFirstQueue = true;

	private new IList EnumEntrys;

	private IList queueList;

	private DelegateCommand _SelectedChangedCommand = null;

	public override string DisplayType => "Enumeration";

	public override int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public new IList EnumEntrysA
	{
		get
		{
			EnumEntrys = GetAllDataCenterModules(out dictNodeID4ModuleName, out dictNodeID4DisplayName);
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

	public IList QueueList
	{
		get
		{
			queueList = GetAllQueues(CurValueA);
			return queueList;
		}
	}

	public new ICommand SelectedChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
	}

	public override string Value
	{
		get
		{
			return CurValue2Value();
		}
		set
		{
			CurValueA = Value2CurValue(value);
		}
	}

	public new event SelectedChangedHandler OnSelectedChanged;

	private IList GetAllDataCenterModules(out Dictionary<int, string> NodeID4ModuleName, out Dictionary<int, string> NodeID4DisplayName)
	{
		IList list = new List<EnumEntry>();
		NodeID4ModuleName = new Dictionary<int, string>();
		NodeID4DisplayName = new Dictionary<int, string>();
		ImvsSdkDefine.IMVS_MODULE_BASIC_INFO_LIST iMVS_MODULE_BASIC_INFO_LIST = default;
		try
		{
			iMVS_MODULE_BASIC_INFO_LIST = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetModuleListByType(4u).Value;
		}
		catch (VmException ex)
		{
			string text = AppLocalizationService.GetLang("errCode:") + $"{ex.errorCode:X}" + Environment.NewLine;
			text = text + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(text, MessageIcons.Error);
			LogHelper.Error("GetAllDataCenterModules error, " + text);
			return null;
		}
		if (iMVS_MODULE_BASIC_INFO_LIST.nNum != 0)
		{
			for (int i = 0; i < iMVS_MODULE_BASIC_INFO_LIST.nNum; i++)
			{
				EnumEntry enumEntry = new EnumEntry();
				uint num = ((iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].nModuleID >= 12000) ? (iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].nModuleID - 12000) : iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].nModuleID);
				enumEntry.DisplayNameA = "%" + num + " " + Hik.Util.StringUtil.UTF8GetString(iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].strDisplayName) + "%";
				enumEntry.DescriptionA = "%" + num + " " + Hik.Util.StringUtil.UTF8GetString(iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].strDisplayName) + "%";
				enumEntry.ValueA = (int)iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].nModuleID;
				list.Add(enumEntry);
				NodeID4DisplayName.Add((int)iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].nModuleID, Hik.Util.StringUtil.UTF8GetString(iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].strDisplayName));
				NodeID4ModuleName.Add((int)iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].nModuleID, Hik.Util.StringUtil.UTF8GetString(iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[i].strModuleName));
			}
		}
		if (list.Count > 0)
		{
			EnumEntry value = new EnumEntry
			{
				DisplayNameA = strChoice,
				DescriptionA = strChoice,
				ValueA = -1
			};
			list.Insert(0, value);
		}
		return list;
	}

	private IList GetAllQueues(int moduNodeID)
	{
		IList list = new ObservableCollection<object>();
		if (0 > moduNodeID || dictNodeID4ModuleName == null || !dictNodeID4ModuleName.ContainsKey(moduNodeID))
		{
			return list;
		}
		IntPtr data = IntPtr.Zero;
		int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData((uint)moduNodeID, "DataQueueList", out data);
		if (binaryData > 0)
		{
			IMVS_DATAQUEUE_LIST iMVS_DATAQUEUE_LIST = Marshal.PtrToStructure<IMVS_DATAQUEUE_LIST>(data);
			for (int i = 0; i < iMVS_DATAQUEUE_LIST.nCount; i++)
			{
				IMVS_DATAQUEUE iMVS_DATAQUEUE = iMVS_DATAQUEUE_LIST.stDataQueue[i];
				EnumEntry enumEntry = new EnumEntry();
				enumEntry.DisplayNameA = "%" + Hik.Util.StringUtil.UTF8GetString(iMVS_DATAQUEUE.chszQueueName) + "%";
				enumEntry.DescriptionA = "%" + Hik.Util.StringUtil.UTF8GetString(iMVS_DATAQUEUE.chszQueueName) + "%";
				enumEntry.ReservedProperty = iMVS_DATAQUEUE.chszDataType;
				enumEntry.ValueA = i + 1;
				list.Add(enumEntry);
			}
		}
		if (data != IntPtr.Zero)
		{
			Marshal.FreeHGlobal(data);
		}
		if (list.Count > 0)
		{
			EnumEntry value = new EnumEntry
			{
				DisplayNameA = strChoice,
				DescriptionA = strChoice,
				ValueA = 0
			};
			list.Insert(0, value);
		}
		return list;
	}

	private void SelectedChangedCmdExecute()
	{
		if (IsUnConnect())
		{
			return;
		}
		string value = Value;
		if (OldValue != value)
		{
			int num = SetParamUnit();
			OldValue = value;
			if (num >= 0)
			{
				PreExecuteRun();
			}
			if (OnSelectedChanged != null)
			{
				EventArgs e = new EventArgs();
				OnSelectedChanged(this, e);
			}
		}
	}

	private string CurValue2Value()
	{
		string result = string.Empty;
		IList enumEntrysA = EnumEntrysA;
		if (enumEntrysA != null)
		{
			foreach (EnumEntry item in enumEntrysA)
			{
				if (item.ValueA > -1 && item.ValueA == CurValueA && dictNodeID4ModuleName.ContainsKey(item.ValueA))
				{
					result = item.ValueA + "+" + dictNodeID4ModuleName[item.ValueA];
					break;
				}
			}
		}
		return result;
	}

	private int Value2CurValue(string value)
	{
		int result = 0;
		IList enumEntrysA = EnumEntrysA;
		if (enumEntrysA != null && !string.IsNullOrWhiteSpace(value))
		{
			string value2 = value.Substring(0, value.LastIndexOf('+'));
			result = Convert.ToInt32(value2);
		}
		return result;
	}

	public override int SetParamUnit()
	{
		string value = Value;
		return SetParamUnitByZmq(value);
	}

	public override int GetParamUnit()
	{
		MyItemState = ItemState.Normal;
		string text = string.Empty;
		VmModule vmModule = GetVmModule();
		try
		{
			if (vmModule != null)
			{
				text = vmModule.Params[Name];
			}
			string text2 = text;
			string value = Value;
			try
			{
				if (value != text2)
				{
					OldValue = text2;
					Value = text2;
				}
			}
			catch (Exception ex)
			{
				string lang = AppLocalizationService.GetLang("Lang_ParamGet");
				HikMessageBox.Show(lang + ex.Message, MessageIcons.Error);
				LogHelper.Error("获取失败！！！！！名称：" + Name + " des:" + Description + "  获取值:" + text);
			}
			return 0;
		}
		catch (Exception)
		{
			MyItemState = ItemState.GetParamErro;
			return -1;
		}
	}
}
