using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GateWaySendEventItem : NameValueItem
{
	private const string inFilterStr = "SendData";

	private bool flagEvent = false;

	private bool flagOpen = false;

	private IList Items = null;

	public IList ItemsA
	{
		get
		{
			if (Items == null || !flagOpen)
			{
				if (GetParentNode(2u) is Tab tab)
				{
					BaseNode baseNode = tab.FindBaseNodeByName("SendEventId");
					if (baseNode != null && baseNode is EnumerationSendEvent)
					{
						DataSendEvent_SelectedChanged(baseNode, null);
						if (!flagEvent)
						{
							(baseNode as EnumerationSendEvent).OnSelectedChanged += DataSendEvent_SelectedChanged;
							flagEvent = true;
						}
					}
				}
				flagOpen = true;
			}
			return Items;
		}
		set
		{
			if (Items != value)
			{
				Items = value;
				OnPropertyChanged("ItemsA");
			}
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
			if (CustomVisible == value)
			{
				return;
			}
			CustomVisible = value;
			OnPropertyChanged("CustomVisibleA");
			if (CustomVisible || Items == null || Items.Count <= 0)
			{
				return;
			}
			foreach (object item in Items)
			{
				(item as ButtonSelecter_Event).CurValueA = null;
			}
			Items.Clear();
		}
	}

	private void DataSendEvent_SelectedChanged(object sender, EventArgs e)
	{
		if (!(sender is EnumerationSendEvent enumerationSendEvent))
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		IList eventParamsList = enumerationSendEvent.EventParamsList;
		if (Items != null && Items.Count > 0 && flagOpen)
		{
			foreach (object item in Items)
			{
				(item as ButtonSelecter_Event).CurValueA = null;
			}
			Items.Clear();
		}
		flagOpen = true;
		if (eventParamsList != null)
		{
			stringBuilder.Append(eventParamsList.Count + "-");
			IList list = new List<ButtonSelecter_Event>();
			for (int i = 0; i < eventParamsList.Count; i++)
			{
				EnumEntry enumEntry = eventParamsList[i] as EnumEntry;
				ButtonSelecter_Event buttonSelecter_Event = AddButtonSelector(i + 1, enumEntry.Name, enumEntry.Name, enumEntry.ReservedProperty.ToString());
				list.Add(buttonSelecter_Event);
				stringBuilder.Append($"{enumEntry.Name}\r{enumEntry.ReservedProperty.ToString()}\n");
				UpdateOperationParams(buttonSelecter_Event);
			}
			ItemsA = list;
		}
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			vmModule.Params["SendEventRule"] = ((Items.Count > 0) ? stringBuilder.Remove(stringBuilder.Length - 1, 1).ToString() : stringBuilder.ToString());
		}
	}

	private ButtonSelecter_Event AddButtonSelector(int index, string name, string displayName, string type)
	{
		string operationParams = "SendData" + index;
		ButtonSelecter_Event buttonSelecter_Event = new ButtonSelecter_Event
		{
			Name = name,
			Description = displayName,
			DisplayName = displayName,
			CustomVisible = false,
			IndexA = index.ToString(),
			TypeNameA = type,
			DropOpenSetter = new Setter
			{
				TargetName = "EnumEntrys",
				OperationName = "GetFrontFilterTreeOperation",
				OperationParams = type
			},
			Triggers = new ObservableCollection<object>(),
			Initers = new ObservableCollection<object>()
		};
		buttonSelecter_Event.SetParentNode(this);
		buttonSelecter_Event.DropOpenSetter.SetParentNode(buttonSelecter_Event);
		Setter setter = new Setter
		{
			TargetName = "CurValue",
			OperationName = "GetSelectedFilterOperation",
			OperationParams = operationParams
		};
		setter.CreatOperateInstance();
		setter.SetParentNode(buttonSelecter_Event);
		buttonSelecter_Event.Initers.Add(setter);
		Trigger trigger = new Trigger
		{
			Property = "CurValue",
			Setters = new ObservableCollection<object>()
		};
		Setter setter2 = new Setter
		{
			OperationName = "SetFilterSourceOperation",
			OperationParams = operationParams
		};
		setter2.CreatOperateInstance();
		setter2.SetParentNode(trigger);
		trigger.Setters.Add(setter2);
		trigger.SetParentNode(buttonSelecter_Event);
		buttonSelecter_Event.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = "CustomVisible",
			Value = "False",
			Setters = new ObservableCollection<object>()
		};
		Setter setter3 = new Setter
		{
			TargetName = name,
			OperationName = "SetValueNullOperaion",
			OperationParams = "CurValue"
		};
		setter3.CreatOperateInstance();
		setter3.SetParentNode(trigger2);
		setter3.TargetObj = buttonSelecter_Event;
		trigger2.Setters.Add(setter3);
		trigger2.SetParentNode(buttonSelecter_Event);
		buttonSelecter_Event.Triggers.Add(trigger2);
		Trigger trigger3 = new Trigger
		{
			Property = "CustomVisible",
			Value = "True",
			Setters = new ObservableCollection<object>()
		};
		Setter setter4 = new Setter
		{
			TargetName = "CurValue",
			OperationName = "GetSelectedFilterOperation",
			OperationParams = operationParams
		};
		setter4.CreatOperateInstance();
		setter4.SetParentNode(trigger3);
		setter4.TargetObj = buttonSelecter_Event;
		trigger3.Setters.Add(setter4);
		trigger3.SetParentNode(buttonSelecter_Event);
		buttonSelecter_Event.Triggers.Add(trigger3);
		buttonSelecter_Event.InniterBusiness();
		return buttonSelecter_Event;
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		flagOpen = false;
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	private void UpdateOperationParams(ButtonSelecter_Event buttonSelecter)
	{
		if (!string.IsNullOrWhiteSpace(buttonSelecter.CurValueA))
		{
			if (string.IsNullOrWhiteSpace(buttonSelecter.TypeNameA))
			{
				buttonSelecter.CurValueA = null;
			}
			else if (!CheckSubscribeDataType(buttonSelecter))
			{
				buttonSelecter.CurValueA = null;
			}
		}
	}

	private bool CheckSubscribeDataType(ButtonSelecter_Event buttonSelecter)
	{
		bool result = true;
		if (string.IsNullOrWhiteSpace(buttonSelecter.CurValueA))
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
			if (buttonSelecter.Triggers == null || buttonSelecter.Triggers.Count == 0)
			{
				return result;
			}
			if (!(buttonSelecter.Triggers[0] is Trigger) || (buttonSelecter.Triggers[0] as Trigger).Setters.Count == 0)
			{
				return result;
			}
			if (!((buttonSelecter.Triggers[0] as Trigger).Setters[0] is Setter { OperationParamsA: var operationParamsA }))
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
				if (iO.RelateIO is VmConstantIO)
				{
					return true;
				}
				string typeName = iO.RelateIO.TypeName;
				result = ((string.IsNullOrWhiteSpace(typeName) || !(typeName != buttonSelecter.TypeNameA)) ? true : false);
			}
		}
		catch (Exception)
		{
			result = true;
		}
		return result;
	}
}
