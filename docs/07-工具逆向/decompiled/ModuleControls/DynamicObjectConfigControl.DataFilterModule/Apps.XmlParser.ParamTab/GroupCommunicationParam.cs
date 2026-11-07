using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GroupCommunicationParam : BridgeItemBase
{
	private VmModule owner;

	public ObservableCollection<CommunicationSettingsItem> Items = new ObservableCollection<CommunicationSettingsItem>();

	private bool isRegularChecked = true;

	private bool isEndChecked = false;

	private string subscribeDataType = "int;float;byte;string";

	private DelegateCommand _AddCommand = null;

	public VmModule Owner
	{
		get
		{
			if (owner == null)
			{
				owner = (GetVmModule() as VmModuleSet).ProcedureCommunicaitonModule as VmModule;
			}
			return owner;
		}
	}

	public ObservableCollection<CommunicationSettingsItem> ItemsA => Items;

	public bool IsRegularChecked
	{
		get
		{
			return isRegularChecked;
		}
		set
		{
			isRegularChecked = value;
			if (isRegularChecked)
			{
				foreach (CommunicationSettingsItem item in ItemsA)
				{
					Owner.Params["DeleteItem"] = item.ID.ToString();
					item.RemoveIO(item.IfJudge);
					item.RemoveIO(item.OKInput);
					item.RemoveIO(item.NGInput);
				}
				ItemsA.Clear();
			}
			OnPropertyChanged("IsRegularChecked");
		}
	}

	public bool IsEndChecked
	{
		get
		{
			return isEndChecked;
		}
		set
		{
			isEndChecked = value;
			if (isEndChecked)
			{
				foreach (CommunicationSettingsItem item in ItemsA)
				{
					Owner.Params["DeleteItem"] = item.ID.ToString();
					item.RemoveIO(item.IfJudge);
					item.RemoveIO(item.OKInput);
					item.RemoveIO(item.NGInput);
					item.RemoveIO(item.TimeoutSend);
				}
				ItemsA.Clear();
			}
			OnPropertyChanged("IsEndChecked");
		}
	}

	public ICommand AddCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _AddCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddCommandCmdExecute);
				DelegateCommand val3 = val2;
				_AddCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public int MaxCount => 16;

	public int MinCount => 1;

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
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
			}
		}
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
	}

	private void AddCommandCmdExecute()
	{
		int num = Enumerable.Count(ItemsA);
		if (num >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		CommunicationSettingsItem communicationSettingsItem = new CommunicationSettingsItem();
		string[] array = Owner.Params["AllItemsId"].TrimEnd(';').Split(';');
		if (array.Length != 0 && !string.IsNullOrEmpty(array[0]))
		{
			List<string> source = array.OrderByDescending((string x) => int.Parse(x)).ToList();
			if (string.IsNullOrEmpty(source.FirstOrDefault()))
			{
				communicationSettingsItem.ID = 1;
			}
			else
			{
				communicationSettingsItem.ID = int.Parse(source.FirstOrDefault()) + 1;
			}
		}
		else
		{
			communicationSettingsItem.ID = 1;
		}
		communicationSettingsItem.DataSource = GetVmModule();
		communicationSettingsItem.IsOutput = false;
		communicationSettingsItem.SetParentNode(this);
		if (IsRegularChecked)
		{
			communicationSettingsItem.SelectCurrentMode = 1;
			communicationSettingsItem.IfJudge = communicationSettingsItem.AddIO($"condition_{communicationSettingsItem.ID}", "string");
			communicationSettingsItem.OKInput = communicationSettingsItem.AddIO($"ok_send_{communicationSettingsItem.ID}", "string");
			communicationSettingsItem.NGInput = communicationSettingsItem.AddIO($"ng_send_{communicationSettingsItem.ID}", "string");
			communicationSettingsItem.TimeoutSend = communicationSettingsItem.AddIO($"timeout_send_{communicationSettingsItem.ID}", "string");
			ItemsA.Add(communicationSettingsItem);
		}
		else if (IsEndChecked)
		{
			communicationSettingsItem.SelectCurrentMode = 2;
			communicationSettingsItem.IfJudge = communicationSettingsItem.AddIO($"condition_{communicationSettingsItem.ID}", "string");
			communicationSettingsItem.OKInput = communicationSettingsItem.AddIO($"ok_send_{communicationSettingsItem.ID}", "string");
			communicationSettingsItem.NGInput = communicationSettingsItem.AddIO($"ng_send_{communicationSettingsItem.ID}", "string");
			ItemsA.Add(communicationSettingsItem);
		}
		ModifyDisplayIndex();
		Owner.Params["AddItem"] = communicationSettingsItem.ID.ToString();
		Owner.Params["SelectCurrentMode"] = communicationSettingsItem.SelectCurrentMode.ToString();
		Owner.Params["SelectItem"] = communicationSettingsItem.ID.ToString();
	}

	internal void ModifyDisplayIndex()
	{
		CommunicationSettingsItem[] array = ItemsA.Where((CommunicationSettingsItem p) => p != null).Cast<CommunicationSettingsItem>().ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			if (array[num] != null)
			{
				array[num].DisplayIndex = (num + 1).ToString();
			}
		}
	}
}
