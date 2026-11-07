using System.Collections.ObjectModel;
using Apps.Data;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class IOSendData4 : NameValueItem
{
	private const string SEND_DATA_1 = "SendData1";

	private const string SEND_DATA_2 = "SendData2";

	private const string SEND_DATA_3 = "SendData3";

	private const string SEND_DATA_4 = "SendData4";

	private ButtonSelecter outCondition1;

	private ButtonSelecter outCondition2;

	private ButtonSelecter outCondition3;

	private ButtonSelecter outCondition4;

	private bool IsIniterBussiness = false;

	public ButtonSelecter OutCondition1
	{
		get
		{
			if (outCondition1 == null)
			{
				outCondition1 = CreateOutCondition("SendData1", "OutCondition1", "RunParam_IO1 Output Condition");
			}
			outCondition1.InniterBusiness();
			return outCondition1;
		}
	}

	public ButtonSelecter OutCondition2
	{
		get
		{
			if (outCondition2 == null)
			{
				outCondition2 = CreateOutCondition("SendData2", "OutCondition2", "RunParam_IO2 Output Condition");
			}
			outCondition2.InniterBusiness();
			return outCondition2;
		}
	}

	public ButtonSelecter OutCondition3
	{
		get
		{
			if (outCondition3 == null)
			{
				outCondition3 = CreateOutCondition("SendData3", "OutCondition3", "RunParam_IO3 Output Condition");
			}
			outCondition3.InniterBusiness();
			return outCondition3;
		}
	}

	public ButtonSelecter OutCondition4
	{
		get
		{
			if (outCondition4 == null)
			{
				outCondition4 = CreateOutCondition("SendData4", "OutCondition4", "RunParam_IO4 Output Condition");
			}
			outCondition4.InniterBusiness();
			return outCondition4;
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
					ShowEveryItem();
				}
				else
				{
					HideEveryItem();
				}
				CustomVisible = value;
				if (!CustomVisible && IsIniterBussiness)
				{
					OutCondition1.CurValueA = null;
					OutCondition2.CurValueA = null;
					OutCondition3.CurValueA = null;
					OutCondition4.CurValueA = null;
				}
				OnPropertyChanged("CustomVisibleA");
			}
		}
	}

	public override int GetParamUnit()
	{
		IsIniterBussiness = true;
		return 0;
	}

	public override int SetParamUnit()
	{
		string text = Value;
		if (string.IsNullOrWhiteSpace(text))
		{
			MyItemState = ItemState.SetParamErro;
			return -2;
		}
		MyItemState = ItemState.Normal;
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			try
			{
				text = vmModule.Params["Name"];
			}
			catch (VmException ex)
			{
				MyItemState = ItemState.SetParamErro;
				OnSetValueCompleted(success: false);
				return ex.errorCode;
			}
		}
		OldValue = text;
		OnSetValueCompleted(success: true);
		return 0;
	}

	private ButtonSelecter CreateOutCondition(string inputParamName, string conditionName, string displayName)
	{
		ButtonSelecter buttonSelecter = new ButtonSelecter
		{
			Name = conditionName,
			Description = displayName,
			DisplayName = displayName,
			CustomVisible = false,
			DropOpenSetter = new Setter
			{
				TargetName = "EnumEntrys",
				OperationName = "GetFrontFilterTreeOperation",
				OperationParams = "string"
			},
			Triggers = new ObservableCollection<object>(),
			Initers = new ObservableCollection<object>()
		};
		buttonSelecter.SetParentNode(this);
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
		return buttonSelecter;
	}

	private string GenerateSetMsg()
	{
		return "SendData1#SendData2#SendData3#SendData4";
	}

	private void HideEveryItem()
	{
		OutCondition1.CustomVisibleA = false;
		OutCondition2.CustomVisibleA = false;
		OutCondition3.CustomVisibleA = false;
		OutCondition4.CustomVisibleA = false;
	}

	private void ShowEveryItem()
	{
		OutCondition1.CustomVisibleA = true;
		OutCondition2.CustomVisibleA = true;
		OutCondition3.CustomVisibleA = true;
		OutCondition4.CustomVisibleA = true;
	}
}
