using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Apps.Localization;
using Apps.Log;
using GateWayServer;
using Hik.Util;
using VM.Controls;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class CommunicationMgrHelper
{
	private static readonly object _lock = new object();

	public static Dictionary<GW_PF_eFIELD_TYPE, string> typeDict = new Dictionary<GW_PF_eFIELD_TYPE, string>
	{
		{
			(GW_PF_eFIELD_TYPE)1,
			"int"
		},
		{
			(GW_PF_eFIELD_TYPE)2,
			"float"
		},
		{
			(GW_PF_eFIELD_TYPE)3,
			"string"
		},
		{
			(GW_PF_eFIELD_TYPE)4,
			"byte"
		}
	};

	private static Dictionary<MODULE_DEVICE_TYPE, string> TypeDict = new Dictionary<MODULE_DEVICE_TYPE, string>
	{
		{
			(MODULE_DEVICE_TYPE)1,
			"tcpclient"
		},
		{
			(MODULE_DEVICE_TYPE)2,
			"tcpserver"
		},
		{
			(MODULE_DEVICE_TYPE)3,
			"udp"
		},
		{
			(MODULE_DEVICE_TYPE)5,
			"modbus"
		},
		{
			(MODULE_DEVICE_TYPE)6,
			"plc"
		},
		{
			(MODULE_DEVICE_TYPE)4,
			"serial"
		},
		{
			(MODULE_DEVICE_TYPE)7,
			"eip"
		}
	};

	public static int ModuNodeID { get; set; }

	public static string ModuDisplayName { get; set; }

	public static string ModuName { get; set; }

	public static string CommunicationAddress
	{
		get
		{
			if (string.IsNullOrWhiteSpace(ModuName) || ModuNodeID == 0)
			{
				return "";
			}
			return ModuNodeID + "+" + ModuName;
		}
	}

	public static IList GetAllCommunicationDevices(bool isAllCommType, bool isAddDefaulEntry, params string[] commTypes)
	{
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Invalid comparison between Unknown and I4
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Expected I4, but got Unknown
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		IList list = new List<EnumEntry>();
		if (!isAllCommType && commTypes == null)
		{
			return list;
		}
		ImvsSdkDefine.IMVS_MODULE_BASIC_INFO_LIST value;
		try
		{
			value = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetModuleListByType(3u).Value;
		}
		catch (VmException)
		{
			HikMessageBox.Show("Lang_GlobalCommGetModuleListERR", MessageIcons.Error, isTranslate: true);
			return null;
		}
		if (value.nNum != 0)
		{
			ModuNodeID = (int)value.astModuleInfo[0].nModuleID;
			ModuDisplayName = Hik.Util.StringUtil.UTF8GetString(value.astModuleInfo[0].strDisplayName);
			ModuName = Hik.Util.StringUtil.UTF8GetString(value.astModuleInfo[0].strModuleName);
			int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData((uint)ModuNodeID, "GetAllModules", out var data);
			try
			{
				if (binaryData > 0)
				{
					int num = Marshal.SizeOf(typeof(MODULE_INFO));
					int num2 = binaryData / num;
					for (int i = 0; i < num2; i++)
					{
						MODULE_INFO val = Marshal.PtrToStructure<MODULE_INFO>(data + i * num);
						EnumEntry enumEntry = new EnumEntry();
						enumEntry.Name = Hik.Util.StringUtil.UTF8GetStringTrimZero(val.byModuleName);
						enumEntry.DisplayNameA = val.nModuleId + " " + Hik.Util.StringUtil.UTF8GetStringTrimZero(val.byDisplayName);
						enumEntry.DescriptionA = enumEntry.DisplayNameA;
						enumEntry.Value = val.nModuleId;
						byte[] array = new byte[16];
						Buffer.BlockCopy(val.byGuid, 0, array, 0, 16);
						enumEntry.Tag = new Guid(array).ToString();
						enumEntry.IoNum = val.nIoNum;
						if ((int)val.eModuleType >= 101)
						{
							enumEntry.DeviceType = val.eLightIOType;
							enumEntry.ReservedProperty = val.nModuleId.ToString() + "#" + ModuNodeID + "#" + (int)val.eModuleType + "#0123456789ABCDEF";
						}
						else
						{
							enumEntry.ReservedProperty = val.eModuleType;
						}
						if (isAllCommType)
						{
							list.Add(enumEntry);
						}
						else if (TypeDict.ContainsKey(val.eModuleType) && commTypes.Contains(TypeDict[val.eModuleType]))
						{
							list.Add(enumEntry);
						}
					}
				}
			}
			catch (Exception ex2)
			{
				LogHelper.Error("获取通信设备枚举失败:" + ex2.Message + "\r\n" + ex2.StackTrace);
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
		if (isAddDefaulEntry && list.Count > 0)
		{
			string lang = AppLocalizationService.GetLang("Lang_PlzChoose");
			EnumEntry value2 = new EnumEntry
			{
				DisplayNameA = lang,
				DescriptionA = lang,
				ValueA = 0
			};
			list.Insert(0, value2);
		}
		return list;
	}

	public static bool GetCommunicationDeviceState(uint id)
	{
		ImvsSdkDefine.IMVS_MODULE_BASIC_INFO_LIST value;
		try
		{
			value = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetModuleListByType(3u).Value;
		}
		catch (VmException)
		{
			HikMessageBox.Show("Lang_GlobalCommGetModuleListERR", MessageIcons.Error, isTranslate: true);
			return false;
		}
		if (value.nNum != 0)
		{
			ModuNodeID = (int)value.astModuleInfo[0].nModuleID;
			string strValue = null;
			ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue((uint)ModuNodeID, $"{id}#-1#ConnectState", 100, ref strValue);
			if (strValue != null && bool.TryParse(strValue, out var result))
			{
				return result;
			}
		}
		return false;
	}

	public static IList GetAllAddresses(int devIndex)
	{
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		IList list = new ObservableCollection<object>();
		lock (_lock)
		{
			IntPtr data = IntPtr.Zero;
			try
			{
				int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(11000u, "GetModuleAllAddressInfo-" + devIndex, out data);
				if (binaryData > 0)
				{
					int num = Marshal.SizeOf(typeof(ADDRESS_INFO));
					int num2 = binaryData / num;
					if (devIndex >= 0)
					{
						for (int i = 0; i < num2; i++)
						{
							ADDRESS_INFO val = Marshal.PtrToStructure<ADDRESS_INFO>(data + i * num);
							EnumEntry enumEntry = new EnumEntry();
							enumEntry.Name = Hik.Util.StringUtil.UTF8GetStringTrimZero(val.byDisplayName);
							enumEntry.DisplayName = "%" + val.nAddressId + " " + Hik.Util.StringUtil.UTF8GetStringTrimZero(val.byDisplayName) + "%";
							enumEntry.Value = val.nAddressId;
							EnumEntry enumEntry2 = enumEntry;
							enumEntry2.Description = enumEntry2.DisplayName;
							list.Add(enumEntry2);
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
		if (list.Count > 0)
		{
			string lang = AppLocalizationService.GetLang("Lang_PlzChoose");
			EnumEntry value = new EnumEntry
			{
				DisplayNameA = lang,
				DescriptionA = lang,
				ValueA = 0
			};
			list.Insert(0, value);
		}
		return list;
	}

	public static IList GetAllSendEvents()
	{
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		IList list = new List<EnumEntry>();
		ImvsSdkDefine.IMVS_MODULE_BASIC_INFO_LIST value;
		try
		{
			value = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetModuleListByType(3u).Value;
		}
		catch (VmException)
		{
			HikMessageBox.Show("Lang_GlobalCommGetModuleListERR", MessageIcons.Error, isTranslate: true);
			return null;
		}
		if (value.nNum != 0)
		{
			ModuNodeID = (int)value.astModuleInfo[0].nModuleID;
			ModuDisplayName = Hik.Util.StringUtil.UTF8GetString(value.astModuleInfo[0].strDisplayName);
			ModuName = Hik.Util.StringUtil.UTF8GetString(value.astModuleInfo[0].strModuleName);
			int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData((uint)ModuNodeID, "GetAllSendEventInfo", out var data);
			try
			{
				if (binaryData > 0)
				{
					GW_ALL_EVENT_INFO val = Marshal.PtrToStructure<GW_ALL_EVENT_INFO>(data);
					for (int i = 0; i < val.nNum; i++)
					{
						GW_EVENT_INFO val2 = val.stSendEventInfo[i];
						EnumEntry enumEntry = new EnumEntry();
						enumEntry.Name = Hik.Util.StringUtil.UTF8GetStringTrimZero(val2.szEventName);
						enumEntry.DisplayNameA = $"%{val2.nEventId - 2000} {Hik.Util.StringUtil.UTF8GetStringTrimZero(val2.szEventName)}%";
						enumEntry.DescriptionA = enumEntry.DisplayNameA;
						enumEntry.ValueA = val2.nEventId;
						enumEntry.ReservedProperty = val2.nEventId;
						list.Add(enumEntry);
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
		if (list.Count > 0)
		{
			string lang = AppLocalizationService.GetLang("Lang_PlzChoose");
			EnumEntry value2 = new EnumEntry
			{
				DisplayNameA = lang,
				DescriptionA = lang,
				ValueA = 0
			};
			list.Insert(0, value2);
		}
		return list;
	}

	public static IList GetAllSendEventParams(int eventIndex)
	{
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		IList list = new ObservableCollection<object>();
		if (eventIndex <= 0)
		{
			return list;
		}
		lock (_lock)
		{
			IntPtr data = IntPtr.Zero;
			try
			{
				int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(11000u, "GetSendEventInputRule-" + eventIndex, out data);
				if (binaryData > 0)
				{
					int num = Marshal.SizeOf(typeof(SEND_EVENT_INPUT_RULE));
					int num2 = binaryData / num;
					if (eventIndex >= 0)
					{
						for (int i = 0; i < num2; i++)
						{
							SEND_EVENT_INPUT_RULE val = Marshal.PtrToStructure<SEND_EVENT_INPUT_RULE>(data + i * num);
							EnumEntry value = new EnumEntry
							{
								Name = Hik.Util.StringUtil.UTF8GetStringTrimZero(val.strParamName),
								DisplayName = "%" + Hik.Util.StringUtil.UTF8GetStringTrimZero(val.strParamName) + "%",
								Value = i + 1,
								ReservedProperty = typeDict[val.eFieldType]
							};
							list.Add(value);
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

	public static IList GetAllReceiveEvents()
	{
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		IList list = new List<EnumEntry>();
		ImvsSdkDefine.IMVS_MODULE_BASIC_INFO_LIST value;
		try
		{
			value = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GetModuleListByType(3u).Value;
		}
		catch (VmException)
		{
			HikMessageBox.Show("Lang_GlobalCommGetModuleListERR", MessageIcons.Error, isTranslate: true);
			return null;
		}
		if (value.nNum != 0)
		{
			ModuNodeID = (int)value.astModuleInfo[0].nModuleID;
			ModuDisplayName = Hik.Util.StringUtil.UTF8GetString(value.astModuleInfo[0].strDisplayName);
			ModuName = Hik.Util.StringUtil.UTF8GetString(value.astModuleInfo[0].strModuleName);
			int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData((uint)ModuNodeID, "GetAllRecvEventInfo", out var data);
			try
			{
				if (binaryData > 0)
				{
					GW_ALL_EVENT_INFO val = Marshal.PtrToStructure<GW_ALL_EVENT_INFO>(data);
					for (int i = 0; i < val.nNum; i++)
					{
						GW_EVENT_INFO val2 = val.stSendEventInfo[i];
						EnumEntry enumEntry = new EnumEntry();
						enumEntry.Name = Hik.Util.StringUtil.UTF8GetStringTrimZero(val2.szEventName);
						enumEntry.DisplayNameA = $"%{val2.nEventId - 1000} {Hik.Util.StringUtil.UTF8GetStringTrimZero(val2.szEventName)}%";
						enumEntry.DescriptionA = enumEntry.DisplayNameA;
						enumEntry.ValueA = val2.nEventId;
						enumEntry.ReservedProperty = val2.nEventId;
						list.Add(enumEntry);
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
		if (list.Count > 0)
		{
			string lang = AppLocalizationService.GetLang("Lang_PlzChoose");
			EnumEntry value2 = new EnumEntry
			{
				DisplayNameA = lang,
				DescriptionA = lang,
				ValueA = -1
			};
			list.Insert(0, value2);
		}
		return list;
	}

	public static IList GetAllReceiveEventParams(int eventIndex)
	{
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		IList list = new ObservableCollection<object>();
		if (eventIndex <= 0)
		{
			return list;
		}
		lock (_lock)
		{
			IntPtr data = IntPtr.Zero;
			try
			{
				int binaryData = ServerSDKManager.serverSDKManager.mModuleManager.GetBinaryData(11000u, "GetRecvEventOutputExternalRule-" + eventIndex, out data);
				if (binaryData > 0)
				{
					int num = Marshal.SizeOf(typeof(RECV_EVENT_OUTPUT_RULE));
					int num2 = binaryData / num;
					if (eventIndex >= 0)
					{
						for (int i = 0; i < num2; i++)
						{
							RECV_EVENT_OUTPUT_RULE val = Marshal.PtrToStructure<RECV_EVENT_OUTPUT_RULE>(data + i * num);
							EnumEntry value = new EnumEntry
							{
								Name = Hik.Util.StringUtil.UTF8GetStringTrimZero(val.byParamName),
								DisplayName = "%" + Hik.Util.StringUtil.UTF8GetString(val.byParamName) + "%",
								Value = i + 1,
								ReservedProperty = typeDict[val.eFieldType]
							};
							list.Add(value);
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
}
