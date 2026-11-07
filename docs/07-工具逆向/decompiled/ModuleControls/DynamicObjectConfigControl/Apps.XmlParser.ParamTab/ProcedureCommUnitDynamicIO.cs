using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Data;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using GateWayServer;
using Mvvm.Commands;
using VM.Core;
using VMControls.WPF;

namespace Apps.XmlParser.ParamTab;

public class ProcedureCommUnitDynamicIO : DynamicIOContainer<ProcedureCommItemDynamicIO>
{
	private bool isRegisterCom = false;

	public string oldIOControl = "";

	private VmModule procedureComm;

	private int _ID;

	private string _DisplayIndex = "1";

	private bool _DelBtnVisible = true;

	private Integer _SetInterval;

	private EnumerationAllCom _CommDeviceSelecterA;

	public Enumeration AddressItemList;

	private bool _PLCVisibleA;

	private bool _IOVisibleA;

	private Enumeration _IOItemOKSendListA;

	private Enumeration _IOItemNGSendListA;

	private Enumeration _IOItemTimeoutSendListA;

	private ButtonSelecter _FilterSelecterIfJudge;

	public ButtonSelecter _FilterSelecterOKInput;

	public ButtonSelecter _FilterSelecterNGInput;

	public ButtonSelecter _FilterSelecterTimeoutSend;

	private DelegateCommand _SetIntervalChangedCommand = null;

	private DelegateCommand _SelectDeviceChangedCommand;

	private DelegateCommand _SelectedChangedCommand = null;

	private DelegateCommand clickCommand = null;

	private ICommand _SelectedChangedCommand_OKSend = null;

	private ICommand _SelectedChangedCommand_NGSend = null;

	private ICommand _SelectedChangedCommand_TimeoutSend = null;

	private DelegateCommand<object> deleteIOCommand = null;

	public VmModule ProcedureComm
	{
		get
		{
			if (procedureComm == null)
			{
				procedureComm = ((parentNode as IOwner).Owner as VmModuleSet).ProcedureCommunicaitonModule as VmModule;
			}
			return procedureComm;
		}
	}

	public int ID
	{
		get
		{
			if (_ID == 0)
			{
				string[] array = ProcedureComm?.Params["AllItemsId"].TrimEnd(';').Split(';');
				if (array.Length != 0 && !string.IsNullOrEmpty(array[0]))
				{
					List<string> source = array.OrderByDescending((string x) => int.Parse(x)).ToList();
					if (string.IsNullOrEmpty(source.FirstOrDefault()))
					{
						_ID = 1;
					}
					else
					{
						_ID = int.Parse(source.FirstOrDefault()) + 1;
					}
				}
				else
				{
					_ID = 1;
				}
			}
			return _ID;
		}
		set
		{
			if (_ID != value)
			{
				_ID = value;
			}
		}
	}

	public string DisplayIndex
	{
		get
		{
			return _DisplayIndex;
		}
		set
		{
			_DisplayIndex = value;
			OnPropertyChanged("DisplayIndex");
		}
	}

	public bool DelBtnVisible
	{
		get
		{
			return _DelBtnVisible;
		}
		set
		{
			_DelBtnVisible = value;
			OnPropertyChanged("DelBtnVisible");
		}
	}

	public Integer SetInterval
	{
		get
		{
			if (_SetInterval == null)
			{
				_SetInterval = new Integer
				{
					DefaultValue = 100,
					MinValue = 0,
					MaxValue = 10000,
					CurValue = 100,
					SubscribeVisibleA = false,
					ValueChangedCommand = SetIntervalChangedCommand
				};
			}
			return _SetInterval;
		}
	}

	public EnumerationAllCom CommDeviceSelecterA
	{
		get
		{
			if (_CommDeviceSelecterA == null)
			{
				CreateCommDeviceSelecterInstance();
			}
			return _CommDeviceSelecterA;
		}
		set
		{
			_CommDeviceSelecterA = value;
			OnPropertyChanged("CommDeviceSelecterA");
		}
	}

	public Enumeration AddressItemListA
	{
		get
		{
			if (AddressItemList == null)
			{
				AddressItemList = new Enumeration();
				AddressItemList.Name = "AddressItemList";
				AddressItemList.AccessMode = "RW";
				AddressItemList.CurValue = 0;
				AddressItemList.DefaultValue = 0;
				AddressItemList.SetParentNode(this);
			}
			AddressItemList.SelectedChangedCommand = SelectedChangedCommand;
			CommDevice_SelectedChanged(null, null);
			return AddressItemList;
		}
		set
		{
			AddressItemList = value;
			OnPropertyChanged("AddressItemListA");
		}
	}

	public bool PLCVisibleA
	{
		get
		{
			return _PLCVisibleA;
		}
		set
		{
			if (_PLCVisibleA != value)
			{
				_PLCVisibleA = value;
				OnPropertyChanged("PLCVisibleA");
			}
		}
	}

	public bool IOVisibleA
	{
		get
		{
			return _IOVisibleA;
		}
		set
		{
			if (_IOVisibleA != value)
			{
				_IOVisibleA = value;
				OnPropertyChanged("IOVisibleA");
			}
		}
	}

	public Enumeration IOItemOKSendListA
	{
		get
		{
			if (_IOItemOKSendListA == null)
			{
				_IOItemOKSendListA = new Enumeration();
				_IOItemOKSendListA.Name = "IOItemOKSendListA";
				_IOItemOKSendListA.AccessMode = "O";
				_IOItemOKSendListA.CurValue = 0;
				_IOItemOKSendListA.DefaultValue = 0;
				_IOItemOKSendListA.SelectedChangedCommand = SelectedChangedCommand_OKSend;
				_IOItemOKSendListA.SetParentNode(this);
			}
			return _IOItemOKSendListA;
		}
		set
		{
			_IOItemOKSendListA = value;
			OnPropertyChanged("IOItemOKSendListA");
		}
	}

	public Enumeration IOItemNGSendListA
	{
		get
		{
			if (_IOItemNGSendListA == null)
			{
				_IOItemNGSendListA = new Enumeration();
				_IOItemNGSendListA.Name = "IOItemNGSendListA";
				_IOItemNGSendListA.AccessMode = "O";
				_IOItemNGSendListA.CurValue = 0;
				_IOItemNGSendListA.DefaultValue = 0;
				_IOItemNGSendListA.SelectedChangedCommand = SelectedChangedCommand_NGSend;
				_IOItemNGSendListA.SetParentNode(this);
			}
			return _IOItemNGSendListA;
		}
		set
		{
			_IOItemNGSendListA = value;
			OnPropertyChanged("IOItemNGSendListA");
		}
	}

	public Enumeration IOItemTimeoutSendListA
	{
		get
		{
			if (_IOItemTimeoutSendListA == null)
			{
				_IOItemTimeoutSendListA = new Enumeration();
				_IOItemTimeoutSendListA.Name = "IOItemTimeoutSendListA";
				_IOItemTimeoutSendListA.AccessMode = "O";
				_IOItemTimeoutSendListA.CurValue = 0;
				_IOItemTimeoutSendListA.DefaultValue = 0;
				_IOItemTimeoutSendListA.SelectedChangedCommand = SelectedChangedCommand_TimeoutSend;
				_IOItemTimeoutSendListA.SetParentNode(this);
			}
			return _IOItemTimeoutSendListA;
		}
		set
		{
			_IOItemTimeoutSendListA = value;
			OnPropertyChanged("IOItemTimeoutSendListA");
		}
	}

	public ButtonSelecter FilterSelecterIfJudge
	{
		get
		{
			if (_FilterSelecterIfJudge == null)
			{
				_FilterSelecterIfJudge = (ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%condition_" + ID + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
			}
			return _FilterSelecterIfJudge;
		}
		set
		{
			if (value != _FilterSelecterIfJudge)
			{
				_FilterSelecterIfJudge = value;
			}
		}
	}

	public ButtonSelecter FilterSelecterOKInput
	{
		get
		{
			if (_FilterSelecterOKInput == null)
			{
				_FilterSelecterOKInput = (ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%ok_send_" + ID + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
			}
			return _FilterSelecterOKInput;
		}
		set
		{
			if (value != _FilterSelecterOKInput)
			{
				_FilterSelecterOKInput = value;
			}
		}
	}

	public ButtonSelecter FilterSelecterNGInput
	{
		get
		{
			if (_FilterSelecterNGInput == null)
			{
				_FilterSelecterNGInput = (ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%ng_send_" + ID + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
			}
			return _FilterSelecterNGInput;
		}
		set
		{
			if (value != _FilterSelecterNGInput)
			{
				_FilterSelecterNGInput = value;
			}
		}
	}

	public ButtonSelecter FilterSelecterTimeoutSend
	{
		get
		{
			if (_FilterSelecterTimeoutSend == null)
			{
				_FilterSelecterTimeoutSend = (ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%timeout_send_" + ID + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
			}
			return _FilterSelecterTimeoutSend;
		}
		set
		{
			if (value != _FilterSelecterTimeoutSend)
			{
				_FilterSelecterTimeoutSend = value;
			}
		}
	}

	public ICommand SetIntervalChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SetIntervalChangedCommand == null)
			{
				_SetIntervalChangedCommand = new DelegateCommand((Action)SetIntervalChangedCommandExecute);
			}
			return (ICommand)_SetIntervalChangedCommand;
		}
	}

	public ICommand SelectedChangedCommand_CommDevice
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectDeviceChangedCommand == null)
			{
				_SelectDeviceChangedCommand = new DelegateCommand((Action)SelectDeviceChangedCommandExecute);
			}
			return (ICommand)_SelectDeviceChangedCommand;
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
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = clickCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ClickCommandExecute);
				DelegateCommand val3 = val2;
				clickCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand SelectedChangedCommand_OKSend
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_OKSend == null)
			{
				_SelectedChangedCommand_OKSend = (ICommand)new DelegateCommand((Action)SelectedChangedCmdExecute_OKSend);
			}
			return _SelectedChangedCommand_OKSend;
		}
	}

	public ICommand SelectedChangedCommand_NGSend
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_NGSend == null)
			{
				_SelectedChangedCommand_NGSend = (ICommand)new DelegateCommand((Action)SelectedChangedCmdExecute_NGSend);
			}
			return _SelectedChangedCommand_NGSend;
		}
	}

	public ICommand SelectedChangedCommand_TimeoutSend
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand_TimeoutSend == null)
			{
				_SelectedChangedCommand_TimeoutSend = (ICommand)new DelegateCommand((Action)SelectedChangedCmdExecute_TimeoutSend);
			}
			return _SelectedChangedCommand_TimeoutSend;
		}
	}

	public ICommand DeleteCommand => (ICommand)(deleteIOCommand ?? (deleteIOCommand = new DelegateCommand<object>((Action<object>)DeleteIOCommandExecute)));

	public ProcedureCommUnitDynamicIO()
	{
		FunctionManager.Register("CommSet", ToolControl.CommSet);
		FunctionCameraManager.Register("GetComm", GetComm);
	}

	private void GetComm()
	{
		ProcedureCommDynamicIO procedureCommDynamicIO = parentNode as ProcedureCommDynamicIO;
		foreach (ProcedureCommUnitDynamicIO item in procedureCommDynamicIO.ObservableCollectionA)
		{
			int curValueA = item.CommDeviceSelecterA.CurValueA;
			bool flag = true;
			ObservableCollection<object> allCommunicationDevices = EnumerationAllCom.GetAllCommunicationDevices();
			foreach (object item2 in allCommunicationDevices)
			{
				if ((item2 as EnumEntry).Value == curValueA)
				{
					flag = false;
					break;
				}
			}
			item.isRegisterCom = true;
			if (flag || curValueA < 0)
			{
				item.CommDeviceSelecterA.CurValueA = 0;
			}
			item.CommDeviceSelecterA.EnumEntrysA = allCommunicationDevices;
			item.CommDeviceSelecterA.CurValueA = -1;
			item.CommDeviceSelecterA.CurValueA = curValueA;
			item.isRegisterCom = false;
		}
	}

	public ProcedureCommUnitDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	private void SetIntervalChangedCommandExecute()
	{
		if (ProcedureComm != null)
		{
			ProcedureComm.Params["SelectItem"] = ID.ToString();
			ProcedureComm.Params["Interval"] = _SetInterval.CurValueA.ToString();
		}
	}

	public void SelectDeviceChangedCommandExecute()
	{
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (CommDeviceSelecterA == null || ProcedureComm == null)
			{
				return;
			}
			if (CommDeviceSelecterA.CurValue == 0)
			{
				ProcedureComm.Params["SelectItem"] = _ID.ToString();
				ProcedureComm.Params["TargetDevice"] = "11000#-1#-1";
				IOItemOKSendListA.CurValueA = 0;
				IOItemNGSendListA.CurValueA = 0;
				IOItemTimeoutSendListA.CurValueA = 0;
				PLCVisibleA = false;
				IOVisibleA = false;
				return;
			}
			EnumEntry enumEntry = CommDeviceSelecterA.EnumEntrysA.FirstOrDefault((object x) => (x as EnumEntry).Value == CommDeviceSelecterA.CurValue) as EnumEntry;
			object obj = enumEntry?.ReservedProperty;
			if (obj == null)
			{
				LogHelper.Error("Selected Device is error");
				return;
			}
			string[] array = obj.ToString().Split('#');
			if (array.LastOrDefault() != null && array.LastOrDefault().Contains("0123456789ABCDEF"))
			{
				if (string.IsNullOrEmpty(oldIOControl) || (!string.IsNullOrEmpty(oldIOControl) && oldIOControl != enumEntry.FullName))
				{
					IOItemOKSendListA = null;
					IOItemNGSendListA = null;
					IOItemTimeoutSendListA = null;
					ObservableCollection<object> observableCollection = new ObservableCollection<object>();
					observableCollection.Add(new EnumEntry
					{
						Name = AppLocalizationService.GetLang("Lang_PlzChoose"),
						DisplayName = "%" + AppLocalizationService.GetLang("Lang_PlzChoose") + "%",
						Value = 0,
						Description = "%" + AppLocalizationService.GetLang("Lang_PlzChoose") + "%",
						Tag = enumEntry
					});
					for (int num = 0; num < enumEntry.IoNum; num++)
					{
						observableCollection.Add(new EnumEntry
						{
							Name = "IO",
							DisplayName = $"%IO {num + 1}%",
							Value = num + 1,
							Description = $"%IO {num + 1}%",
							Tag = enumEntry
						});
					}
					IOItemOKSendListA.EnumEntrysA = observableCollection;
					IOItemNGSendListA.EnumEntrysA = observableCollection;
					IOItemTimeoutSendListA.EnumEntrysA = observableCollection;
				}
				if (array.Count() == 4)
				{
					ProcedureComm.Params["SelectItem"] = _ID.ToString();
					ProcedureComm.Params["TargetDevice"] = $"{array[1]}#{array[0]}#IO#{enumEntry.IoNum}#{enumEntry.Tag}";
				}
			}
			else
			{
				obj = new EnumerationCommunication().TypeDict[(MODULE_DEVICE_TYPE)obj];
				string value = $"11000#{CommDeviceSelecterA.CurValue}#{obj}";
				ProcedureComm.Params["SelectItem"] = _ID.ToString();
				ProcedureComm.Params["TargetDevice"] = value;
				IOItemOKSendListA = null;
				IOItemNGSendListA = null;
				IOItemTimeoutSendListA = null;
			}
			oldIOControl = enumEntry.FullName;
			if (obj.ToString() == "PLC")
			{
				PLCVisibleA = true;
				IOVisibleA = false;
				CommDevice_SelectedChanged(null, new EventArgs());
			}
			else if (array.LastOrDefault() != null && array.LastOrDefault().Contains("0123456789ABCDEF"))
			{
				IOVisibleA = true;
				PLCVisibleA = false;
			}
			else
			{
				PLCVisibleA = false;
				IOVisibleA = false;
			}
			OnPropertyChanged("CurValueA");
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message + Environment.NewLine + ex.StackTrace);
		}
	}

	private void CommDevice_SelectedChanged(object sender, EventArgs e)
	{
		if (AddressItemList == null)
		{
			return;
		}
		IList allAddresses = CommunicationMgrHelper.GetAllAddresses(CommDeviceSelecterA.CurValue);
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in allAddresses)
		{
			observableCollection.Add(item);
		}
		AddressItemList.EnumEntrysA = observableCollection;
		int curValueA = AddressItemList.CurValueA;
		AddressItemList.CurValueA = -1;
		AddressItemList.CurValueA = curValueA;
		if (e != null && !isRegisterCom)
		{
			AddressItemList.CurValueA = -1;
			AddressItemList.CurValueA = 0;
		}
	}

	private void SelectedChangedCmdExecute()
	{
		if (ProcedureComm != null)
		{
			ProcedureComm.Params["SelectItem"] = _ID.ToString();
			ProcedureComm.Params["AddressIndex"] = AddressItemList.CurValue.ToString();
		}
	}

	private void ClickCommandExecute()
	{
		try
		{
			FunctionManager.Execute("CommSet");
		}
		catch (Exception ex)
		{
			LogHelper.Error("function CommSet execute error:" + ex.ToString());
		}
	}

	private void SelectedChangedCmdExecute_OKSend()
	{
		SelectedChangedCmdExecure_IO(IOItemOKSendListA, "ControllerOK");
	}

	private void SelectedChangedCmdExecute_NGSend()
	{
		SelectedChangedCmdExecure_IO(IOItemNGSendListA, "ControllerNG");
	}

	private void SelectedChangedCmdExecure_IO(Enumeration enumerationList, string target)
	{
		if (enumerationList.EnumEntrysA?[enumerationList.CurValue] is EnumEntry enumEntry && ProcedureComm != null)
		{
			EnumEntry enumEntry2 = enumEntry.Tag as EnumEntry;
			ProcedureComm.Params["SelectItem"] = _ID.ToString();
			string[] array = enumEntry2?.ReservedProperty.ToString().Split('#');
			if (array.Count() == 4)
			{
				ProcedureComm.Params["TargetDevice"] = $"{array[1]}#{array[0]}#IO#{enumEntry2.IoNum}#{enumEntry2.Tag}";
				ProcedureComm.Params[target] = enumEntry.Value.ToString();
			}
			else
			{
				LogHelper.Error("EnumEntry data error:" + enumEntry2.ReservedProperty);
			}
		}
	}

	private void SelectedChangedCmdExecute_TimeoutSend()
	{
		SelectedChangedCmdExecure_IO(IOItemTimeoutSendListA, "ControllerTimeout");
	}

	private void DeleteIOCommandExecute(object procedureCommItem)
	{
		RemoveItem(procedureCommItem);
	}

	public void RemoveItem(object dymaicIO)
	{
		ProcedureCommUnitDynamicIO io = dymaicIO as ProcedureCommUnitDynamicIO;
		if (io == null)
		{
			return;
		}
		ProcedureCommDynamicIO procedureCommDynamicIO = io.parentNode as ProcedureCommDynamicIO;
		ProcedureCommUnitDynamicIO procedureCommUnitDynamicIO = procedureCommDynamicIO.ObservableCollectionA.FirstOrDefault((ProcedureCommUnitDynamicIO x) => x.DisplayIndex == io.DisplayIndex);
		if (procedureCommUnitDynamicIO == null)
		{
			return;
		}
		procedureCommDynamicIO.ObservableCollectionA.Remove(procedureCommUnitDynamicIO);
		procedureCommDynamicIO.ModifyDisplayIndex();
		if (procedureCommUnitDynamicIO.FilterSelecterIfJudge != null)
		{
			ProcedureCommItemDynamicIO toDeleteItemIO = procedureCommUnitDynamicIO.FilterSelecterIfJudge?.GetParentNode() as ProcedureCommItemDynamicIO;
			object dymaicIO2 = procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object x) => (x as DynamicIO).DisplayNameA == toDeleteItemIO.DisplayNameA);
			procedureCommUnitDynamicIO.RemoveIO(dymaicIO2);
		}
		if (procedureCommUnitDynamicIO.FilterSelecterOKInput != null)
		{
			ProcedureCommItemDynamicIO toDeleteItemIO2 = procedureCommUnitDynamicIO.FilterSelecterOKInput?.GetParentNode() as ProcedureCommItemDynamicIO;
			object dymaicIO3 = procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object x) => (x as DynamicIO).DisplayNameA == toDeleteItemIO2.DisplayNameA);
			procedureCommUnitDynamicIO.RemoveIO(dymaicIO3);
		}
		if (procedureCommUnitDynamicIO.FilterSelecterNGInput != null)
		{
			ProcedureCommItemDynamicIO toDeleteItemIO3 = procedureCommUnitDynamicIO.FilterSelecterNGInput?.GetParentNode() as ProcedureCommItemDynamicIO;
			object dymaicIO4 = procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object x) => (x as DynamicIO).DisplayNameA == toDeleteItemIO3.DisplayNameA);
			procedureCommUnitDynamicIO.RemoveIO(dymaicIO4);
		}
		if (procedureCommDynamicIO.IsRegularChecked && procedureCommUnitDynamicIO.FilterSelecterTimeoutSend != null)
		{
			ProcedureCommItemDynamicIO toDeleteItemIO4 = procedureCommUnitDynamicIO.FilterSelecterTimeoutSend?.GetParentNode() as ProcedureCommItemDynamicIO;
			object dymaicIO5 = procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object x) => (x as DynamicIO).DisplayNameA == toDeleteItemIO4.DisplayNameA);
			procedureCommUnitDynamicIO.RemoveIO(dymaicIO5);
		}
		if (ProcedureComm != null)
		{
			ProcedureComm.Params["SelectItem"] = io.ID.ToString();
			ProcedureComm.Params["DeleteItem"] = io.ID.ToString();
		}
	}

	private void CreateCommDeviceSelecterInstance()
	{
		_CommDeviceSelecterA = new EnumerationAllCom
		{
			Name = "EnumerationAllCom",
			AccessMode = "RW",
			CurValue = 0,
			DefaultValue = 0,
			SelectedChangedCommand = SelectedChangedCommand_CommDevice
		};
		_CommDeviceSelecterA.SetParentNode(this);
	}
}
