using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Apps.Data;
using Apps.Data.Common;
using Apps.Localization;
using Apps.Log;
using GateWayServer;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class EnumerationCommunication : NameValueItem
{
	public delegate void SelectedChangedHandler(object sender, EventArgs e);

	public const string COMM_TYPE_NONE = "None";

	public const string COMM_TYPE_TCPCLIENT = "TCPClient";

	public const string COMM_TYPE_TCPSERVER = "TCPServer";

	public const string COMM_TYPE_UDP = "UDP";

	public const string COMM_TYPE_SERIAL = "Serial";

	public const string COMM_TYPE_BASIC = "Basic";

	public const string COMM_TYPE_IO = "IO";

	public const string COMM_TYPE_PLC = "PLC";

	public const string COMM_TYPE_MODBUS = "Modbus";

	public const string COMM_TYPE_EIP = "Eip";

	public string deviceType = "None";

	public string CommunicationType = "All";

	public string ProtocolType;

	public int DefaultValue;

	public int CurValue;

	private IList EnumEntrys;

	private IList addressList;

	public int NumA = -1;

	public int NumB = -1;

	public bool isDelete = false;

	public Dictionary<MODULE_DEVICE_TYPE, string> TypeDict = new Dictionary<MODULE_DEVICE_TYPE, string>
	{
		{
			(MODULE_DEVICE_TYPE)1,
			"TCPClient"
		},
		{
			(MODULE_DEVICE_TYPE)2,
			"TCPServer"
		},
		{
			(MODULE_DEVICE_TYPE)3,
			"UDP"
		},
		{
			(MODULE_DEVICE_TYPE)5,
			"PLC"
		},
		{
			(MODULE_DEVICE_TYPE)4,
			"Serial"
		},
		{
			(MODULE_DEVICE_TYPE)6,
			"Modbus"
		},
		{
			(MODULE_DEVICE_TYPE)7,
			"PLC"
		}
	};

	private DelegateCommand _SelectedChangedCommand = null;

	public int ModuNodeID { get; set; }

	public string ModuDisplayName { get; set; }

	public string ModuName { get; set; }

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

	public int CurValueA
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

	public string ProtocolTypeA
	{
		get
		{
			return ProtocolType;
		}
		set
		{
			if (ProtocolType != value)
			{
				ProtocolType = value;
				OnPropertyChanged("ProtocolTypeA");
			}
		}
	}

	public CommunicationType DeviceType => (CommunicationType)Enum.Parse(typeof(CommunicationType), deviceType);

	public IList EnumEntrysA
	{
		get
		{
			EnumEntrys = GetAllCommunicationDevices();
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

	public IList AddressList
	{
		get
		{
			addressList = CommunicationMgrHelper.GetAllAddresses(CurValue);
			return addressList;
		}
	}

	public override string Value
	{
		get
		{
			return GetPipeAddress();
		}
		set
		{
			CurValueA = AnalyzePipeAddress(value);
			SetProtocolType();
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
		set
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected Obj, but got Unknown
			_SelectedChangedCommand = (DelegateCommand)value;
		}
	}

	public event SelectedChangedHandler OnSelectedChanged;

	public override void OnInniterBusinessFinish()
	{
		FunctionCameraManager.Register("GetComm", GetComm);
	}

	private void GetComm()
	{
		int curValueA = CurValueA;
		bool flag = true;
		EnumEntrys = GetAllCommunicationDevices();
		foreach (object item in EnumEntrysA)
		{
			if ((item as EnumEntry).Value == curValueA)
			{
				flag = false;
				break;
			}
		}
		if (flag || curValueA < 0)
		{
			CurValueA = 0;
			SetProtocolType();
		}
		OnPropertyChanged("EnumEntrysA");
		OnPropertyChanged("CurValueA");
		GetParamUnit();
	}

	private IList GetAllCommunicationDevices()
	{
		IList list = new List<EnumEntry>();
		IList list2 = null;
		if ("All" == CommunicationType)
		{
			list2 = CommunicationMgrHelper.GetAllCommunicationDevices(true, true, (string[])null);
		}
		else
		{
			string[] commTypes = CommunicationType.ToLower().Split(';');
			list2 = CommunicationMgrHelper.GetAllCommunicationDevices(isAllCommType: false, isAddDefaulEntry: true, commTypes);
		}
		if (list2 != null && list2.Count > 0)
		{
			foreach (EnumEntry item in list2)
			{
				if (item.DeviceType == 0)
				{
					list.Add(item);
				}
			}
		}
		ModuNodeID = CommunicationMgrHelper.ModuNodeID;
		ModuName = CommunicationMgrHelper.ModuName;
		ModuDisplayName = CommunicationMgrHelper.ModuDisplayName;
		return list;
	}

	private void SetProtocolType()
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if (CurValueA == 0)
		{
			deviceType = "None";
			ProtocolTypeA = "None";
			return;
		}
		IList enumEntrysA = EnumEntrysA;
		if (enumEntrysA == null)
		{
			return;
		}
		foreach (EnumEntry item in enumEntrysA)
		{
			if (item.ValueA > 0 && item.ValueA == CurValueA)
			{
				deviceType = TypeDict[(MODULE_DEVICE_TYPE)item.ReservedProperty];
				if ("TCPClient" == deviceType || "TCPServer" == deviceType || "UDP" == deviceType || "Serial" == deviceType)
				{
					ProtocolTypeA = "Basic";
				}
				else
				{
					ProtocolTypeA = deviceType;
				}
				break;
			}
		}
	}

	private string GetPipeAddress()
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		string result = string.Empty;
		IList enumEntrysA = EnumEntrysA;
		if (enumEntrysA != null)
		{
			foreach (EnumEntry item in enumEntrysA)
			{
				if (item.ValueA > 0 && item.ValueA == CurValueA)
				{
					result = ModuNodeID + "+" + ModuName + "#" + item.Value + "#" + TypeDict[(MODULE_DEVICE_TYPE)item.ReservedProperty];
					break;
				}
			}
		}
		return result;
	}

	private int AnalyzePipeAddress(string pipeAddress)
	{
		int result = 0;
		if (!string.IsNullOrWhiteSpace(pipeAddress))
		{
			string s = "-1";
			string[] array = pipeAddress.Split('#');
			if (array.Length >= 2)
			{
				s = array[1];
			}
			int.TryParse(s, out result);
			if (result < 0 || result > GetMaxID())
			{
				result = 0;
			}
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
		catch
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
		if (CurValueA > GetMaxID() || CurValueA < 0)
		{
			CurValueA = 0;
			EnumEntrys = GetAllCommunicationDevices();
			OnPropertyChanged("EnumEntrysA");
			return;
		}
		TaskAsyncHelper.RunAsync(() =>
		{
			SetProtocolType();
			string value = Value;
			if (OldValue != value)
			{
				if (SetParamUnit() == 0)
				{
					OldValue = value;
					PreExecuteRun();
				}
				if ((OnSelectedChanged != null && (ProtocolType == "Modbus" || ProtocolType == "PLC")) || ProtocolType == "Eip")
				{
					EventArgs e = new EventArgs();
					OnSelectedChanged(this, e);
				}
				if (OnSelectedChanged != null && ProtocolType == "Basic")
				{
					EventArgs e2 = new EventArgs();
					OnSelectedChanged(this, e2);
				}
			}
			if (CurValueA > 0 && GetTopBaseNode() is AlgorithmTabRoot)
			{
				VmModule vmModule = GetVmModule();
				if (vmModule != null && !(vmModule.StrModuleName != "ReadDatasModule"))
				{
					VmIOCollection outputs = vmModule.Outputs;
					if (outputs != null)
					{
						VmIO vmIO = outputs.FirstOrDefault((VmIO p) => p?.CustomVisibleA ?? false);
						if (vmIO != null)
						{
							vmIO.TypeName = "string";
						}
					}
				}
			}
		});
	}

	internal void RefreshDataSource(int index)
	{
		EnumEntrys = GetAllCommunicationDevices();
		if (CurValueA >= EnumEntrys.Count)
		{
			CurValue = 0;
			ProtocolTypeA = "None";
			SetParamUnit();
		}
		else if (CurValueA >= index)
		{
			CurValue = 0;
			ProtocolTypeA = "None";
			SetParamUnit();
		}
	}

	internal void RefreshCommunication(int index)
	{
		EnumEntrys = GetAllCommunicationDevices();
		if (CurValueA >= EnumEntrys.Count)
		{
			CurValue = 0;
			ProtocolTypeA = "None";
			SetParamUnit();
		}
		else if (CurValueA == index + 1 || CurValueA == index + 2)
		{
			CurValue = 0;
			ProtocolTypeA = "None";
			SetParamUnit();
		}
	}

	internal void GetNumA(int index, bool IsDelete)
	{
		NumA = index;
		isDelete = IsDelete;
	}

	internal void GetNumB(int index)
	{
		NumB = index;
	}

	internal void SetCurValue(string strValue)
	{
		if (string.IsNullOrWhiteSpace(strValue))
		{
			CurValue = 0;
		}
		else
		{
			CurValue = AnalyzePipeAddress(strValue);
		}
	}

	private int GetMaxID()
	{
		int result = 0;
		List<int> list = (EnumEntrysA as List<EnumEntry>).Select((EnumEntry p) => p.Value).ToList();
		if (list.Count > 0)
		{
			list.Sort();
			result = list.Last();
		}
		return result;
	}
}
