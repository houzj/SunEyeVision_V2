using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Hik.Util;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class EnumerationLight : NameValueItem
{
	public int DefaultValue;

	public int CurValue;

	public string DeviceType;

	private IList EnumEntrys;

	private IList Lights = new int[10] { 101, 102, 103, 104, 105, 107, 109, 110, 8917764, 114 };

	private IList IOs = new int[10] { 103, 104, 105, 106, 110, 108, 109, 110, 8917764, 114 };

	private DelegateCommand _SelectedChangedCommand = null;

	private DelegateCommand _ClickCommand = null;

	private DelegateCommand _RefreshListCommand = null;

	public override string DisplayType
	{
		get
		{
			return "EnumerationLight";
		}
		set
		{
		}
	}

	public int DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
		}
	}

	private EnumEntry SelectedEnumEntry { get; set; }

	public virtual int CurValueA
	{
		get
		{
			try
			{
				byte[] array = GetVmModule().UserData[NameA + "CurVal"];
				if (array != null)
				{
					CurValue = Convert.ToInt32(VM.PlatformSDKCS.StringUtil.UTF8GetString(array));
				}
			}
			catch (Exception)
			{
			}
			return CurValue;
		}
		set
		{
			if (CurValue == value)
			{
				return;
			}
			CurValue = value;
			try
			{
				List<EnumEntry> list = new List<EnumEntry>();
				foreach (object item in EnumEntrysA)
				{
					list.Add(item as EnumEntry);
				}
				SelectedEnumEntry = list.FirstOrDefault((EnumEntry f) => f.ValueA == value);
				if ((GetTopBaseNode() as AlgorithmTabRoot).Owner is VmModule vmModule)
				{
					vmModule.UserData[NameA + "CurVal"] = VM.PlatformSDKCS.StringUtil.UTF8GetBytesPadZero(CurValue.ToString());
				}
			}
			catch (Exception)
			{
			}
			OnPropertyChanged("CurValueA");
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			EnumEntrys = GetAllRelativeDevices();
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
				OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public override string Value
	{
		get
		{
			foreach (EnumEntry item in EnumEntrysA)
			{
				if (item.Value == CurValue)
				{
					return item.ReservedProperty?.ToString();
				}
			}
			return null;
		}
	}

	public ICommand SelectedChangedCommand
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

	public ICommand ClickCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public ICommand RefreshListCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_RefreshListCommand == null)
			{
				_RefreshListCommand = new DelegateCommand((Action)RefreshListCmdExecute);
			}
			return (ICommand)_RefreshListCommand;
		}
	}

	public override void OnInniterBusinessFinish()
	{
		FunctionCameraManager.UnRegister("GetLight");
		FunctionCameraManager.Register("GetLight", GetLight);
		FunctionCameraManager.UnRegister("DeleteLight");
		FunctionCameraManager.Register("DeleteLight", DeleteLight);
	}

	public void GetLight()
	{
		OnPropertyChanged("EnumEntrysA");
		OnPropertyChanged("CurValueA");
	}

	public void DeleteLight()
	{
		GetParamUnit();
	}

	private List<object> GetAllConnectedLightSourceDevices()
	{
		ImvsSdkDefine.IMVS_MODULE_BASIC_INFO_LIST iMVS_MODULE_BASIC_INFO_LIST = default;
		try
		{
			iMVS_MODULE_BASIC_INFO_LIST = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetModuleListByType(7u).Value;
		}
		catch (VmException)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetGlobalLightListERR"), MessageIcons.Error);
			return null;
		}
		List<object> list = new List<object>();
		if (iMVS_MODULE_BASIC_INFO_LIST.nNum != 0)
		{
			uint nModuleID = iMVS_MODULE_BASIC_INFO_LIST.astModuleInfo[0].nModuleID;
			IntPtr data = IntPtr.Zero;
			try
			{
				int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(nModuleID, "LightDeviceList", out data);
				if (binaryData > 0)
				{
					IMVS_LIGHT_DEVICE_LIST iMVS_LIGHT_DEVICE_LIST = Marshal.PtrToStructure<IMVS_LIGHT_DEVICE_LIST>(data);
					int num = 0;
					for (int i = 0; i < iMVS_LIGHT_DEVICE_LIST.nCount; i++)
					{
						int nDeviceType = iMVS_LIGHT_DEVICE_LIST.stLightDevice[i].nDeviceType;
						nDeviceType = LightSourceHelper.GetNewLightTypeByOldLightType(nDeviceType);
						if (nDeviceType >= 101)
						{
							EnumEntry enumEntry = new EnumEntry();
							enumEntry.DisplayNameA = Hik.Util.StringUtil.UTF8GetString(iMVS_LIGHT_DEVICE_LIST.stLightDevice[i].chszDeviceName);
							enumEntry.ValueA = num + 1;
							enumEntry.ReservedProperty = iMVS_LIGHT_DEVICE_LIST.stLightDevice[i].nIndex + "#" + nModuleID + "#" + nDeviceType + "#" + iMVS_LIGHT_DEVICE_LIST.stLightDevice[i].chszDeviceGuid;
							if (Lights.Contains(nDeviceType) && IOs.Contains(nDeviceType))
							{
								enumEntry.DeviceType = 3;
							}
							else if (IOs.Contains(nDeviceType))
							{
								enumEntry.DeviceType = 2;
							}
							else if (Lights.Contains(nDeviceType))
							{
								enumEntry.DeviceType = 1;
							}
							list.Add(enumEntry);
							num++;
						}
					}
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				if (data != IntPtr.Zero)
				{
					Marshal.FreeHGlobal(data);
					data = IntPtr.Zero;
				}
			}
		}
		return list;
	}

	private List<object> GetAllCommunicationDevices()
	{
		List<object> list = new List<object>();
		IList allCommunicationDevices = CommunicationMgrHelper.GetAllCommunicationDevices(true, true, (string[])null);
		foreach (object item in allCommunicationDevices)
		{
			list.Add(item);
		}
		return list;
	}

	private IList GetAllRelativeDevices()
	{
		List<object> list = new List<object>();
		VmModule vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule m) => m.ID == 14000);
		if (vmModule != null)
		{
			List<object> allConnectedLightSourceDevices = GetAllConnectedLightSourceDevices();
			if (allConnectedLightSourceDevices != null && allConnectedLightSourceDevices.Count > 0)
			{
				list.AddRange(allConnectedLightSourceDevices);
			}
		}
		List<object> allCommunicationDevices = GetAllCommunicationDevices();
		if (allCommunicationDevices != null && allCommunicationDevices.Count > 0)
		{
			list.AddRange(allCommunicationDevices);
		}
		string lang = AppLocalizationService.GetLang("Lang_PlzChoose");
		IList list2 = new List<object>
		{
			new EnumEntry
			{
				DisplayNameA = lang,
				DescriptionA = lang,
				ValueA = 0,
				ReservedProperty = -1
			}
		};
		if (list != null && list.Count > 0)
		{
			foreach (EnumEntry item in list)
			{
				if (DeviceType == "Light")
				{
					if (item.DeviceType == 1 || item.DeviceType == 3)
					{
						list2.Add(item);
					}
				}
				else if (DeviceType == "IO" && (item.DeviceType == 2 || item.DeviceType == 3))
				{
					list2.Add(item);
				}
			}
		}
		return list2;
	}

	public override int SetParamUnit()
	{
		string text = Value;
		if (string.IsNullOrEmpty(text))
		{
			text = "-1";
		}
		return SetParamUnitByZmq(text);
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
			OldValue = text2;
			try
			{
				if (value != text2)
				{
					bool flag = false;
					foreach (object item in EnumEntrysA)
					{
						if (item is EnumEntry { ReservedProperty: not null } enumEntry && enumEntry.ReservedProperty.ToString() != "-1")
						{
							string text3 = enumEntry.ReservedProperty.ToString().Substring(0, enumEntry.ReservedProperty.ToString().LastIndexOf("#"));
							if (text3 == text2.Substring(0, text2.LastIndexOf("#")))
							{
								CurValueA = enumEntry.ValueA;
								flag = true;
								break;
							}
						}
					}
					if (!flag)
					{
						CurValueA = 0;
					}
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

	public void SelectedChangedCmdExecute()
	{
		if (IsUnConnect())
		{
			return;
		}
		string value = Value;
		if (OldValue != value && SetParamUnit() == 0)
		{
			OldValue = value;
			if (DeviceType == "Light" && GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot && ((GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule).StrModuleName != "ImageAcquisitionModule")
			{
				algorithmTabRoot.GetItemsValueUnit();
			}
			PreExecuteRun();
		}
	}

	public void ClickCmdExecute()
	{
		if (OpenGlobalLight() && EnumEntrysA.OfType<EnumEntry>()?.FirstOrDefault((EnumEntry e) => e.Name == SelectedEnumEntry?.Name) == null)
		{
			CurValueA = 0;
		}
	}

	public void RefreshListCmdExecute()
	{
		EnumEntrysA = GetAllRelativeDevices();
		OnPropertyChanged("CurValueA");
	}

	public virtual bool OpenGlobalLight()
	{
		try
		{
			FunctionManager.Execute("GlobalLightSet");
			return true;
		}
		catch (Exception ex)
		{
			LogHelper.Error("function GlobalCameraSet execute error:" + ex.ToString());
			return false;
		}
	}
}
