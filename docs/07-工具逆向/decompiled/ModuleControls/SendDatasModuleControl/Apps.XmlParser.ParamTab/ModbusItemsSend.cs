using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Hik.Util;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ModbusItemsSend : NameValueItem
{
	private const string SEND_DATA_1 = "SendData1";

	private const string SEND_DATA_2 = "SendData2";

	private const string SEND_DATA_3 = "SendData3";

	private const string SEND_DATA_4 = "SendData4";

	private const string SEND_DATA_5 = "SendData5";

	private const string SEND_DATA_6 = "SendData6";

	private const string SEND_DATA_7 = "SendData7";

	private const string SEND_DATA_8 = "SendData8";

	private const string SEND_DATA_9 = "SendData9";

	private const string SEND_DATA_10 = "SendData10";

	private const string SEND_DATA_11 = "SendData11";

	private const string SEND_DATA_12 = "SendData12";

	private const string SEND_DATA_13 = "SendData13";

	private const string SEND_DATA_14 = "SendData14";

	private const string SEND_DATA_15 = "SendData15";

	private const string SEND_DATA_16 = "SendData16";

	private bool flagEventRegister = false;

	private Enumeration functionCode;

	private EnumerationCommunication commDevice;

	public bool IsAddButtonVisible = false;

	public ObservableCollection<ModbusItemSend> Items = null;

	private bool flagCommandBind = false;

	public Enumeration AddressItemList;

	private DelegateCommand _SelectedChangedCommand = null;

	private DelegateCommand addCommand = null;

	public int MaxCount => 16;

	public int MinCount => 1;

	public Enumeration FunctionCode
	{
		get
		{
			if (functionCode == null)
			{
				Tab tab = GetParentNode<Tab>();
				if (tab != null)
				{
					functionCode = tab.FindBaseNodeByName("FunctionCode") as Enumeration;
				}
			}
			return functionCode;
		}
	}

	public EnumerationCommunication CommDevice
	{
		get
		{
			if (commDevice == null)
			{
				Tab tab = GetParentNode<Tab>();
				if (tab != null)
				{
					commDevice = tab.FindBaseNodeByName("CommunicationModuleID") as EnumerationCommunication;
				}
			}
			return commDevice;
		}
	}

	public bool IsAddButtonVisibleA
	{
		get
		{
			return IsAddButtonVisible;
		}
		set
		{
			if (IsAddButtonVisible != value)
			{
				IsAddButtonVisible = value;
				OnPropertyChanged("IsAddButtonVisibleA");
			}
		}
	}

	public ObservableCollection<ModbusItemSend> ItemsA
	{
		get
		{
			if (Items == null)
			{
				Items = new ObservableCollection<ModbusItemSend>
				{
					new ModbusItemSend("PLCItem1", "SendData1", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem2", "SendData2", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem3", "SendData3", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem4", "SendData4", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem5", "SendData5", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem6", "SendData6", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem7", "SendData7", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem8", "SendData8", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem9", "SendData9", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem10", "SendData10", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem11", "SendData11", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem12", "SendData12", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem13", "SendData13", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem14", "SendData14", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem15", "SendData15", isAdd: false, canDelete: false, this),
					new ModbusItemSend("PLCItem16", "SendData16", isAdd: false, canDelete: false, this)
				};
			}
			return Items;
		}
	}

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
					FunctionCoe_SelectedChanged(FunctionCode, null);
				}
				else
				{
					HideEveryItem();
				}
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
			}
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

	public ICommand AddCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (addCommand == null)
			{
				addCommand = new DelegateCommand((Action)AddCommandExecute);
			}
			return (ICommand)addCommand;
		}
	}

	public override void OnInniterBusinessFinish()
	{
		FunctionCameraManager.Register("GetSendAddress", GetSendAddress);
	}

	private void SelectedChangedCmdExecute()
	{
		if (!(AddressItemList.OldValue != AddressItemList.Value))
		{
			return;
		}
		try
		{
			AddressItemList.OldValue = AddressItemList.Value;
			foreach (ModbusItemSend item in Items)
			{
				if (item.CustomVisibleA)
				{
					item.ChangeAddressValue(AddressItemList.Value);
				}
			}
		}
		catch (Exception)
		{
			AddressItemList.Value = AddressItemList.OldValue;
		}
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
		}
	}

	private void AddModbusItem()
	{
		if (Items != null)
		{
			ModbusItemSend modbusItemSend = Items.FirstOrDefault((ModbusItemSend p) => !p.CustomVisibleA && !p.IsAddedA);
			if (modbusItemSend != null)
			{
				modbusItemSend.IsAddedA = true;
				modbusItemSend.CustomVisibleA = true;
				SetVisibleUserData();
				modbusItemSend.ChangeAddressValue(AddressItemList?.Value);
			}
		}
	}

	public void SetVisibleUserData()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (ModbusItemSend item in ItemsA)
		{
			stringBuilder.Append($"{item.NameA}-{item.CustomVisibleA}-{item.IsAddedA};");
		}
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			vmModule.UserData[$"params.{NameA}"] = StringUtil.UTF8GetBytesPadZero(stringBuilder.ToString());
		}
	}

	private void GetVisibleUserData()
	{
		VmModule vmModule = GetVmModule();
		if (vmModule == null)
		{
			return;
		}
		byte[] array = vmModule.UserData[$"params.{NameA}"];
		if (array == null || array.Length == 0)
		{
			return;
		}
		string text = StringUtil.UTF8GetString(array);
		string[] array2 = text.Split(';');
		for (int i = 0; i < array2.Length; i++)
		{
			string[] tmpStr = array2[i].Split('-');
			ModbusItemSend modbusItemSend = ItemsA.FirstOrDefault((ModbusItemSend x) => x.NameA == tmpStr[0]);
			if (modbusItemSend != null)
			{
				modbusItemSend.CustomVisible = bool.Parse(tmpStr[1]);
				modbusItemSend.IsAdded = bool.Parse(tmpStr[2]);
				if (i == array2.Length - 2)
				{
					modbusItemSend.ModifyDeleteBtnVisibility();
				}
			}
		}
	}

	private int GetCurCount()
	{
		if (Items == null)
		{
			return 0;
		}
		return Enumerable.Count(Items, (ModbusItemSend p) => p.IsAddedA);
	}

	private void HideEveryItem()
	{
		if (Items == null)
		{
			return;
		}
		foreach (ModbusItemSend item in Items)
		{
			item.CustomVisibleA = false;
		}
	}

	private void AddCommandExecute()
	{
		int curCount = GetCurCount();
		if (curCount >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
		}
		else
		{
			AddModbusItem();
		}
	}

	public void GetSendAddress()
	{
		if (AddressItemListA == null)
		{
			return;
		}
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

	private void FunctionCoe_SelectedChanged(object sender, EventArgs e)
	{
		if (!(sender is Enumeration enumeration) || Items == null)
		{
			return;
		}
		if (3 == enumeration.CurValueA)
		{
			IsAddButtonVisibleA = false;
			ModbusItemBase[] array = (from ModbusItemBase p in Items.Where((ModbusItemSend p) => p != null)
				where p.IsAddedA
				select p).ToArray();
			if (array.Length >= 1)
			{
				for (int num = 1; num < array.Length; num++)
				{
					array[num].DeleteCommandCmdExecute();
				}
				array[0].CustomVisibleA = true;
			}
			else if (array.Length == 0)
			{
				AddModbusItem();
			}
		}
		else if (4 == enumeration.CurValueA)
		{
			ModbusItemBase[] array2 = (from ModbusItemBase p in Items.Where((ModbusItemSend p) => p != null)
				where p.IsAddedA
				select p).ToArray();
			for (int num2 = 0; num2 < array2.Length; num2++)
			{
				array2[num2].CustomVisibleA = true;
			}
			if (array2.Length == 0)
			{
				AddModbusItem();
			}
			IsAddButtonVisibleA = true;
		}
	}

	public override int GetParamUnit()
	{
		foreach (ModbusItemSend item in ItemsA)
		{
			if (item.CustomVisibleA)
			{
				item.GetParamUnit();
			}
		}
		IEnumerable<ModbusItemSend> enumerable = Items.Where((ModbusItemSend x) => x.CustomVisibleA);
		int num = 0;
		if (enumerable != null)
		{
			foreach (ModbusItemSend item2 in enumerable)
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
				if (item2.AddressItemList != null)
				{
					if (num == 0)
					{
						AddressItemList.CurValueA = item2.AddressItemList.CurValue;
					}
					item2.ProperChangAddress();
				}
				num++;
			}
		}
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (ItemsA != null)
		{
			GetVisibleUserData();
			ModbusItemBase[] array = (from ModbusItemBase p in Items.Where((ModbusItemSend p) => p != null)
				where p.IsAddedA
				select p).ToArray();
			ModbusItemBase[] array2 = array;
			foreach (ModbusItemBase modbusItemBase in array2)
			{
				modbusItemBase.InniterBusiness();
			}
		}
	}

	internal void RefreshAddressData(int index, bool IsDelete)
	{
		ModbusItemBase[] array = (from ModbusItemBase p in Items.Where((ModbusItemSend p) => p != null)
			where p.CustomVisibleA
			select p).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			Items[num].RefreshAddressData(index, IsDelete);
		}
	}
}
