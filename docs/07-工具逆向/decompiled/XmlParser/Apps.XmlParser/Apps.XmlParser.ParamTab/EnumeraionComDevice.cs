using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Data;
using Apps.Data.Common;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using GateWayServer;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class EnumeraionComDevice : NameValueItem
{
	private IList EnumEntrys;

	public string CurValue;

	private EnumEntry _selectedItem = null;

	private EnumEntry DefaultEmptyItem = new EnumEntry
	{
		DisplayNameA = "",
		DescriptionA = "",
		ValueA = -2
	};

	private object _reserved;

	private DelegateCommand<ObjectEventArgs> _ClickCommand = null;

	private DelegateCommand<ObjectEventArgs> _SingleClickCommand = null;

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

	public string CurValueA
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

	public override string Value
	{
		get
		{
			return GetPipeAddress();
		}
		set
		{
			CurValueA = AnalyzePipeAddress(value);
		}
	}

	public EnumEntry SelectedItem
	{
		get
		{
			return _selectedItem;
		}
		set
		{
			if (_selectedItem != value)
			{
				_selectedItem = value;
				OnPropertyChanged("SelectedItem");
			}
		}
	}

	private bool isBindCom
	{
		get
		{
			object obj = SelectedItem?.GetParentNode();
			return obj == null;
		}
	}

	public object Reserved
	{
		get
		{
			return _reserved;
		}
		set
		{
			if (_reserved != value)
			{
				_reserved = value;
				OnPropertyChanged("Reserved");
			}
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)ClickCommandExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public ICommand SingleClickCommand
	{
		get
		{
			if (_SingleClickCommand == null)
			{
				_SingleClickCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)ClickCommandExecute);
			}
			return (ICommand)_SingleClickCommand;
		}
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

	public void ChangeDevice(int comDevice, int addressDevice, bool removeCom)
	{
		if (SelectedItem == null)
		{
			return;
		}
		int num = -1;
		bool flag = false;
		if (removeCom)
		{
			num = (isBindCom ? SelectedItem.ValueA : SelectedItem.GetParentNode<EnumEntry>().ValueA);
			flag = comDevice == num;
		}
		else
		{
			num = SelectedItem.ValueA;
			flag = addressDevice == num;
		}
		if (flag)
		{
			SelectedItem = null;
			CurValueA = "";
			int num2 = SetParamUnit();
			if (num2 >= 0)
			{
				PreExecuteRun();
			}
		}
	}

	public void ClickCommandExecute(ObjectEventArgs obj)
	{
		if (obj == null)
		{
			return;
		}
		EnumEntry enumEntry = obj.Param as EnumEntry;
		EnumEntry enumEntry2 = enumEntry.GetParentNode<EnumEntry>();
		if (enumEntry2 != null)
		{
			CurValueA = enumEntry2.DisplayNameA + "." + enumEntry.DisplayNameA;
		}
		else
		{
			CurValueA = enumEntry.DisplayNameA;
		}
		SelectedItem = enumEntry;
		TaskAsyncHelper.RunAsync(() =>
		{
			string value = Value;
			if (OldValue != value && SetParamUnit() == 0)
			{
				OldValue = value;
				PreExecuteRun();
			}
		});
	}

	private string GetPipeAddress()
	{
		string result = string.Empty;
		try
		{
			IList enumEntrys = EnumEntrys;
			if (enumEntrys != null)
			{
				if (SelectedItem != null)
				{
					object obj = SelectedItem.GetParentNode();
					int num = -1;
					int num2 = -1;
					int num3 = -1;
					if (obj != null)
					{
						num2 = (obj as EnumEntry).Value;
						num = SelectedItem.ValueA;
						num3 = (int)(obj as EnumEntry).ReservedProperty;
					}
					else
					{
						num2 = SelectedItem.ValueA;
						num3 = ((num2 == 0) ? (-1) : ((int)SelectedItem.ReservedProperty));
					}
					result = $"{num2}#{num}#{num3}";
				}
				else
				{
					result = "-1#-1#-1";
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("GetPipeAddress is exception:" + ex.ToString());
		}
		return result;
	}

	private string AnalyzePipeAddress(string pipeAddress)
	{
		string result = string.Empty;
		if (!string.IsNullOrWhiteSpace(pipeAddress))
		{
			string value = "-1";
			string value2 = "-1";
			string[] array = pipeAddress.Split('#');
			if (array.Length > 2)
			{
				value = array[0];
				value2 = array[1];
			}
			int num = Convert.ToInt32(value);
			int addressid = Convert.ToInt32(value2);
			IList enumEntrysA = EnumEntrysA;
			if (enumEntrysA != null)
			{
				foreach (EnumEntry item in enumEntrysA)
				{
					if (num != item.ValueA)
					{
						continue;
					}
					if (addressid > -1)
					{
						SelectedItem = (EnumEntry)item.EnumEntrysA.FirstOrDefault((object x) => (x as EnumEntry).Value == addressid);
						result = item.DisplayNameA + "." + SelectedItem.DisplayNameA;
					}
					else
					{
						SelectedItem = item;
						result = item.DisplayNameA;
					}
					break;
				}
			}
		}
		return result;
	}

	public IList GetAllCommunicationDevices()
	{
		IList list = new List<EnumEntry>();
		IList allCommunicationDevices = CommunicationMgrHelper.GetAllCommunicationDevices(true, true, (string[])null);
		if (allCommunicationDevices != null && allCommunicationDevices.Count > 0)
		{
			foreach (object item in allCommunicationDevices)
			{
				object reservedProperty = (item as EnumEntry).ReservedProperty;
				if (object.Equals((object)(MODULE_DEVICE_TYPE)5, reservedProperty) || object.Equals((object)(MODULE_DEVICE_TYPE)6, reservedProperty))
				{
					IList allAddresses = CommunicationMgrHelper.GetAllAddresses((item as EnumEntry).Value);
					if (allAddresses == null || allAddresses.Count <= 0)
					{
						continue;
					}
					allAddresses.RemoveAt(0);
					foreach (EnumEntry item2 in allAddresses)
					{
						item2.SetParentNode(item as EnumEntry);
					}
					(item as EnumEntry).EnumEntrysA = (ObservableCollection<object>)allAddresses;
				}
				list.Add(item);
			}
		}
		return list;
	}
}
