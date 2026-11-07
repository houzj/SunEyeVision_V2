using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class ModbusItemSend : ModbusItemBase
{
	private const int TYPE_COMMUNICATION = 1;

	private bool isThrowException = false;

	private bool flagEventRegister = false;

	private bool flagCommandBind = false;

	private string subscribeDataType = "int;float;string";

	private bool IsIniterBussiness = false;

	public string oldSubscribeDataType = "int;float;string";

	public Enumeration AddressItemList;

	private DelegateCommand _DeleteCommand = null;

	private DelegateCommand _SelectedChangedCommand = null;

	public Enumeration AddressItemListA
	{
		get
		{
			if (AddressItemList == null)
			{
				AddressItemList = new Enumeration();
				AddressItemList.Name = "AddressItemList";
				AddressItemList.AccessMode = "O";
				AddressItemList.CurValue = 0;
				AddressItemList.DefaultValue = 0;
				AddressItemList.SetParentNode(this);
			}
			if (!flagCommandBind)
			{
				AddressItemList.SelectedChangedCommand = SelectedChangedCommand;
				flagCommandBind = true;
			}
			Tab tab = GetParentNode<Tab>();
			if (tab != null)
			{
				BaseNode baseNode = tab.FindBaseNodeByName("CommunicationModuleID");
				if (baseNode != null && baseNode is EnumerationCommunication)
				{
					CommDevice_SelectedChanged(baseNode, null);
					if (!flagEventRegister)
					{
						(baseNode as EnumerationCommunication).OnSelectedChanged += CommDevice_SelectedChanged;
						flagEventRegister = true;
					}
				}
			}
			return AddressItemList;
		}
		set
		{
			AddressItemList = value;
			OnPropertyChanged("AddressItemListA");
		}
	}

	public override string Value => GenerateSetMsg();

	public override bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				if (value)
				{
					CustomVisible = IsAddedA;
				}
				else
				{
					CustomVisible = value;
				}
				OnPropertyChanged("CustomVisibleA");
				ModifyDeleteBtnVisibility();
				if (!CustomVisible && IsIniterBussiness)
				{
					CurValueA = null;
				}
			}
		}
	}

	public override bool IsAddedA
	{
		get
		{
			return IsAdded;
		}
		set
		{
			if (IsAdded != value)
			{
				IsAdded = value;
				OnPropertyChanged("IsAddedA");
				SetParamUnit();
			}
		}
	}

	public override ICommand DeleteCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
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

	public override void DeleteCommandCmdExecute()
	{
		base.DeleteCommandCmdExecute();
		if (AddressItemList != null)
		{
			AddressItemList.CurValueA = 0;
		}
	}

	private void SelectedChangedCmdExecute()
	{
		if (OldValue != Value)
		{
			if (SetParamUnit() == 0)
			{
				AddressItemList.OldValue = AddressItemList.Value;
				RefreshSendData();
			}
			else
			{
				AddressItemList.Value = AddressItemList.OldValue;
			}
		}
	}

	public override int GetParamUnit()
	{
		int num = 0;
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
			try
			{
				Check(text2);
				if (Value != text2)
				{
					OldValue = text2;
					AnalyseGetMsg(text2);
				}
			}
			catch (Exception ex)
			{
				string lang = AppLocalizationService.GetLang("Lang_ParamGet");
				HikMessageBox.Show(lang + ex.Message, MessageIcons.Error);
				LogHelper.Error("获取失败！！！！！名称：" + Name + " des:" + Description + "  获取值:" + text);
			}
			num = 0;
		}
		catch
		{
			MyItemState = ItemState.GetParamErro;
			num = -1;
		}
		IsIniterBussiness = true;
		return num;
	}

	public override int SetParamUnit()
	{
		string value = Value;
		if (string.IsNullOrWhiteSpace(value))
		{
			MyItemState = ItemState.SetParamErro;
			return -2;
		}
		MyItemState = ItemState.Normal;
		VmModule vmModule = GetVmModule();
		int num = 0;
		try
		{
			if (vmModule != null)
			{
				vmModule.Params[Name] = value;
			}
		}
		catch
		{
			num = -1;
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
			return num;
		}
		OldValue = value;
		OnSetValueCompleted(success: true);
		SetModbusItemIndex();
		return 0;
	}

	private void SetModbusItemIndex()
	{
		string text = "";
		ModbusItemsSend modbusItemsSend = GetParentNode<ModbusItemsSend>();
		if (modbusItemsSend != null && modbusItemsSend.ItemsA != null)
		{
			foreach (ModbusItemSend item in modbusItemsSend.ItemsA)
			{
				if (item is ModbusItemSend && item.CustomVisibleA)
				{
					text = text + (item as ModbusItemSend).Value + "\r";
				}
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				vmModule.UserData["PlcItemRecord"] = StringUtil.UTF8GetBytesPadZero(text);
			}
			else
			{
				LogHelper.Error($"模块获取为空,setMsg={text}");
			}
		}
	}

	public override string ToString()
	{
		return Name + "_IsAdd[" + IsAddedA + "]_CustomVisible[" + CustomVisibleA + "]";
	}

	internal void RefreshDataSource()
	{
		if (GetParentNode(2u) is Tab tab)
		{
			BaseNode baseNode = tab.FindBaseNodeByName("CommunicationModuleID");
			if (baseNode != null && baseNode is EnumerationCommunication)
			{
				CommDevice_SelectedChanged(baseNode, null);
			}
		}
	}

	public ModbusItemSend()
	{
	}

	public ModbusItemSend(string name, string inputParamName, bool isAdd, bool canDelete, object parent)
	{
		AccessMode = "O";
		CustomVisible = isAdd;
		IsAdded = isAdd;
		CanDelete = canDelete;
		Name = name;
		CreateTriggers(this, inputParamName);
		SetParentNode(parent);
	}

	public override BaseNode Init(XmlNode node)
	{
		BaseNode result = base.Init(node);
		if (Triggers != null)
		{
			foreach (object trigger in Triggers)
			{
				if (trigger == null || !(trigger is Trigger) || !(trigger as Trigger).Property.Equals("CustomVisible") || (trigger as Trigger).Setters == null)
				{
					continue;
				}
				foreach (Setter setter in (trigger as Trigger).Setters)
				{
					if (setter != null && setter.TargetObj == null)
					{
						setter.TargetObj = this;
					}
				}
			}
		}
		return result;
	}

	public void ChangeAddressValue(string curValue)
	{
		if (AddressItemList == null)
		{
			AddressItemListA.Value = curValue;
		}
		else
		{
			AddressItemList.Value = curValue;
		}
		SelectedChangedCmdExecute();
	}

	public void ProperChangAddress()
	{
		string value = AddressItemListA.Value;
	}

	private void CommDevice_SelectedChanged(object sender, EventArgs e)
	{
		if (!CustomVisible || AddressItemList == null || !(sender is EnumerationCommunication { AddressList: var addressList }))
		{
			return;
		}
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in addressList)
		{
			observableCollection.Add(item);
		}
		AddressItemList.EnumEntrysA = observableCollection;
		if (e != null)
		{
			AddressItemList.CurValueA = AddressItemList.EnumEntrysA.Count - 1;
			AddressItemList.CurValueA = 0;
			DelSubscriber();
		}
		RefreshSendData();
	}

	public void GetSendAddressA()
	{
		int curValueA = AddressItemList.CurValueA;
		bool flag = true;
		Tab tab = GetParentNode<Tab>();
		if (tab == null)
		{
			return;
		}
		BaseNode baseNode = tab.FindBaseNodeByName("CommunicationModuleID");
		if (baseNode == null || !(baseNode is EnumerationCommunication { AddressList: var addressList } enumerationCommunication))
		{
			return;
		}
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in addressList)
		{
			observableCollection.Add(item);
		}
		AddressItemList.EnumEntrysA = observableCollection;
		foreach (object item2 in AddressItemList.EnumEntrysA)
		{
			if ((item2 as EnumEntry).Value == curValueA)
			{
				flag = false;
				break;
			}
		}
		if (flag || curValueA < 0)
		{
			AddressItemList.CurValueA = 0;
		}
		enumerationCommunication.OnPropertyChanged("AddressList");
		AddressItemList.OnPropertyChanged("CurValueA");
	}

	private string GenerateSetMsg()
	{
		string result = "";
		string text = Name + "#";
		try
		{
			if (Triggers == null || Triggers.Count == 0)
			{
				return result;
			}
			if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0)
			{
				return result;
			}
			text += ((Triggers[0] as Trigger).Setters[0] as Setter).OperationParams;
			text = ((!IsAddedA) ? (text + "#0") : (text + "#1"));
			text = ((AddressItemList != null) ? (text + "#" + AddressItemList.Value) : (text + "#0"));
			result = text;
		}
		catch (Exception)
		{
			return "";
		}
		return result;
	}

	private void AnalyseGetMsg(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}
		if (AddressItemList == null)
		{
			AddressItemList = new Enumeration();
			AddressItemList.Name = "AddressItemList";
			AddressItemList.AccessMode = "O";
			AddressItemList.CurValue = 0;
			AddressItemList.DefaultValue = 0;
			AddressItemList.SetParentNode(this);
		}
		string[] array = value.Split('#');
		if (array.Length < 4)
		{
			return;
		}
		IsAdded = array[2] == "1";
		AddressItemList.CurValue = Convert.ToInt32(array[3]);
		int num = -1;
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			BaseNode baseNode = algorithmTabRoot.FindBaseNodeByName("OutputType");
			if (baseNode != null && baseNode is RadioSelecterEx3)
			{
				num = (baseNode as RadioSelecterEx3).CurValueA;
			}
		}
		if (num == 1)
		{
			CustomVisibleA = array[2] == "1";
		}
		else
		{
			CustomVisibleA = false;
		}
	}

	private bool CheckSubscribeDataType(string filterStr)
	{
		bool result = true;
		if (string.IsNullOrWhiteSpace(filterStr))
		{
			return true;
		}
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return true;
			}
			if (Triggers == null || Triggers.Count == 0)
			{
				return result;
			}
			if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0)
			{
				return result;
			}
			if (!((Triggers[0] as Trigger).Setters[0] is Setter { OperationParamsA: var operationParamsA }))
			{
				return result;
			}
			VmIO iO = vmModule.GetIO(operationParamsA);
			if (iO == null)
			{
				return result;
			}
			if (iO.RelateIO != null)
			{
				string typeName = iO.RelateIO.TypeName;
				result = ((string.IsNullOrWhiteSpace(typeName) || subscribeDataType.Contains(typeName)) ? true : false);
			}
		}
		catch (Exception)
		{
			result = true;
		}
		return result;
	}

	private void DelSubscriber()
	{
		SetPropertyValueByNameNotrigger("CurValueA", null);
		try
		{
			if (Triggers != null && Triggers.Count > 0)
			{
				if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0 || !((Triggers[0] as Trigger).Setters[0] is Setter setter))
				{
					return;
				}
				setter.Operate(new object[2] { this, null });
			}
			isThrowException = false;
		}
		catch (Exception)
		{
			isThrowException = true;
		}
	}

	private void UpdateOperationParams()
	{
		DropOpenSetter.OperationParams = subscribeDataType;
		if (!subscribeDataType.Contains(oldSubscribeDataType))
		{
			oldSubscribeDataType = subscribeDataType;
			DelSubscriber();
		}
		else if (!string.IsNullOrWhiteSpace(CurValueA))
		{
			if (string.IsNullOrWhiteSpace(subscribeDataType))
			{
				DelSubscriber();
			}
			else if (!CheckSubscribeDataType(CurValueA))
			{
				DelSubscriber();
			}
		}
	}

	private void RefreshSendData()
	{
		if (AddressItemList.CurValueA == 0)
		{
			subscribeDataType = "";
		}
		else
		{
			subscribeDataType = "int;float;string";
		}
		UpdateOperationParams();
	}

	internal void RefreshAddressData(int index, bool IsDelete)
	{
		if (IsDelete)
		{
			if (AddressItemList.CurValueA > index)
			{
				subscribeDataType = "";
				AddressItemList.CurValue = 0;
				SetParamUnit();
			}
		}
		else if (AddressItemList.CurValueA == index + 1 || AddressItemList.CurValueA == index + 2)
		{
			subscribeDataType = "";
			AddressItemList.CurValue = 0;
			SetParamUnit();
		}
	}

	private void CreateTriggers(ButtonSelecter buttonSelecter, string inputParamName)
	{
		if (buttonSelecter.Triggers == null)
		{
			buttonSelecter.Triggers = new ObservableCollection<object>();
		}
		if (buttonSelecter.Initers == null)
		{
			buttonSelecter.Initers = new ObservableCollection<object>();
		}
		buttonSelecter.DropOpenSetter = new Setter
		{
			TargetName = "EnumEntrys",
			OperationName = "GetFrontFilterTreeOperation",
			OperationParams = ";"
		};
		buttonSelecter.DropOpenSetter.SetParentNode(buttonSelecter);
		Setter setter = new Setter
		{
			TargetName = "CurValue",
			OperationName = "GetSelectedFilterOperation",
			OperationParams = inputParamName
		};
		setter.CreatOperateInstance();
		setter.SetParentNode(buttonSelecter);
		buttonSelecter.Initers.Add(setter);
		Trigger trigger = new Trigger
		{
			Property = "CurValue",
			Setters = new ObservableCollection<object>()
		};
		Setter setter2 = new Setter
		{
			OperationName = "SetFilterSourceOperation",
			OperationParams = inputParamName
		};
		setter2.CreatOperateInstance();
		setter2.SetParentNode(trigger);
		trigger.Setters.Add(setter2);
		trigger.SetParentNode(buttonSelecter);
		buttonSelecter.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = "CustomVisible",
			Value = "True",
			Setters = new ObservableCollection<object>()
		};
		Setter setter3 = new Setter
		{
			TargetName = "CurValue",
			OperationName = "GetSelectedFilterOperation",
			OperationParams = inputParamName
		};
		setter3.CreatOperateInstance();
		setter3.SetParentNode(trigger2);
		setter3.TargetObj = buttonSelecter;
		trigger2.Setters.Add(setter3);
		trigger2.SetParentNode(buttonSelecter);
		buttonSelecter.Triggers.Add(trigger2);
		Trigger trigger3 = new Trigger
		{
			Property = "CustomVisible",
			Setters = new ObservableCollection<object>()
		};
		Setter setter4 = new Setter
		{
			TargetName = buttonSelecter.NameA,
			OperationName = "SetFilterForcePropertyOperation",
			OperationParams = inputParamName
		};
		setter4.CreatOperateInstance();
		setter4.SetParentNode(trigger3);
		setter4.TargetObj = buttonSelecter;
		trigger3.Setters.Add(setter4);
		trigger3.SetParentNode(buttonSelecter);
		buttonSelecter.Triggers.Add(trigger3);
	}
}
