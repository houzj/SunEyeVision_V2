#define DEBUG
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Xml;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public static class MVSDlg
{
	public static uint u_moduleID;

	public static string CameraClosed = "Close";

	public static string CameraReleased = "Release";

	public static string MvsCurDevice = "Release";

	public static string MvsNewDevice = "";

	public static VM_FT_DEVICEINFO st_deviceinfo = default;

	public static MVSState MvsDlgDtatus = MVSState.Released;

	public static IntPtr Handle;

	public static IntPtr pDeviceInfo;

	private static delegateGetFeatureCallBack GetFeatureCallBack = delegateGetFeatureBackFunc;

	private static delegateSetFeatureCallBack SetFeatureCallBack = delegateSetFeatureBackFunc;

	private static delegateGetAccessModeCallBack GetAccessModeCallBack = delegateGetAccessModeBackFunc;

	private static delegateInvalidateNodesCallBack InvalidateNodesCallBack = delegateInvalidateNodesBackFunc;

	private static delegateActionCallBack ActionCallBack = delegateActionCallBackFunc;

	private static bool CamerXmlEventInited = false;

	private static readonly object CamerXmlEventInitedLocker = new object();

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	private static void StrToMVCC_INTVALUE_EX(string sSetValue, out MVCC_INTVALUE_EX IntValue)
	{
		IntValue = new MVCC_INTVALUE_EX
		{
			nReserved = new uint[16]
		};
		string[] array = sSetValue.Split('$');
		int num = array.Length;
		if (num < 4)
		{
			LogHelper.Error(sSetValue + " can not split to nCurValue, nMin, nMax, nInc");
			return;
		}
		long.TryParse(array[0], out IntValue.nCurValue);
		long.TryParse(array[1], out IntValue.nMin);
		long.TryParse(array[2], out IntValue.nMax);
		long.TryParse(array[3], out IntValue.nInc);
	}

	private static void StrToMVCC_FLOATVALUE(string sSetValue, out MVCC_FLOATVALUE FloatValue)
	{
		FloatValue = new MVCC_FLOATVALUE
		{
			nReserved = new uint[4]
		};
		string[] array = sSetValue.Split('$');
		int num = array.Length;
		if (num < 4)
		{
			LogHelper.Error(sSetValue + " can not split to nCurValue, nMin, nMax");
			return;
		}
		float.TryParse(array[0], out FloatValue.fCurValue);
		float.TryParse(array[1], out FloatValue.fMin);
		float.TryParse(array[2], out FloatValue.fMax);
	}

	private static void StrToMVCC_STRINGVALUE(string sSetValue, out MVCC_STRINGVALUE StrValue)
	{
		StrValue = new MVCC_STRINGVALUE
		{
			chCurValue = new byte[256],
			nReserved = new uint[2]
		};
		if (sSetValue.Contains("$"))
		{
			string[] array = sSetValue.Split('$');
			if (array.Length < 2)
			{
				LogHelper.Error(sSetValue + " can not split to MVCC_STRINGVALUE");
				return;
			}
			StrValue.chCurValue = Encoding.Default.GetBytes(array[0].PadRight(256, '\0'));
			long.TryParse(array[1], out StrValue.nMaxLength);
		}
		else
		{
			StrValue.chCurValue = Encoding.Default.GetBytes(sSetValue.PadRight(256, '\0'));
			StrValue.nMaxLength = 30L;
			LogHelper.Error("Param: " + sSetValue + " MaxLength get null, set default length 30");
		}
	}

	private static void StrToMVCC_ENUMVALUE(string sSetValue, out MVCC_ENUMVALUE EnumValue)
	{
		EnumValue = new MVCC_ENUMVALUE
		{
			nSupportValue = new uint[64],
			nReserved = new uint[4]
		};
		string[] array = sSetValue.Split('$');
		if (array.Length < 2)
		{
			LogHelper.Error(sSetValue + " can not split to MVCC_ENUMVALUE");
			return;
		}
		uint.TryParse(array[0], out EnumValue.nCurValue);
		EnumValue.nSupportedNum = Convert.ToUInt32(array.Length - 1);
		for (int i = 0; i < array.Length - 1; i++)
		{
			uint.TryParse(array[i + 1], out EnumValue.nSupportValue[i]);
		}
	}

	private static int delegateGetFeatureBackFunc(int nFeatureType, IntPtr pFeatureName, IntPtr pOutValue, IntPtr pUser)
	{
		Debug.WriteLine("MVS DEBUG GetFeature Fun Coming in");
		string text = Marshal.PtrToStringAnsi(pFeatureName);
		if (string.IsNullOrEmpty(text))
		{
			LogHelper.Error("sFeatureName is null or Empty");
			return -1;
		}
		string strValue = "";
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(u_moduleID, text, ref strValue);
		try
		{
			switch (nFeatureType)
			{
			case 2:
			{
				StrToMVCC_INTVALUE_EX(strValue, out var IntValue);
				Marshal.StructureToPtr(IntValue, pOutValue, fDeleteOld: false);
				break;
			}
			case 3:
			{
				bool.TryParse(strValue, out var result);
				Marshal.WriteByte(pOutValue, Convert.ToByte(result));
				break;
			}
			case 5:
			{
				StrToMVCC_FLOATVALUE(strValue, out var FloatValue);
				Marshal.StructureToPtr(FloatValue, pOutValue, fDeleteOld: false);
				break;
			}
			case 6:
			{
				StrToMVCC_STRINGVALUE(strValue, out var StrValue);
				Marshal.StructureToPtr(StrValue, pOutValue, fDeleteOld: false);
				break;
			}
			case 9:
			{
				StrToMVCC_ENUMVALUE(strValue, out var EnumValue);
				Marshal.StructureToPtr(EnumValue, pOutValue, fDeleteOld: false);
				break;
			}
			case 4:
			case 7:
			case 8:
				break;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("UI GetFeature Name(Param):" + text + "(" + strValue + ") /n Ex:" + ex.ToString());
			return -1;
		}
		return 0;
	}

	private static int delegateSetFeatureBackFunc(int nFeatureType, IntPtr pFeatureName, IntPtr pSetValue, IntPtr pUser)
	{
		string text = "";
		string text2 = "";
		try
		{
			text = Marshal.PtrToStringAnsi(pFeatureName);
			text2 = Marshal.PtrToStringAnsi(pSetValue);
			int num = -536870657;
			num = ServerSDKAPI.IMVS_SetParamValue(CVmObjectBase.mhandle, u_moduleID, text, text2);
			if (num != 0)
			{
				LogHelper.Error("UI SetFeature Name(Param):" + text + "(" + text2 + ") /n Ex:" + num);
				return num;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("UI SetFeature Name(Param):" + text + "(" + text2 + ") /n Ex:" + ex.ToString());
			int.TryParse(ex.ToString(), out var result);
			return result;
		}
		return 0;
	}

	private static int delegateGetAccessModeBackFunc(IntPtr pFeatureName, IntPtr pOutAccessMode, IntPtr pUser)
	{
		string strValue = "";
		string text = "";
		try
		{
			text = Marshal.PtrToStringAnsi(pFeatureName);
			string strName = "NodeAccessMode-" + text;
			ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(u_moduleID, strName, ref strValue);
			if (string.IsNullOrEmpty(strValue))
			{
				LogHelper.Error("UI AccessMode Empty:" + text);
			}
			if (pOutAccessMode == IntPtr.Zero)
			{
				pOutAccessMode = Marshal.AllocHGlobal(strValue.Length);
			}
			int.TryParse(strValue, out var result);
			Marshal.WriteInt32(pOutAccessMode, result);
		}
		catch (Exception ex)
		{
			LogHelper.Error("UI AccessMode Name(accessmode):" + text + "(" + strValue + ") /n Ex:" + ex.ToString());
			return -1;
		}
		return 0;
	}

	private static int delegateInvalidateNodesBackFunc(IntPtr pUser)
	{
		try
		{
			ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(u_moduleID, "InvalidateNodes", "");
			LogHelper.Info("SDK SetParamValue: InvalidateNodes");
		}
		catch (Exception ex)
		{
			LogHelper.Error("Exception:" + ex.ToString());
			return -1;
		}
		return 0;
	}

	private static int delegateActionCallBackFunc(int nAction, IntPtr pUser)
	{
		try
		{
			if (System.Windows.Application.Current == null)
			{
				VmModule moduleByID = VmSolution.Instance.GetModuleByID(u_moduleID);
				(moduleByID.Params.Data as AlgorithmTabRoot)?.GetItemsAllValueUnit();
				UpdateXml(moduleByID);
			}
			else
			{
				System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(() =>
				{
					VmModule moduleByID2 = VmSolution.Instance.GetModuleByID(u_moduleID);
					if (moduleByID2 != null)
					{
						(moduleByID2.Params.Data as AlgorithmTabRoot)?.GetItemsAllValueUnit();
						UpdateXml(moduleByID2);
					}
				}));
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("mvs 控件窗口关闭异常:" + ex.ToString());
		}
		return 0;
	}

	private static void UpdateXml(VmModule vmModule)
	{
		XmlDocument xmlDocument = new XmlDocument();
		if (MvsCurDevice != CameraClosed && MvsCurDevice != CameraReleased)
		{
			SetMvsStatus(MVSState.Released);
		}
		xmlDocument.Load("C:\\Windows\\Temp\\FeatureTree\\FavoriteFeatures.xml");
		string innerXml = xmlDocument.InnerXml;
		vmModule.UserData["MvsConfig"] = StringUtil.UTF8GetBytes(innerXml);
		DynamicCameraParamList dynamicCameraParamList = (vmModule.Params.Data as AlgorithmTabRoot).FindBaseNodeByName("DynamiceList") as DynamicCameraParamList;
		XmlNodeList xmlNodeList = xmlDocument.SelectSingleNode("Favorites")?.ChildNodes;
		if (xmlNodeList == null)
		{
			return;
		}
		string text = string.Empty;
		foreach (XmlNode item in xmlNodeList)
		{
			text = text + item.SelectSingleNode("Name")?.InnerText + ";";
		}
		if (text.Contains(";"))
		{
			text = text.TrimEnd(';');
		}
		if (dynamicCameraParamList.Collection.Count == 0)
		{
			vmModule.Params["CameraNodeNameList"] = text;
		}
		else
		{
			vmModule.Params["UpdateNodeNameList"] = text;
		}
		dynamicCameraParamList.SetNull();
		dynamicCameraParamList.GetParamUnit();
	}

	public static void SetMvsStatus(MVSState state, bool ifrelease = true)
	{
		try
		{
			switch (state)
			{
			case MVSState.Released:
				if (MvsDlgDtatus != MVSState.Released && MvsDlgDtatus != MVSState.Closed)
				{
					if (ifrelease)
					{
						MvsRelease();
						break;
					}
					MvsDlgDtatus = MVSState.Released;
					MvsCurDevice = CameraClosed;
				}
				break;
			case MVSState.Closed:
				if (MvsDlgDtatus != MVSState.Closed && MvsDlgDtatus != MVSState.Released)
				{
					MvsClose();
				}
				break;
			case MVSState.Opened:
				if (MvsDlgDtatus != MVSState.Opened)
				{
					MvsDlgDtatus = MVSState.Opened;
				}
				break;
			}
		}
		catch
		{
		}
	}

	public static void MvsStart()
	{
		int lang = ((!(AppLocalizationService.GetLang("Language") == "English")) ? 1 : 0);
		int nRet = -1;
		if (System.Windows.Application.Current == null)
		{
			nRet = MVS_FT_INTERFACE.VM_FT_Start(bModel: true, lang);
		}
		else
		{
			System.Windows.Application.Current.Dispatcher.Invoke(() =>
			{
				nRet = MVS_FT_INTERFACE.VM_FT_Start(bModel: true, lang);
			});
		}
		if (nRet != 0)
		{
			LogHelper.Error("MvsStart VM_FT_Start failed ");
		}
	}

	public static void MvsInit()
	{
		IntPtr deviceXmlIntPtr = IntPtr.Zero;
		IntPtr translateXmlIntPtr = IntPtr.Zero;
		string mvsCurDevice = MvsCurDevice;
		MVSState mvsDlgDtatus = MvsDlgDtatus;
		try
		{
			int cb = Marshal.SizeOf(st_deviceinfo);
			pDeviceInfo = Marshal.AllocHGlobal(cb);
			Marshal.StructureToPtr(st_deviceinfo, pDeviceInfo, fDeleteOld: false);
			ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(u_moduleID, "DeviceXml", out deviceXmlIntPtr);
			ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(u_moduleID, "TranslateXml", out translateXmlIntPtr);
			int lang = ((!(AppLocalizationService.GetLang("Language") == "English")) ? 1 : 0);
			int posx = SystemInformation.WorkingArea.Width / 2 - 200;
			int posy = SystemInformation.WorkingArea.Height / 2 - 200;
			if (posx < 0)
			{
				posx = 0;
			}
			if (posy < 0)
			{
				posy = 0;
			}
			Handle = FindWindow(null, AppLocalizationService.GetLang("Lang_CameraSet"));
			_ = Handle;
			if (false)
			{
				LogHelper.Error("FindWindow null，mvs set father window failed. ");
			}
			MvsCurDevice = MvsNewDevice;
			MvsDlgDtatus = MVSState.Opened;
			if (System.Windows.Application.Current == null)
			{
				mvsinitopen(deviceXmlIntPtr, translateXmlIntPtr, posx, posy, lang);
				return;
			}
			System.Windows.Application.Current.Dispatcher.Invoke(() =>
			{
				mvsinitopen(deviceXmlIntPtr, translateXmlIntPtr, posx, posy, lang);
			});
		}
		catch (Exception ex)
		{
			MvsCurDevice = mvsCurDevice;
			MvsDlgDtatus = mvsDlgDtatus;
			System.Windows.MessageBox.Show("不支持中文相机ID，请更改ID名后重试");
			LogHelper.Error("MvsInit Ex:" + ex.ToString());
		}
		finally
		{
			if (IntPtr.Zero != translateXmlIntPtr)
			{
				Marshal.FreeHGlobal(translateXmlIntPtr);
				translateXmlIntPtr = IntPtr.Zero;
			}
			if (IntPtr.Zero != deviceXmlIntPtr)
			{
				Marshal.FreeHGlobal(deviceXmlIntPtr);
				deviceXmlIntPtr = IntPtr.Zero;
			}
		}
	}

	public static void MvsRelease()
	{
		try
		{
			int nRet = -1;
			if (System.Windows.Application.Current == null)
			{
				nRet = MVS_FT_INTERFACE.VM_FT_Release();
			}
			else
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					nRet = MVS_FT_INTERFACE.VM_FT_Release();
				});
			}
			if (nRet != 0)
			{
				LogHelper.Error("VM_FT_Release failed ");
				return;
			}
			MVSPDeinit();
			MvsCurDevice = CameraClosed;
			MvsDlgDtatus = MVSState.Closed;
		}
		catch (Exception ex)
		{
			LogHelper.Error("VM_FT_Release failed:" + ex.ToString());
		}
	}

	public static void MvsClose()
	{
		int nRet = -1;
		if (System.Windows.Application.Current == null)
		{
			nRet = MVS_FT_INTERFACE.VM_FT_Close();
		}
		else
		{
			System.Windows.Application.Current.Dispatcher.Invoke(() =>
			{
				nRet = MVS_FT_INTERFACE.VM_FT_Close();
			});
		}
		if (nRet != 0)
		{
			LogHelper.Error("MvsClose VM_FT_Close failed ");
			return;
		}
		MvsDlgDtatus = MVSState.Closed;
		MvsCurDevice = CameraClosed;
		MVSPDeinit();
	}

	private static void mvsinitopen(IntPtr pDeviceXml, IntPtr pTranslateXml, int posx, int posy, int lang)
	{
		int num = MVS_FT_INTERFACE.VM_FT_Init(pDeviceXml, pTranslateXml, Handle);
		num = MVS_FT_INTERFACE.VM_FT_SetDeviceInfo(pDeviceInfo);
		num = MVS_FT_INTERFACE.VM_FT_SetCallBackFunc(GetFeatureCallBack, SetFeatureCallBack, GetAccessModeCallBack, InvalidateNodesCallBack, ActionCallBack, Handle);
		num = MVS_FT_INTERFACE.VM_FT_SetWidgetSize(500, 800);
		num = MVS_FT_INTERFACE.VM_FT_SetWidgetPosition(posx, posy);
		num = MVS_FT_INTERFACE.VM_FT_SetTitleVisible(bVisible: true);
		num = MVS_FT_INTERFACE.VM_FT_Start(bModel: true, lang);
	}

	public static void MVSPDeinit()
	{
		Marshal.FreeHGlobal(pDeviceInfo);
		pDeviceInfo = IntPtr.Zero;
		Handle = IntPtr.Zero;
	}

	public static void InitCamerXmlEvent()
	{
		lock (CamerXmlEventInitedLocker)
		{
			if (!CamerXmlEventInited)
			{
				FunctionCameraManager.GetCamerXmlEvent += InitCamerParam;
				CamerXmlEventInited = true;
			}
		}
	}

	public static void UpdateCamerParam(VmModule module)
	{
		if (module == null || module.Params == null)
		{
			return;
		}
		string devicename = "Close";
		if (module.Params["Type"] == "0")
		{
			try
			{
				byte[] data = null;
				if (ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(module.ID, "CameraName", ref data, 60000u) == 0)
				{
					string text = StringUtil.UTF8GetString(data);
					string[] array = text.ToString().Split('$');
					int num = Convert.ToInt32(array[0]);
					devicename = array.Last().Split('\r')[num];
				}
			}
			catch
			{
				devicename = "Close";
			}
		}
		else
		{
			string strValue = "";
			ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(module.ID, "ConnectIP", ref strValue);
			if (strValue == "True")
			{
				string strValue2 = "";
				ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(module.ID, "CameraIP", ref strValue2);
				if (!string.IsNullOrEmpty(strValue2))
				{
					devicename = strValue2;
				}
			}
		}
		InitCamerParam(module.ID, devicename);
	}

	private static void InitCamerParam(uint moduleID, string devicename)
	{
		if (string.IsNullOrEmpty(devicename))
		{
			return;
		}
		if (devicename == CameraReleased)
		{
			SetMvsStatus(MVSState.Released);
			MvsNewDevice = CameraClosed;
			return;
		}
		if (devicename == CameraClosed)
		{
			SetMvsStatus(MVSState.Released);
			MvsNewDevice = CameraClosed;
			return;
		}
		if (MvsCurDevice != CameraClosed && MvsCurDevice != CameraReleased)
		{
			SetMvsStatus(MVSState.Released);
		}
		MvsNewDevice = devicename;
		st_deviceinfo.cDeviceName = Encoding.Default.GetBytes(devicename.PadRight(128, '\0'));
		st_deviceinfo.nDeviceType = 1;
		u_moduleID = moduleID;
	}
}
