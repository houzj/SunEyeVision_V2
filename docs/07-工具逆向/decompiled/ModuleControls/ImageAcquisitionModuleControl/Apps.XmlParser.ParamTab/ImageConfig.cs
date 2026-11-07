using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Apps.Data;
using Apps.Interface;
using Apps.Localization;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ImageConfig : NameValueItem
{
	private bool _isCameraMode = true;

	private object _lock = new object();

	public const int MaxCount = 8;

	private ObservableCollection<ImageConfigItem> items = new ObservableCollection<ImageConfigItem>();

	private IVMRun vMRun;

	private bool _isAllowSet = true;

	private DelegateCommand addCommand;

	public bool IsCameraMode
	{
		get
		{
			return _isCameraMode;
		}
		set
		{
			if (_isCameraMode != value)
			{
				_isCameraMode = value;
				OnPropertyChanged("IsCameraMode");
			}
		}
	}

	public ObservableCollection<ImageConfigItem> ItemsA
	{
		get
		{
			return items;
		}
		set
		{
			if (items != value)
			{
				items = value;
				OnPropertyChanged("ItemsA");
			}
		}
	}

	public IVMRun VMRun
	{
		get
		{
			if (vMRun != null)
			{
				return vMRun;
			}
			if (GetTopBaseNode() is IOwner owner)
			{
				vMRun = owner.Owner as IVMRun;
			}
			return vMRun;
		}
	}

	public bool IsAllowSet
	{
		get
		{
			return _isAllowSet;
		}
		set
		{
			if (_isAllowSet != value)
			{
				_isAllowSet = value;
				OnPropertyChanged("IsAllowSet");
			}
		}
	}

	public ICommand AddCommand => addCommand ?? (addCommand = new DelegateCommand(AddCommandExecute));

	public override void OnInniterBusinessFinish()
	{
		FunctionCameraManager.UnRegister("GetCameraParam");
		FunctionCameraManager.Register("GetCameraParam", GetCameraParam);
		FunctionCameraManager.UnRegister("GetImageConfigLight");
		FunctionCameraManager.Register("GetImageConfigLight", GetImageConfigLight);
		FunctionCameraManager.UnRegister("DeleteImageConfigLight");
		FunctionCameraManager.Register("DeleteImageConfigLight", DeleteImageConfigLight);
	}

	public void GetImageConfigLight()
	{
		foreach (ImageConfigItem item in ItemsA)
		{
			item.EnumerationLightA.OnPropertyChanged("EnumEntrysA");
			item.EnumerationLightA.OnPropertyChanged("CurValueA");
		}
	}

	public void DeleteImageConfigLight()
	{
		foreach (ImageConfigItem item in ItemsA)
		{
			item.EnumerationLightA.CurValueA = 0;
			item.EnumerationLightA.OnPropertyChanged("CurValueA");
		}
	}

	public override int GetParamUnit()
	{
		GetData();
		OpenConfigGetCameraParam();
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	private void AddCommandExecute()
	{
		if (VMRun != null && VMRun.IsRunning)
		{
			HikMessageBox.Show("Lang_OpeRun", MessageIcons.Warning, isTranslate: true);
			return;
		}
		int count = ItemsA.Count;
		if (count >= 8)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("添加") + AppLocalizationService.GetLang("超限") + ":" + 8, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		ImageConfigItem imageConfigItem = new ImageConfigItem();
		AddNewItem(imageConfigItem, count);
		imageConfigItem.ValuePropertyChanged += ParamChanged;
		imageConfigItem.SetParentNode(this);
	}

	private void AddNewItem(ImageConfigItem item, int serial)
	{
		if (item == null)
		{
			return;
		}
		item.Serial = serial + 1;
		ItemsA.Add(item);
		string strValue = string.Empty;
		string strValue2 = string.Empty;
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "ExposureTime", ref strValue);
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "ExposureTimeAbs", ref strValue2);
		string[] array = strValue.Split('$');
		string[] array2 = strValue2.Split('$');
		if (array.Length >= 4)
		{
			try
			{
				if (array[0] == "NULL")
				{
					array[0] = "0";
				}
				if (array2[0] == "NULL")
				{
					array2[0] = "0";
				}
				item.FloatExposure.MaxValueA = Convert.ToDouble((array[0] == "0") ? array2[2] : array[2]);
				item.FloatExposure.MinValueA = Convert.ToDouble((array[0] == "0") ? array2[1] : array[1]);
				item.FloatExposure.CurValueA = Convert.ToDouble((array[0] == "0") ? array2[0] : array[0]);
				try
				{
					item.FloatExposure.IncValueA = Convert.ToDouble((array[0] == "0") ? array2[3] : array[3]);
				}
				catch (Exception)
				{
					item.FloatExposure.IncValueA = 0.1;
				}
			}
			catch (Exception)
			{
			}
		}
		string strValue3 = string.Empty;
		string strValue4 = string.Empty;
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "Gain", ref strValue3);
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "GainRaw", ref strValue4);
		string[] array3 = strValue3.Split('$');
		string[] array4 = strValue4.Split('$');
		if (array3.Length >= 4)
		{
			try
			{
				if (array3[0] == "NULL")
				{
					array3[0] = "0";
				}
				if (array4[0] == "NULL")
				{
					array4[0] = "0";
				}
				item.FloatGain.MaxValueA = Convert.ToDouble((array3[0] == "0") ? array4[2] : array3[2]);
				item.FloatGain.MinValueA = Convert.ToDouble((array3[0] == "0") ? array4[1] : array3[1]);
				item.FloatGain.CurValueA = Convert.ToDouble((array3[0] == "0") ? array4[0] : array3[0]);
				try
				{
					item.FloatGain.IncValueA = Convert.ToDouble((array3[0] == "0") ? array4[3] : array3[3]);
				}
				catch (Exception)
				{
					item.FloatGain.IncValueA = 0.1;
				}
			}
			catch (Exception)
			{
			}
		}
		if (SetData() != 0)
		{
			ItemsA.Remove(item);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_AddImageConfigFailed"), MessageIcons.Error);
		}
	}

	public void ParamChanged(object sender, EventArgs e)
	{
		if (IsAllowSet && SetData() != 0)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_InfoTips"), AppLocalizationService.GetLang("Lang_ParamSetFail"), MessageButtons.Normal, MessageIcons.Error);
		}
	}

	public int SetData()
	{
		int result = -1;
		lock (_lock)
		{
			IMVS_IMAGE_CONFIG_LIST structure = new IMVS_IMAGE_CONFIG_LIST
			{
				nCount = ItemsA.Count,
				stImageConfig = new IMVS_IMAGE_CONFIG[8],
				nReserved = new int[4]
			};
			for (int i = 0; i < ItemsA.Count; i++)
			{
				structure.stImageConfig[i] = ItemsA[i].ConvertToIMVS_IMAGE_CONFIG();
				SetChannelBrightness(structure.stImageConfig[i], ItemsA[i]);
			}
			int num = Marshal.SizeOf<IMVS_IMAGE_CONFIG_LIST>();
			IntPtr intPtr = Marshal.AllocHGlobal(num);
			try
			{
				Marshal.StructureToPtr(structure, intPtr, fDeleteOld: true);
				result = ServerSDKManager.serverSDKManager.mModuleManager.SetBinaryData(GetVmModule().ID, "ImageConfigList", intPtr, num);
			}
			catch (VmException ex)
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
			}
			catch (Exception ex2)
			{
				HikMessageBox.Show(ex2.Message, MessageIcons.Error);
			}
			finally
			{
				if (intPtr != IntPtr.Zero)
				{
					Marshal.FreeHGlobal(intPtr);
					intPtr = IntPtr.Zero;
				}
			}
		}
		return result;
	}

	public void SetChannelBrightness(IMVS_IMAGE_CONFIG imageconfig, ImageConfigItem item)
	{
		switch (imageconfig.nDeviceType)
		{
		case 101:
			item.IntBright.MaxValueA = 255;
			break;
		case 103:
			item.IntBright.MaxValueA = 100;
			break;
		case 107:
			item.IntBright.MaxValueA = 100;
			break;
		case 110:
			item.IntBright.MaxValueA = 100;
			break;
		case 105:
			item.IntBright.MaxValueA = 100;
			break;
		}
	}

	public void GetData()
	{
		IsAllowSet = false;
		lock (_lock)
		{
			int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(GetVmModule().ID, "ImageConfigList", out var data);
			try
			{
				if (binaryData > 0)
				{
					IMVS_IMAGE_CONFIG_LIST iMVS_IMAGE_CONFIG_LIST = Marshal.PtrToStructure<IMVS_IMAGE_CONFIG_LIST>(data);
					Debugger.Log(0, null, "LightManagerUI：dataLen [" + binaryData + "], Items.Count[" + ItemsA.Count + "], target.Count[" + iMVS_IMAGE_CONFIG_LIST.nCount + "]");
					for (int i = 0; i < iMVS_IMAGE_CONFIG_LIST.nCount; i++)
					{
						IMVS_IMAGE_CONFIG item = iMVS_IMAGE_CONFIG_LIST.stImageConfig[i];
						AddImageConfig(item, i);
					}
				}
				else
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_GetImageConfigListERR"), MessageIcons.Error);
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
		IsAllowSet = true;
	}

	private void AddImageConfig(IMVS_IMAGE_CONFIG item, int serial)
	{
		try
		{
			if (ItemsA.Count < serial + 1)
			{
				ImageConfigItem item2 = new ImageConfigItem
				{
					Serial = item.nSerial
				};
				ItemsA.Add(item2);
			}
			ItemsA[serial].Serial = item.nSerial;
			ItemsA[serial].ValuePropertyChanged += ParamChanged;
			ItemsA[serial].SetParentNode(this);
			ItemsA[serial].FloatExposure.CurValueA = item.nFloatExposure;
			ItemsA[serial].FloatGain.CurValueA = item.nFloatGain;
			ItemsA[serial].IntTriggerTime.CurValueA = item.nIntTriggerTime;
			ItemsA[serial].IntChannel.CurValueA = item.nIntChannel;
			ItemsA[serial].IntBright.CurValueA = item.nIntBright;
			ItemsA[serial].FloatDistribution.CurValueA = item.nFloatDistribution;
			ItemsA[serial].FloatIrradiation.CurValueA = item.nFloatIrradiation;
			if (item.nIndex == -1)
			{
				return;
			}
			string text = item.nIndex + "#" + item.nId + "#" + item.nDeviceType + "#" + item.nchDeviceGuid;
			if (!(Value != text))
			{
				return;
			}
			OldValue = text;
			foreach (object item3 in ItemsA[serial].EnumerationLightA.EnumEntrysA)
			{
				if (item3 is EnumEntry enumEntry && enumEntry.ReservedProperty.ToString() == text)
				{
					ItemsA[serial].EnumerationLightA.CurValueA = enumEntry.ValueA;
					break;
				}
			}
		}
		catch
		{
		}
	}

	public void GetCameraParam()
	{
		string strValue = string.Empty;
		string strValue2 = string.Empty;
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "ExposureTime", ref strValue);
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "ExposureTimeAbs", ref strValue2);
		string[] array = strValue.Split('$');
		string[] array2 = strValue2.Split('$');
		if (array.Length >= 4)
		{
			try
			{
				if (array[0] == "NULL")
				{
					array[0] = "0";
				}
				if (array2[0] == "NULL")
				{
					array2[0] = "0";
				}
				foreach (ImageConfigItem item in ItemsA)
				{
					item.FloatExposure.MaxValueA = Convert.ToDouble((array[0] == "0") ? array2[2] : array[2]);
					item.FloatExposure.MinValueA = Convert.ToDouble((array[0] == "0") ? array2[1] : array[1]);
					item.FloatExposure.CurValueA = Convert.ToDouble((array[0] == "0") ? array2[0] : array[0]);
					try
					{
						item.FloatExposure.IncValueA = Convert.ToDouble((array[0] == "0") ? array2[3] : array[3]);
					}
					catch (Exception)
					{
						item.FloatExposure.IncValueA = 0.1;
					}
				}
			}
			catch (Exception)
			{
			}
		}
		else
		{
			foreach (ImageConfigItem item2 in ItemsA)
			{
				item2.FloatExposure.MaxValueA = 0.0;
				item2.FloatExposure.MinValueA = 0.0;
				item2.FloatExposure.CurValueA = 0.0;
				item2.FloatExposure.IncValueA = 1.0;
			}
		}
		string strValue3 = string.Empty;
		string strValue4 = string.Empty;
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "Gain", ref strValue3);
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "GainRaw", ref strValue4);
		string[] array3 = strValue3.Split('$');
		string[] array4 = strValue4.Split('$');
		if (array3.Length >= 4)
		{
			try
			{
				if (array3[0] == "NULL")
				{
					array3[0] = "0";
				}
				if (array4[0] == "NULL")
				{
					array4[0] = "0";
				}
				foreach (ImageConfigItem item3 in ItemsA)
				{
					item3.FloatGain.MaxValueA = Convert.ToDouble((array3[0] == "0") ? array4[2] : array3[2]);
					item3.FloatGain.MinValueA = Convert.ToDouble((array3[0] == "0") ? array4[1] : array3[1]);
					item3.FloatGain.CurValueA = Convert.ToDouble((array3[0] == "0") ? array4[0] : array3[0]);
					try
					{
						item3.FloatGain.IncValueA = Convert.ToDouble((array3[0] == "0") ? array4[3] : array3[3]);
					}
					catch (Exception)
					{
						item3.FloatGain.IncValueA = 0.1;
					}
				}
				return;
			}
			catch (Exception)
			{
				return;
			}
		}
		foreach (ImageConfigItem item4 in ItemsA)
		{
			item4.FloatGain.MaxValueA = 0.0;
			item4.FloatGain.MinValueA = 0.0;
			item4.FloatGain.CurValueA = 0.0;
			item4.FloatGain.IncValueA = 1.0;
		}
	}

	public void OpenConfigGetCameraParam()
	{
		string strValue = string.Empty;
		string strValue2 = string.Empty;
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "ExposureTime", ref strValue);
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "ExposureTimeAbs", ref strValue2);
		string[] array = strValue.Split('$');
		string[] array2 = strValue2.Split('$');
		if (array.Length >= 4)
		{
			try
			{
				if (array[0] == "NULL")
				{
					array[0] = "0";
				}
				if (array2[0] == "NULL")
				{
					array2[0] = "0";
				}
				foreach (ImageConfigItem item in ItemsA)
				{
					item.FloatExposure.MaxValueA = Convert.ToDouble((array[0] == "0") ? array2[2] : array[2]);
					item.FloatExposure.MinValueA = Convert.ToDouble((array[0] == "0") ? array2[1] : array[1]);
					try
					{
						item.FloatExposure.IncValueA = Convert.ToDouble((array[0] == "0") ? array2[3] : array[3]);
					}
					catch (Exception)
					{
						item.FloatExposure.IncValueA = 0.1;
					}
				}
			}
			catch (Exception)
			{
			}
		}
		else
		{
			foreach (ImageConfigItem item2 in ItemsA)
			{
				item2.FloatExposure.MaxValueA = 0.0;
				item2.FloatExposure.MinValueA = 0.0;
				item2.FloatExposure.CurValueA = 0.0;
				item2.FloatExposure.IncValueA = 1.0;
			}
		}
		string strValue3 = string.Empty;
		string strValue4 = string.Empty;
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "Gain", ref strValue3);
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(GetVmModule().ID, "GainRaw", ref strValue4);
		string[] array3 = strValue3.Split('$');
		string[] array4 = strValue4.Split('$');
		if (array3.Length >= 4)
		{
			try
			{
				if (array3[0] == "NULL")
				{
					array3[0] = "0";
				}
				if (array4[0] == "NULL")
				{
					array4[0] = "0";
				}
				foreach (ImageConfigItem item3 in ItemsA)
				{
					item3.FloatGain.MaxValueA = Convert.ToDouble((array3[0] == "0") ? array4[2] : array3[2]);
					item3.FloatGain.MinValueA = Convert.ToDouble((array3[0] == "0") ? array4[1] : array3[1]);
					try
					{
						item3.FloatGain.IncValueA = Convert.ToDouble((array3[0] == "0") ? array4[3] : array3[3]);
					}
					catch (Exception)
					{
						item3.FloatGain.IncValueA = 0.1;
					}
				}
				return;
			}
			catch (Exception)
			{
				return;
			}
		}
		foreach (ImageConfigItem item4 in ItemsA)
		{
			item4.FloatGain.MaxValueA = 0.0;
			item4.FloatGain.MinValueA = 0.0;
			item4.FloatGain.CurValueA = 0.0;
			item4.FloatGain.IncValueA = 1.0;
		}
	}

	public void ChangeVisibleT()
	{
		IsCameraMode = true;
	}

	public void ChangeVisibleF()
	{
		IsCameraMode = false;
	}
}
