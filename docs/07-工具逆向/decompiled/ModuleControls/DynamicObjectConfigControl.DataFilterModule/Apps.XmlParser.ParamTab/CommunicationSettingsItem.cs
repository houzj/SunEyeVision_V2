using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Interface;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class CommunicationSettingsItem : DynamicIOContainer<ProcedureCommDynamicIO>
{
	private int id;

	private string displayIndex = "1";

	private DelegateCommand _SetIntervalChangedCommand = null;

	private Integer _SetInterval;

	private string targetDevice;

	private DelegateCommand _SelectDeviceChangedCommand = null;

	private EnumerationCommunication commDevices;

	private Enumeration _CommDeviceSelecter;

	private DelegateCommand<CommunicationSettingsItem> _DeleteCommand = null;

	public int ID
	{
		get
		{
			return id;
		}
		set
		{
			id = value;
		}
	}

	public string DisplayIndex
	{
		get
		{
			return displayIndex;
		}
		set
		{
			displayIndex = value;
			OnPropertyChanged("DisplayIndex");
		}
	}

	public int SelectCurrentMode { get; set; }

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
					ValueChangedCommand = SetIntervalChangedCommand
				};
			}
			return _SetInterval;
		}
	}

	public string TargetDevice => targetDevice = commDevices.Value.Replace("+CommManagerModule", "");

	public object IfJudge { get; set; }

	public object OKInput { get; set; }

	public object NGInput { get; set; }

	public object TimeoutSend { get; set; }

	public ICommand SelectDeviceChangedCommand
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

	public EnumerationCommunication CommDevices
	{
		get
		{
			if (commDevices == null)
			{
				Tab tab = GetParentNode<Tab>();
				if (tab != null)
				{
					commDevices = tab.FindBaseNodeByName("CommunicationModuleID") as EnumerationCommunication;
					commDevices.SelectedChangedCommand = SelectDeviceChangedCommand;
				}
			}
			return commDevices;
		}
	}

	public Enumeration CommDeviceSelecter
	{
		get
		{
			if (_CommDeviceSelecter == null)
			{
			}
			return _CommDeviceSelecter;
		}
	}

	public ICommand DeleteCommand => (ICommand)(_DeleteCommand ?? (_DeleteCommand = new DelegateCommand<CommunicationSettingsItem>((Action<CommunicationSettingsItem>)DeleteCommandCmdExecute)));

	public override bool IsInitAdd => true;

	public override string DefaultTypeName => "string";

	private void SetIntervalChangedCommandExecute()
	{
		GroupCommunicationParam groupCommunicationParam = parentNode as GroupCommunicationParam;
		groupCommunicationParam.Owner.Params["SelectItem"] = id.ToString();
		groupCommunicationParam.Owner.Params["SetInterval"] = _SetInterval.CurValueA.ToString();
	}

	private void SelectDeviceChangedCommandExecute()
	{
		GroupCommunicationParam groupCommunicationParam = parentNode as GroupCommunicationParam;
		groupCommunicationParam.Owner.Params["SelectItem"] = id.ToString();
		groupCommunicationParam.Owner.Params["TargetDevice"] = TargetDevice;
	}

	public CommunicationSettingsItem()
	{
	}

	public CommunicationSettingsItem(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public CommunicationSettingsItem(string name, string inputParamName, bool isAdd, object parent)
	{
	}

	private void DeleteCommandCmdExecute(CommunicationSettingsItem item)
	{
		GroupCommunicationParam groupCommunicationParam = parentNode as GroupCommunicationParam;
		ObservableCollection<CommunicationSettingsItem> itemsA = groupCommunicationParam.ItemsA;
		if (itemsA.Contains(item))
		{
			itemsA.Remove(item);
		}
		groupCommunicationParam.ModifyDisplayIndex();
		RemoveIO(item.IfJudge);
		RemoveIO(item.OKInput);
		RemoveIO(item.NGInput);
		if (item.SelectCurrentMode == 1)
		{
			RemoveIO(item.TimeoutSend);
		}
		groupCommunicationParam.Owner.Params["DeleteItem"] = item.ID.ToString();
	}

	internal void ModifyDisplayIndex()
	{
		if (!(GetParentNode() is GroupCommunicationParam { Items: not null, Items: var items }))
		{
			return;
		}
		CommunicationSettingsItem[] array = items.Where((CommunicationSettingsItem p) => p != null).Cast<CommunicationSettingsItem>().ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			if (array[num] != null)
			{
				array[num].DisplayIndex = (num + 1).ToString();
			}
		}
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		object obj = base.AddIO(name, type, index);
		if (obj != null)
		{
			if (index > -1)
			{
				(obj as DynamicIO).IndexA = (index - ItemsA.Where((object x) => !(x as DynamicIO).CustomVisibleA).Count()).ToString();
			}
			(obj as DynamicIO).AddImageObject();
		}
		return obj;
	}

	public override void RemoveIO(object dymaicIO)
	{
		if (dymaicIO is DynamicIO dynamicIO)
		{
			dynamicIO.IsAddedA = false;
			dynamicIO.RemoveImageObject(dynamicIO.IoDataSource);
			base.RemoveIO(dymaicIO);
		}
	}
}
