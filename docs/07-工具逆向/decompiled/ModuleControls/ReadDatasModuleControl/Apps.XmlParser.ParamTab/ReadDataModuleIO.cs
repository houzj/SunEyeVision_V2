using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.Variable;
using Apps.XmlUI.ParamTab;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ReadDataModuleIO : BaseParamNode
{
	private const int TYPE_QUEUE = 0;

	private const int TYPE_COMMUNICATION = 1;

	private const int TYPE_GLOBALVAR = 2;

	private const int PLC_BASE = 10;

	private const int MODBUS_BASE = 26;

	private const string SPILT_COLON = ":";

	private bool flagRegisterEvent_CommDevice;

	private bool flagRegisterEvent_Queue;

	private bool flagRegisterEvent_OutType;

	private bool isOldVersion;

	private GlobalModuleType outputType;

	private CommunicationType deviceType;

	private string OldSetValue_QueueCenter;

	private string OldSetValue_GlobalVar;

	private string OldSetValue_Communication;

	private EnumerationDataCenter dataQueue;

	private EnumerationCommunication commDevice;

	private RadioSelecterEx3 outputTypeSelecter;

	private Enumeration _QueueSelecter;

	private EnumerationVar _GlobalVarSelecter;

	private Enumeration _CommDeviceSelecter;

	private Enumeration _PLCAddressSelecter;

	private Enumeration _ModbusAddressSelecter;

	private int _oldIndex = 0;

	private int _queueIndex = 0;

	private int _globalIndex = 0;

	private int _commDeviceIndex = 0;

	private int _pLCAddressIndex = 0;

	private int _modbusAddressIndex = 0;

	private string tmpOldName = "";

	private uint VarModuleID = 0u;

	public string id;

	private DelegateCommand _SelectedChangedCommand_Queue = null;

	private DelegateCommand _SelectedChangedCommand_GlobalVar = null;

	private DelegateCommand _SelectedChangedCommand_CommDevice = null;

	private DelegateCommand _SelectedChangedCommand_PLCAddress = null;

	private DelegateCommand _SelectedChangedCommand_ModbusAddress = null;

	public string IoName
	{
		get
		{
			if (parentNode is ReadDataDynamicIO)
			{
				tmpOldName = (parentNode as ReadDataDynamicIO).NameA;
				return tmpOldName;
			}
			return string.Empty;
		}
	}

	public int QueueIndex
	{
		get
		{
			int value = 0;
			GetUIParams("QueueIndex", ref value);
			_queueIndex = value;
			return _queueIndex;
		}
		set
		{
			if (_queueIndex != value)
			{
				_queueIndex = value;
				SetUIParams("QueueIndex", value.ToString());
				RecordGlobalQueueIndex();
			}
		}
	}

	public int GlobalIndex
	{
		get
		{
			int value = 0;
			GetVarUIParams("GlobalIndex", ref VarModuleID, ref value);
			_globalIndex = value;
			return _globalIndex;
		}
		set
		{
			if (_globalIndex != value)
			{
				_globalIndex = value;
				SetVarUIParams("GlobalIndex", value.ToString());
				RecordGlobalQueueIndex();
			}
		}
	}

	public int CommDeviceIndex
	{
		get
		{
			int value = 0;
			GetUIParams("CommDeviceIndex", ref value);
			_commDeviceIndex = value;
			return _commDeviceIndex;
		}
		set
		{
			if (_commDeviceIndex != value)
			{
				_commDeviceIndex = value;
				SetUIParams("CommDeviceIndex", value.ToString());
				RecordGlobalQueueIndex();
			}
		}
	}

	public int PLCAddressIndex
	{
		get
		{
			int value = 0;
			GetUIParams("PLCAddressIndex", ref value);
			_pLCAddressIndex = value;
			return _pLCAddressIndex;
		}
		set
		{
			if (_pLCAddressIndex != value)
			{
				_pLCAddressIndex = value;
				SetUIParams("PLCAddressIndex", value.ToString());
				RecordGlobalQueueIndex();
			}
		}
	}

	public int ModbusAddressIndex
	{
		get
		{
			int value = 0;
			GetUIParams("ModbusAddressIndex", ref value);
			_modbusAddressIndex = value;
			return _modbusAddressIndex;
		}
		set
		{
			if (_modbusAddressIndex != value)
			{
				_modbusAddressIndex = value;
				SetUIParams("ModbusAddressIndex", value.ToString());
				RecordGlobalQueueIndex();
			}
		}
	}

	public GlobalModuleType OutputType => outputType;

	public CommunicationType DeviceType => deviceType;

	public EnumerationDataCenter DataQueue => dataQueue;

	public EnumerationCommunication CommDevice => commDevice;

	public RadioSelecterEx3 OutputTypeSelecter => outputTypeSelecter;

	public string GlobalVarIndexUserName => 2 + ":" + SetValue_GlobalVar;

	public string QueueIndexUserName => IoName + ":QueueIndex\r" + _queueIndex;

	public string PLCIndexUserName => 11 + PLCAddressSelecter.CurValueA + ":" + SetValue_Communication;

	public string ModbusIndexUserName => 27 + ModbusAddressSelecter.CurValueA + ":" + SetValue_Communication;

	public string FilterInfor
	{
		get
		{
			if (parentNode is ReadDataDynamicIO)
			{
				return (parentNode as ReadDataDynamicIO).FilterInfor;
			}
			return string.Empty;
		}
	}

	public string SetValue_QueueCenter
	{
		get
		{
			if (QueueSelecter != null && parentNode is ReadDataDynamicIO)
			{
				return FilterInfor + ":" + QueueSelecter.Value + ":" + (parentNode as ReadDataDynamicIO).ImageNo;
			}
			return FilterInfor;
		}
	}

	public string SetValue_GlobalVar
	{
		get
		{
			if (GlobalVarSelecter != null)
			{
				object reservedProperty = GlobalVarSelecter.ReservedProperty;
				if (reservedProperty != null && reservedProperty is IVmModule vmModule)
				{
					return FilterInfor + ":" + GlobalVarSelecter.Value + "#" + vmModule.ID + "+" + vmModule.ModuleTypeName + ":" + (parentNode as ReadDataDynamicIO).ImageNo;
				}
				return FilterInfor + ":" + GlobalVarSelecter.Value + "#13000+GlobalVariableModule";
			}
			return FilterInfor;
		}
	}

	public string SetValue_Communication
	{
		get
		{
			if (CommDeviceSelecter != null && !string.IsNullOrWhiteSpace(CommunicationMgrHelper.CommunicationAddress))
			{
				if (deviceType == CommunicationType.None)
				{
					deviceType = commDevice.DeviceType;
				}
				return FilterInfor + ":" + commDevice?.CurValueA + "#" + CommunicationMgrHelper.CommunicationAddress + ":" + deviceType.ToString();
			}
			return FilterInfor;
		}
	}

	public Enumeration QueueSelecter
	{
		get
		{
			if (_QueueSelecter == null)
			{
				CreateQueueSelecterInstance();
			}
			return _QueueSelecter;
		}
	}

	public Enumeration QueueSelecterA
	{
		get
		{
			if (_QueueSelecter == null)
			{
				CreateQueueSelecterInstance();
			}
			if (outputType == GlobalModuleType.DataQueue)
			{
				DataQueue_SelectedChanged(DataQueue, null);
			}
			return _QueueSelecter;
		}
	}

	public EnumerationVar GlobalVarSelecter
	{
		get
		{
			if (_GlobalVarSelecter == null)
			{
				CreateGlobalVarSelecterInstance();
			}
			return _GlobalVarSelecter;
		}
	}

	public Enumeration GlobalVarSelecterA
	{
		get
		{
			if (_GlobalVarSelecter == null)
			{
				CreateGlobalVarSelecterInstance();
			}
			else
			{
				_GlobalVarSelecter.CurValue = GlobalIndex;
			}
			if (outputType == GlobalModuleType.GlobalVariable)
			{
				GetGlobalVarList();
			}
			return _GlobalVarSelecter;
		}
	}

	public Enumeration CommDeviceSelecter
	{
		get
		{
			if (_CommDeviceSelecter == null)
			{
				CreateCommDeviceSelecterInstance();
			}
			return _CommDeviceSelecter;
		}
	}

	public Enumeration CommDeviceSelecterA
	{
		get
		{
			if (_CommDeviceSelecter == null)
			{
				CreateCommDeviceSelecterInstance();
			}
			if (outputType == GlobalModuleType.Communication)
			{
				GetCommunicationDeviceList();
			}
			return _CommDeviceSelecter;
		}
	}

	public Enumeration PLCAddressSelecter
	{
		get
		{
			if (_PLCAddressSelecter == null)
			{
				CreatePLCAddressSelecterInstance();
			}
			return _PLCAddressSelecter;
		}
	}

	public Enumeration PLCAddressSelecterA
	{
		get
		{
			if (_PLCAddressSelecter == null)
			{
				CreatePLCAddressSelecterInstance();
			}
			if (outputType == GlobalModuleType.Communication && deviceType == CommunicationType.PLC)
			{
				GetPLCAddressList();
			}
			return _PLCAddressSelecter;
		}
	}

	public Enumeration ModbusAddressSelecter
	{
		get
		{
			if (_ModbusAddressSelecter == null)
			{
				CreateModbusAddressSelecterInstance();
			}
			return _ModbusAddressSelecter;
		}
	}

	public Enumeration ModbusAddressSelecterA
	{
		get
		{
			if (_ModbusAddressSelecter == null)
			{
				CreateModbusAddressSelecterInstance();
			}
			if (outputType == GlobalModuleType.Communication && deviceType == CommunicationType.Modbus)
			{
				GetModbusAddressList();
			}
			return _ModbusAddressSelecter;
		}
	}

	private VmModule ownerModule => (parentNode as DynamicIO).IoDataSource.Root;

	public ICommand SelectedChangedCommand_Queue
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_Queue == null)
			{
				_SelectedChangedCommand_Queue = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand_Queue;
		}
	}

	public ICommand SelectedChangedCommand_GlobalVar
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_GlobalVar == null)
			{
				_SelectedChangedCommand_GlobalVar = new DelegateCommand((Action)SelectedChangedCmdExecute_GlobalVar);
			}
			return (ICommand)_SelectedChangedCommand_GlobalVar;
		}
	}

	public ICommand SelectedChangedCommand_CommDevice
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_CommDevice == null)
			{
				_SelectedChangedCommand_CommDevice = new DelegateCommand((Action)SelectedChangedCmdExecute_CommDevice);
			}
			return (ICommand)_SelectedChangedCommand_CommDevice;
		}
	}

	public ICommand SelectedChangedCommand_PLCAddress
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_PLCAddress == null)
			{
				_SelectedChangedCommand_PLCAddress = new DelegateCommand((Action)SelectedChangedCmdExecute_PLCAddress);
			}
			return (ICommand)_SelectedChangedCommand_PLCAddress;
		}
	}

	public ICommand SelectedChangedCommand_ModbusAddress
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_ModbusAddress == null)
			{
				_SelectedChangedCommand_ModbusAddress = new DelegateCommand((Action)SelectedChangedCmdExecute_ModbusAddress);
			}
			return (ICommand)_SelectedChangedCommand_ModbusAddress;
		}
	}

	public event Func<int, bool> OutTypeChangeEventHandler = null;

	public ReadDataModuleIO()
	{
		outputType = GlobalModuleType.None;
		deviceType = CommunicationType.None;
		id = Guid.NewGuid().ToString("N");
	}

	public ReadDataModuleIO(EnumerationDataCenter enumerationDataCenter, EnumerationCommunication enumerationCommunication, RadioSelecterEx3 radioSelecter, bool isRegisterEvent)
	{
		Init(enumerationDataCenter, enumerationCommunication, radioSelecter, isRegisterEvent);
	}

	private void CreateQueueSelecterInstance()
	{
		if (parentNode is ReadDataDynamicIO { IsTypeChange: not false })
		{
			_QueueSelecter = new Enumeration
			{
				Name = "QueueItemList",
				AccessMode = "O",
				CurValue = ReadDataDynamicIO.SelectQueueIndex,
				DefaultValue = ReadDataDynamicIO.SelectQueueIndex,
				OldValue = ReadDataDynamicIO.SelectQueueIndex.ToString(),
				SelectedChangedCommand = SelectedChangedCommand_Queue
			};
		}
		else
		{
			_QueueSelecter = new Enumeration
			{
				Name = "QueueItemList",
				AccessMode = "O",
				CurValue = QueueIndex,
				DefaultValue = _queueIndex,
				OldValue = _queueIndex.ToString(),
				SelectedChangedCommand = SelectedChangedCommand_Queue
			};
		}
		_QueueSelecter.SetParentNode(this);
	}

	private void CreateGlobalVarSelecterInstance()
	{
		_GlobalVarSelecter = new EnumerationVar
		{
			Name = "GlobalVarItemList",
			AccessMode = "O",
			CurValue = GlobalIndex,
			DefaultValue = _globalIndex,
			OldValue = _globalIndex.ToString(),
			OldBindSourceID = VarModuleID,
			SelectedChangedCommand = SelectedChangedCommand_GlobalVar
		};
		_GlobalVarSelecter.SetParentNode(this);
		if (_GlobalVarSelecter.ReservedProperty == null)
		{
			if (VarModuleID == 13000)
			{
				_GlobalVarSelecter.ReservedProperty = VmModuleExtern.GetGlobalVarModule();
			}
			else
			{
				_GlobalVarSelecter.ReservedProperty = ownerModule.GetLocalVarModule();
			}
		}
	}

	private void CreateCommDeviceSelecterInstance()
	{
		_CommDeviceSelecter = new Enumeration
		{
			Name = "CommDeviceItemList",
			AccessMode = "O",
			CurValue = CommDeviceIndex,
			DefaultValue = _commDeviceIndex,
			OldValue = _commDeviceIndex.ToString(),
			SelectedChangedCommand = SelectedChangedCommand_CommDevice
		};
		_CommDeviceSelecter.SetParentNode(this);
	}

	private void CreatePLCAddressSelecterInstance()
	{
		_PLCAddressSelecter = new Enumeration
		{
			Name = "PLCAddressList",
			AccessMode = "O",
			CurValue = PLCAddressIndex,
			DefaultValue = _pLCAddressIndex,
			OldValue = _pLCAddressIndex.ToString(),
			SelectedChangedCommand = SelectedChangedCommand_PLCAddress
		};
		_PLCAddressSelecter.SetParentNode(this);
	}

	private void CreateModbusAddressSelecterInstance()
	{
		_ModbusAddressSelecter = new Enumeration
		{
			Name = "ModbusAddressList",
			AccessMode = "O",
			CurValue = ModbusAddressIndex,
			DefaultValue = _modbusAddressIndex,
			OldValue = _modbusAddressIndex.ToString(),
			SelectedChangedCommand = SelectedChangedCommand_ModbusAddress
		};
		_ModbusAddressSelecter.SetParentNode(this);
	}

	private void GetGlobalVarList()
	{
		IList slectGlobalVar = GlobalVarHelper.GetSlectGlobalVar(ownerModule);
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in slectGlobalVar)
		{
			item.OnItemClicked -= GlobalVar_OnItemClicked;
			item.OnItemClicked += GlobalVar_OnItemClicked;
			observableCollection.Add(item);
		}
		GlobalVarSelecter.EnumEntrysA = observableCollection;
		RefreshValueType(GlobalModuleType.GlobalVariable);
	}

	private void GlobalVar_OnItemClicked(object param)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected Obj, but got Unknown
		if (param == null)
		{
			return;
		}
		string text = param.ToString();
		if (text.Equals(AppLocalizationService.GetLang("Lang_VariableManager")))
		{
			GlobalVariableSelectDlg val = new GlobalVariableSelectDlg(true);
			IVmModule localVarModule = ownerModule.GetLocalVarModule();
			val.Type = new string[4] { "int", "float", "string", "IMAGE" };
			val.DecorateSource = localVarModule;
			((BaseWindow)(object)val).ShowDialog();
			if (val.IsConfirm && val.SelectedVarItem is VarItem varItem)
			{
				GlobalVarSelecter.Value = varItem.Index.ToString();
				GlobalVarSelecter.CurrentNameA = varItem.DisplayName;
				GlobalVarSelecter.ReservedProperty = varItem.Owner;
				if (varItem.Owner != null)
				{
					GlobalVarSelecter.BindSourceID = (varItem.Owner as IVmModule).ID;
				}
				GlobalVarSelecter.OnPropertyChanged("CurrentNameA");
			}
		}
		else
		{
			GlobalVarSelecter.Value = "0";
			GlobalVarSelecter.BindSourceID = 0u;
			GlobalVarSelecter.ReservedProperty = null;
			GlobalVarSelecter.CurrentNameA = AppLocalizationService.GetLang("Lang_PlzChoose");
		}
		if (GlobalVarSelecter.OldValue != GlobalVarSelecter.Value || GlobalVarSelecter.OldBindSourceID != GlobalVarSelecter.BindSourceID)
		{
			if (SetParamUnit() == 0)
			{
				GlobalVarSelecter.OldValue = GlobalVarSelecter.Value;
				GlobalVarSelecter.OldBindSourceID = GlobalVarSelecter.BindSourceID;
				RefreshValueType(GlobalModuleType.GlobalVariable);
				GlobalIndex = GlobalVarSelecter.CurValueA;
			}
			else
			{
				GlobalVarSelecter.Value = GlobalVarSelecter.OldValue;
			}
		}
	}

	private void GetCommunicationDeviceList()
	{
		string[] commTypes = new string[4] { "TCPClient", "TCPServer", "UDP", "Serial" };
		IList allCommunicationDevices = CommunicationMgrHelper.GetAllCommunicationDevices(isAllCommType: false, isAddDefaulEntry: false, commTypes);
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		if (allCommunicationDevices != null)
		{
			foreach (EnumEntry item in allCommunicationDevices)
			{
				observableCollection.Add(item);
			}
		}
		CommDeviceSelecter.EnumEntrysA = observableCollection;
		RefreshValueType(GlobalModuleType.Communication);
	}

	private void GetModbusAddressList()
	{
		if (CommDevice == null)
		{
			return;
		}
		IList addressList = CommDevice.AddressList;
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in addressList)
		{
			observableCollection.Add(item);
		}
		ModbusAddressSelecter.EnumEntrysA = observableCollection;
		if (commDevice.isDelete)
		{
			if (commDevice.NumA >= 0 && ModbusAddressSelecter.CurValueA > commDevice.NumA)
			{
				ModbusAddressSelecter.CurValueA = 0;
				SetParamUnit();
			}
		}
		else if (commDevice.NumA >= 0 && (ModbusAddressSelecter.CurValueA == commDevice.NumA + 1 || ModbusAddressSelecter.CurValueA == commDevice.NumA + 2))
		{
			ModbusAddressSelecter.CurValueA = 0;
			SetParamUnit();
		}
		commDevice.NumA = -1;
		commDevice.isDelete = false;
		RefreshValueType(GlobalModuleType.Communication);
	}

	private void GetPLCAddressList()
	{
		if (CommDevice == null)
		{
			return;
		}
		IList addressList = CommDevice.AddressList;
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in addressList)
		{
			observableCollection.Add(item);
		}
		PLCAddressSelecter.EnumEntrysA = observableCollection;
		if (commDevice.isDelete)
		{
			if (commDevice.NumA >= 0 && PLCAddressSelecter.CurValueA > commDevice.NumA)
			{
				PLCAddressSelecter.CurValueA = 0;
				SetParamUnit();
			}
		}
		else if (commDevice.NumA >= 0 && (PLCAddressSelecter.CurValueA == commDevice.NumA + 1 || PLCAddressSelecter.CurValueA == commDevice.NumA + 2))
		{
			PLCAddressSelecter.CurValueA = 0;
			SetParamUnit();
		}
		commDevice.NumA = -1;
		commDevice.isDelete = false;
		RefreshValueType(GlobalModuleType.Communication);
	}

	private void SetOutputType(RadioSelecterEx3 radioSelecter3, bool bInit = false)
	{
		if (radioSelecter3 == null)
		{
			outputType = GlobalModuleType.None;
			return;
		}
		if (radioSelecter3.CurValueA == 0)
		{
			outputType = GlobalModuleType.DataQueue;
		}
		else if (radioSelecter3.CurValueA == 2)
		{
			outputType = GlobalModuleType.GlobalVariable;
		}
		else if (radioSelecter3.CurValueA == 1)
		{
			outputType = GlobalModuleType.Communication;
		}
		else
		{
			outputType = GlobalModuleType.None;
		}
		if (radioSelecter3.OldTmpValue == 0 && !bInit)
		{
			bool? flag = OutTypeChangeEventHandler?.Invoke(0);
			if (flag.HasValue && flag.Value)
			{
				return;
			}
		}
		if (!bInit)
		{
			RefreshValueType(outputType, IsChangeFormOutType: true);
		}
	}

	private void RefreshValueType(GlobalModuleType globalModuleType, bool IsChangeFormOutType = false)
	{
		try
		{
			if (!(parentNode is DynamicIO dynamicIO))
			{
				return;
			}
			switch (globalModuleType)
			{
			case GlobalModuleType.Communication:
				dynamicIO.ValueTypeA = "string";
				if (deviceType == CommunicationType.PLC)
				{
					List<int> list = PLCAddressSelecter.EnumEntrysA?.Select((object p) => (p as EnumEntry).Value).ToList();
					if (!list.Contains(PLCAddressSelecter.CurValueA))
					{
						PLCAddressSelecter.CurValue = 0;
						if (SetParamUnit() == 0)
						{
							PLCAddressSelecter.OldValue = PLCAddressSelecter.Value;
							PLCAddressIndex = PLCAddressSelecter.CurValueA;
						}
					}
				}
				else if (deviceType == CommunicationType.Modbus)
				{
					if (ModbusAddressSelecter.CurValueA >= ModbusAddressSelecter.EnumEntrysA?.Count)
					{
						ModbusAddressSelecter.CurValue = 0;
						if (SetParamUnit() == 0)
						{
							ModbusAddressSelecter.OldValue = ModbusAddressSelecter.Value;
							ModbusAddressIndex = ModbusAddressSelecter.CurValueA;
						}
					}
				}
				else if (CommDeviceSelecter.CurValueA >= CommDeviceSelecter.EnumEntrysA?.Count)
				{
					CommDeviceSelecter.CurValue = 0;
					if (SetParamUnit() == 0)
					{
						CommDeviceSelecter.OldValue = CommDeviceSelecter.Value;
						CommDeviceIndex = CommDeviceSelecter.CurValueA;
					}
				}
				break;
			case GlobalModuleType.GlobalVariable:
			{
				if (GlobalVarSelecter.ReservedProperty == null || !(GlobalVarSelecter.ReservedProperty is VmModule vmModule))
				{
					break;
				}
				if (GlobalVarSelecter.CurValueA == 0)
				{
					GlobalVarSelecter.CurrentNameA = "";
					GlobalVarSelecter.OldValue = CommDeviceSelecter.Value;
					break;
				}
				if (GlobalVarSelecter.CurValueA > vmModule.Outputs.Count)
				{
					dynamicIO.ValueTypeA = "int";
					GlobalVarSelecter.CurValue = 0;
					GlobalVarSelecter.CurrentNameA = "";
					if (SetParamUnit() == 0)
					{
						GlobalVarSelecter.OldValue = GlobalVarSelecter.Value;
						GlobalIndex = GlobalVarSelecter.CurValueA;
					}
					break;
				}
				VmIO vmIO = vmModule.Outputs.FirstOrDefault((VmIO p) => p.Index == GlobalVarSelecter.CurValueA);
				if (vmIO == null && GlobalVarSelecter.CurValueA > 0)
				{
					GlobalVarHelper.InitGlobalVarOutputIO(vmModule);
					vmIO = vmModule.Outputs.FirstOrDefault((VmIO p) => p.Index == GlobalVarSelecter.CurValueA);
				}
				if (vmIO != null)
				{
					VarItem varItem = new VarItem
					{
						Owner = vmModule,
						Name = vmIO.Name
					};
					dynamicIO.ValueTypeA = vmIO.TypeName;
					GlobalVarSelecter.CurrentNameA = varItem.DisplayName;
				}
				break;
			}
			case GlobalModuleType.DataQueue:
			{
				if (QueueSelecter.CurValueA == 0)
				{
					dynamicIO.ValueTypeA = "int";
					if (QueueSelecter.EnumEntrysA == null)
					{
						OnPropertyChanged("QueueSelecterA");
					}
					break;
				}
				if (QueueSelecter.CurValueA >= QueueSelecter.EnumEntrysA.Count)
				{
					dynamicIO.ValueTypeA = "int";
					QueueSelecter.CurValue = 0;
					if (SetParamUnit() == 0)
					{
						QueueSelecter.OldValue = QueueSelecter.Value;
						QueueIndex = QueueSelecter.CurValueA;
					}
					break;
				}
				object obj = QueueSelecter.EnumEntrysA.FirstOrDefault((object p) => (p as EnumEntry).ValueA == QueueSelecter.CurValueA && (p as EnumEntry).ValueA > 0);
				if (obj == null || !(obj is EnumEntry))
				{
					break;
				}
				if (((obj as EnumEntry).ReservedProperty.ToString() == "IMAGE") & IsChangeFormOutType)
				{
					dynamicIO.ValueTypeA = "int";
					QueueSelecter.CurValueA = 0;
					if (SetParamUnit() == 0)
					{
						QueueSelecter.OldValue = QueueSelecter.Value;
						QueueIndex = QueueSelecter.CurValueA;
					}
				}
				else
				{
					ReadDataDynamicIO.SelectQueueIndex = QueueSelecter.CurValueA;
					dynamicIO.ValueTypeA = (obj as EnumEntry).ReservedProperty.ToString();
				}
				break;
			}
			case (GlobalModuleType)3:
				break;
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("RefreshValueType is error:" + ex.ToString());
		}
	}

	private void GetCommDeviceType(int devIndex)
	{
		if (CommDeviceSelecter == null || devIndex <= 0)
		{
			deviceType = CommunicationType.None;
		}
		object obj = CommDeviceSelecter.EnumEntrysA.FirstOrDefault((object p) => (p as EnumEntry).ValueA == devIndex);
		if (obj != null && obj is EnumEntry)
		{
			deviceType = (CommunicationType)Enum.Parse(typeof(CommunicationType), (obj as EnumEntry).Name);
		}
		else
		{
			deviceType = CommunicationType.None;
		}
	}

	private void DataQueue_SelectedChanged(object sender, EventArgs e)
	{
		if (!(sender is EnumerationDataCenter { QueueList: var queueList }))
		{
			return;
		}
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in queueList)
		{
			observableCollection.Add(item);
		}
		QueueSelecter.EnumEntrysA = observableCollection;
		if (e != null)
		{
			QueueSelecter.CurValueA = 0;
		}
		RefreshValueType(GlobalModuleType.DataQueue);
	}

	private void CommunicationDevice_SelectedChanged(object sender, EventArgs e)
	{
		if (!(sender is EnumerationCommunication enumerationCommunication))
		{
			return;
		}
		deviceType = enumerationCommunication.DeviceType;
		if (deviceType != CommunicationType.PLC && deviceType != CommunicationType.Modbus)
		{
			return;
		}
		IList addressList = enumerationCommunication.AddressList;
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in addressList)
		{
			observableCollection.Add(item);
		}
		if (deviceType == CommunicationType.PLC)
		{
			PLCAddressSelecter.EnumEntrysA = observableCollection;
			if (e != null)
			{
				PLCAddressSelecter.CurValueA = 0;
			}
		}
		else
		{
			ModbusAddressSelecter.EnumEntrysA = observableCollection;
			if (e != null)
			{
				ModbusAddressSelecter.CurValueA = 0;
			}
		}
		RefreshValueType(GlobalModuleType.Communication);
	}

	private void RadioSelecterEx3_CurValueChanged(object sender, EventArgs e)
	{
		if (sender is RadioSelecterEx3 radioSelecter)
		{
			SetOutputType(radioSelecter);
		}
	}

	public void Init(EnumerationDataCenter enumDataCenter, EnumerationCommunication enumCommunication, RadioSelecterEx3 radSelecter, bool isRegisterEvent)
	{
		dataQueue = enumDataCenter;
		commDevice = enumCommunication;
		outputTypeSelecter = radSelecter;
		SetOutputType(radSelecter, bInit: true);
		isOldVersion = true;
		if (commDevice != null)
		{
			deviceType = enumCommunication.DeviceType;
			isOldVersion = false;
		}
		if (isRegisterEvent)
		{
			if (dataQueue != null && !flagRegisterEvent_Queue)
			{
				dataQueue.OnSelectedChanged += DataQueue_SelectedChanged;
				flagRegisterEvent_Queue = true;
			}
			if (outputTypeSelecter != null && !flagRegisterEvent_OutType)
			{
				outputTypeSelecter.OnCurValueChanged += RadioSelecterEx3_CurValueChanged;
				flagRegisterEvent_OutType = true;
			}
			if (commDevice != null && !flagRegisterEvent_CommDevice)
			{
				commDevice.OnSelectedChanged += CommunicationDevice_SelectedChanged;
				flagRegisterEvent_CommDevice = true;
			}
		}
	}

	public void Dispose()
	{
		if (dataQueue != null && flagRegisterEvent_Queue)
		{
			dataQueue.OnSelectedChanged -= DataQueue_SelectedChanged;
			flagRegisterEvent_Queue = false;
		}
		if (outputTypeSelecter != null && flagRegisterEvent_OutType)
		{
			outputTypeSelecter.OnCurValueChanged -= RadioSelecterEx3_CurValueChanged;
			flagRegisterEvent_OutType = false;
		}
		if (commDevice != null && flagRegisterEvent_CommDevice)
		{
			commDevice.OnSelectedChanged -= CommunicationDevice_SelectedChanged;
			flagRegisterEvent_CommDevice = false;
		}
	}

	public void RefreshDataSource(GlobalModuleType globalModuleType, int deleIndex = -1)
	{
		switch (globalModuleType)
		{
		case GlobalModuleType.Communication:
			if (deviceType == CommunicationType.PLC)
			{
				GetPLCAddressList();
			}
			else if (deviceType == CommunicationType.Modbus)
			{
				GetModbusAddressList();
			}
			else
			{
				GetCommunicationDeviceList();
			}
			break;
		case GlobalModuleType.GlobalVariable:
			if (deleIndex > -1)
			{
				RefreshGlobalVarByDelete(deleIndex);
			}
			else
			{
				GetGlobalVarList();
			}
			break;
		case GlobalModuleType.DataQueue:
			DataQueue_SelectedChanged(DataQueue, null);
			break;
		case (GlobalModuleType)3:
			break;
		}
	}

	public void RefreshGlobalVarByDelete(int nDeleteGlobalvarIndex)
	{
		IList slectGlobalVar = GlobalVarHelper.GetSlectGlobalVar(ownerModule);
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in slectGlobalVar)
		{
			item.OnItemClicked -= GlobalVar_OnItemClicked;
			item.OnItemClicked += GlobalVar_OnItemClicked;
			observableCollection.Add(item);
		}
		GlobalVarSelecter.EnumEntrysA = observableCollection;
		if (!(parentNode is Filter filter))
		{
			return;
		}
		if (GlobalVarSelecter.CurValueA == 0)
		{
			filter.ValueType = "int";
		}
		else if (GlobalVarSelecter.CurValueA > nDeleteGlobalvarIndex)
		{
			GlobalVarSelecter.CurValueA -= 1;
			VmModule vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 13000);
			VmIO vmIO = vmModule.Outputs.FirstOrDefault((VmIO p) => p.Index == GlobalVarSelecter.CurValueA);
			if (vmIO != null)
			{
				filter.ValueType = vmIO.TypeName;
				GlobalVarSelecter.CurrentNameA = vmIO.Name;
			}
			if (GlobalVarSelecter.OldValue != GlobalVarSelecter.Value && SetParamUnit() == 0)
			{
				GlobalVarSelecter.OldValue = GlobalVarSelecter.Value;
				GlobalIndex = GlobalVarSelecter.CurValueA;
			}
		}
		else if (GlobalVarSelecter.CurValueA == nDeleteGlobalvarIndex)
		{
			filter.ValueType = "int";
			GlobalVarSelecter.CurValue = 0;
			GlobalVarSelecter.CurrentNameA = "";
			if (SetParamUnit() == 0)
			{
				GlobalVarSelecter.OldValue = GlobalVarSelecter.Value;
				GlobalIndex = GlobalVarSelecter.CurValueA;
			}
		}
	}

	public void SetCommDeviceIndex(int commDeviceIndex)
	{
		if (CommDevice != null)
		{
			CommDevice.CurValue = commDeviceIndex;
			CommDeviceIndex = commDeviceIndex;
		}
	}

	public void SetQueueIndex(int queueIndex)
	{
		if (QueueSelecter != null)
		{
			QueueSelecter.CurValue = queueIndex;
			QueueIndex = queueIndex;
		}
	}

	public void SetGlobalVarIndex(int globalIndex)
	{
		if (GlobalVarSelecter != null)
		{
			GlobalVarSelecter.CurValue = globalIndex;
			GlobalIndex = globalIndex;
		}
	}

	public int SetParamUnit()
	{
		if (!(parentNode is DynamicIO dynamicIO))
		{
			return -3;
		}
		if (dynamicIO.IoDataSource == null)
		{
			return -3;
		}
		string text = "";
		switch (outputType)
		{
		case GlobalModuleType.Communication:
		{
			int num = 1;
			if (deviceType == CommunicationType.PLC)
			{
				num = 11 + PLCAddressSelecter.CurValueA;
			}
			else if (deviceType == CommunicationType.Modbus)
			{
				num = 27 + ModbusAddressSelecter.CurValueA;
			}
			text = num + ":" + SetValue_Communication;
			break;
		}
		case GlobalModuleType.GlobalVariable:
			text = 2 + ":" + SetValue_GlobalVar;
			break;
		case GlobalModuleType.DataQueue:
			text = 0 + ":" + SetValue_QueueCenter;
			break;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return -2;
		}
		try
		{
			dynamicIO.IoDataSource.Root.Params["OutputFilter"] = text;
		}
		catch (Exception)
		{
			return -1;
		}
		switch (outputType)
		{
		case GlobalModuleType.Communication:
			OldSetValue_Communication = text;
			break;
		case GlobalModuleType.GlobalVariable:
			OldSetValue_GlobalVar = text;
			break;
		case GlobalModuleType.DataQueue:
			OldSetValue_QueueCenter = text;
			break;
		}
		return 0;
	}

	public void UpdateUIParams(string newName = null)
	{
		if (newName == null)
		{
			QueueIndex = 0;
			GlobalIndex = 0;
			CommDeviceIndex = 0;
			PLCAddressIndex = 0;
			ModbusAddressIndex = 0;
			return;
		}
		switch (outputType)
		{
		case GlobalModuleType.Communication:
			if (deviceType == CommunicationType.PLC)
			{
				SetUIParams("PLCAddressIndex", "0", tmpOldName);
				SetUIParams("PLCAddressIndex", _pLCAddressIndex.ToString(), newName);
			}
			else if (deviceType == CommunicationType.Modbus)
			{
				SetUIParams("ModbusAddressIndex", "0", tmpOldName);
				SetUIParams("ModbusAddressIndex", _modbusAddressIndex.ToString(), newName);
			}
			else
			{
				SetUIParams("CommDeviceIndex", "0", tmpOldName);
				SetUIParams("CommDeviceIndex", _commDeviceIndex.ToString(), newName);
			}
			break;
		case GlobalModuleType.GlobalVariable:
			SetUIParams("GlobalIndex", "0", tmpOldName);
			SetUIParams("GlobalIndex", _globalIndex.ToString(), newName);
			break;
		case GlobalModuleType.DataQueue:
			SetUIParams("QueueIndex", "0", tmpOldName);
			SetUIParams("QueueIndex", _oldIndex.ToString(), newName);
			break;
		}
		tmpOldName = newName;
		RecordGlobalQueueIndex();
	}

	public void RecordGlobalQueueIndex()
	{
		string text = "";
		if (!(parentNode is DynamicIO dynamicIO))
		{
			return;
		}
		ReadDatasModuleDynamicIO readDatasModuleDynamicIO = dynamicIO.GetParentNode() as ReadDatasModuleDynamicIO;
		switch (OutputType)
		{
		case GlobalModuleType.GlobalVariable:
			foreach (ReadDataDynamicIO item in readDatasModuleDynamicIO.ItemsA)
			{
				if (item.CustomVisibleA)
				{
					text = text + item.ReadDataModuleFilterA?.GlobalVarIndexUserName + "\n";
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				dynamicIO.IoDataSource.Root.UserData["GlobalVarRecord"] = StringUtil.UTF8GetBytesPadZero(text);
			}
			break;
		case GlobalModuleType.DataQueue:
			foreach (ReadDataDynamicIO item2 in readDatasModuleDynamicIO.ItemsA)
			{
				if (item2.CustomVisibleA)
				{
					text = text + item2.ReadDataModuleFilterA?.QueueIndexUserName + "\n";
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				dynamicIO.IoDataSource.Root.UserData["DataQueueRecord"] = StringUtil.UTF8GetBytesPadZero(text);
			}
			break;
		case GlobalModuleType.Communication:
		{
			string key = "";
			foreach (ReadDataDynamicIO item3 in readDatasModuleDynamicIO.ItemsA)
			{
				if (item3.CustomVisibleA)
				{
					string text2 = "";
					if (deviceType == CommunicationType.PLC)
					{
						key = "PlcItemRecord";
						text2 = item3.ReadDataModuleFilterA?.PLCIndexUserName;
					}
					else if (deviceType == CommunicationType.Modbus)
					{
						key = "ModbusItemRecord";
						text2 = item3.ReadDataModuleFilterA?.ModbusIndexUserName;
					}
					if (!string.IsNullOrEmpty(text2))
					{
						text = text + text2 + "\n";
					}
				}
			}
			if (!string.IsNullOrEmpty(text) && dynamicIO.IoDataSource != null && dynamicIO.IoDataSource.Root != null)
			{
				dynamicIO.IoDataSource.Root.UserData[key] = StringUtil.UTF8GetBytesPadZero(text);
			}
			break;
		}
		case (GlobalModuleType)3:
			break;
		}
	}

	public void SetOldIndex()
	{
		_oldIndex = QueueIndex;
	}

	public void SetVarUIParams(string key, string value, string ioname = null)
	{
		if (parentNode is DynamicIO dynamicIO)
		{
			if (string.IsNullOrEmpty(ioname))
			{
				ioname = IoName;
			}
			uint num = 13000u;
			if (GlobalVarSelecter.ReservedProperty != null)
			{
				num = (GlobalVarSelecter.ReservedProperty as VmModule).ID;
			}
			dynamicIO.IoDataSource.Root.UserData[$"{ioname}:{key}"] = StringUtil.UTF8GetBytesPadZero(num + ":" + value);
		}
	}

	public void GetVarUIParams(string key, ref uint moduleid, ref int value)
	{
		if (!(parentNode is DynamicIO dynamicIO))
		{
			return;
		}
		byte[] array = dynamicIO.IoDataSource.Root.UserData[IoName + ":" + key];
		if (array == null || array.Length == 0)
		{
			return;
		}
		string text = StringUtil.UTF8GetString(array);
		if (text.Contains(":"))
		{
			string[] array2 = text.Split(':');
			if (array2.Length >= 2)
			{
				uint.TryParse(array2[0], out moduleid);
				int.TryParse(array2[1], out value);
			}
		}
		else
		{
			int.TryParse(text, out value);
			moduleid = 13000u;
		}
	}

	public void SetUIParams(string key, string value, string ioname = null)
	{
		if (parentNode is DynamicIO dynamicIO)
		{
			if (string.IsNullOrEmpty(ioname))
			{
				ioname = IoName;
			}
			dynamicIO.IoDataSource.Root.UserData[$"{ioname}:{key}"] = StringUtil.UTF8GetBytesPadZero(value);
		}
	}

	public void GetUIParams(string key, ref int value)
	{
		int result = -1;
		if (int.TryParse(GetUIParams(key), out result))
		{
			value = result;
		}
	}

	public string GetUIParams(string key)
	{
		if (parentNode is DynamicIO dynamicIO)
		{
			byte[] array = dynamicIO.IoDataSource.Root.UserData[IoName + ":" + key];
			if (array != null && array.Length != 0)
			{
				return StringUtil.UTF8GetString(array);
			}
		}
		return null;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (this == obj)
		{
			return true;
		}
		if (obj.GetType() != GetType())
		{
			return false;
		}
		return Equals((ReadDataModuleIO)obj);
	}

	public override int GetHashCode()
	{
		return (id != null) ? StringComparer.InvariantCulture.GetHashCode(id) : 0;
	}

	public bool Equals(ReadDataModuleIO other)
	{
		if (null == other)
		{
			return false;
		}
		if ((object)this == other)
		{
			return true;
		}
		if (!string.Equals(id, other.id, StringComparison.InvariantCulture))
		{
			return false;
		}
		return true;
	}

	public static bool operator ==(ReadDataModuleIO left, ReadDataModuleIO right)
	{
		return object.Equals(left, right);
	}

	public static bool operator !=(ReadDataModuleIO left, ReadDataModuleIO right)
	{
		return !object.Equals(left, right);
	}

	private void SelectedChangedCmdExecute()
	{
		if (QueueSelecter.OldValue != QueueSelecter.Value)
		{
			if (SetParamUnit() == 0)
			{
				QueueSelecter.OldValue = QueueSelecter.Value;
				RefreshValueType(GlobalModuleType.DataQueue);
				QueueIndex = QueueSelecter.CurValueA;
			}
			else
			{
				QueueSelecter.Value = QueueSelecter.OldValue;
			}
		}
	}

	private void SelectedChangedCmdExecute_GlobalVar()
	{
		if (GlobalVarSelecter.OldValue != GlobalVarSelecter.Value)
		{
			if (SetParamUnit() == 0)
			{
				GlobalVarSelecter.OldValue = GlobalVarSelecter.Value;
				RefreshValueType(GlobalModuleType.GlobalVariable);
				GlobalIndex = GlobalVarSelecter.CurValueA;
			}
			else
			{
				GlobalVarSelecter.Value = GlobalVarSelecter.OldValue;
			}
		}
	}

	private void SelectedChangedCmdExecute_CommDevice()
	{
		if (CommDeviceSelecter.OldValue != CommDeviceSelecter.Value)
		{
			GetCommDeviceType(CommDeviceSelecter.CurValueA);
			if (SetParamUnit() == 0)
			{
				CommDeviceSelecter.OldValue = CommDeviceSelecter.Value;
				CommDeviceIndex = CommDeviceSelecter.CurValueA;
				RefreshValueType(GlobalModuleType.Communication);
			}
			else
			{
				CommDeviceSelecter.Value = CommDeviceSelecter.OldValue;
			}
		}
	}

	private void SelectedChangedCmdExecute_PLCAddress()
	{
		if (PLCAddressSelecter.OldValue != PLCAddressSelecter.Value)
		{
			if (SetParamUnit() == 0)
			{
				PLCAddressSelecter.OldValue = PLCAddressSelecter.Value;
				PLCAddressIndex = PLCAddressSelecter.CurValueA;
				RefreshValueType(GlobalModuleType.Communication);
			}
			else
			{
				PLCAddressSelecter.Value = PLCAddressSelecter.OldValue;
			}
		}
		else if (PLCAddressSelecter.OldValue == "0" && PLCAddressSelecter.EnumEntrysA != null)
		{
			PLCAddressSelecter.CurValueA = PLCAddressSelecter.EnumEntrysA.Count - 1;
			PLCAddressSelecter.CurValueA = 0;
		}
	}

	private void SelectedChangedCmdExecute_ModbusAddress()
	{
		if (ModbusAddressSelecter.OldValue != ModbusAddressSelecter.Value)
		{
			if (SetParamUnit() == 0)
			{
				ModbusAddressSelecter.OldValue = ModbusAddressSelecter.Value;
				ModbusAddressIndex = ModbusAddressSelecter.CurValueA;
				RefreshValueType(GlobalModuleType.Communication);
			}
			else
			{
				ModbusAddressSelecter.Value = ModbusAddressSelecter.OldValue;
			}
		}
		else if (ModbusAddressSelecter.OldValue == "0" && ModbusAddressSelecter.EnumEntrysA != null)
		{
			ModbusAddressSelecter.CurValueA = ModbusAddressSelecter.EnumEntrysA.Count - 1;
			ModbusAddressSelecter.CurValueA = 0;
		}
	}

	public void GetCommAddress()
	{
		int curValueA = PLCAddressSelecter.CurValueA;
		bool flag = true;
		GetPLCAddressList();
		foreach (object item in PLCAddressSelecter.EnumEntrysA)
		{
			if ((item as EnumEntry).Value == curValueA)
			{
				flag = false;
				break;
			}
		}
		if (flag || curValueA < 0)
		{
			PLCAddressSelecter.CurValueA = 0;
		}
		CommDevice.OnPropertyChanged("AddressList");
		PLCAddressSelecter.OnPropertyChanged("CurValueA");
	}
}
