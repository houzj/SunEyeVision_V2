using System.Collections.ObjectModel;
using Apps.Data;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class IOSendData16 : NameValueItem
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

	private ButtonSelecter outCondition1;

	private ButtonSelecter outCondition2;

	private ButtonSelecter outCondition3;

	private ButtonSelecter outCondition4;

	private ButtonSelecter outCondition5;

	private ButtonSelecter outCondition6;

	private ButtonSelecter outCondition7;

	private ButtonSelecter outCondition8;

	private ButtonSelecter outCondition9;

	private ButtonSelecter outCondition10;

	private ButtonSelecter outCondition11;

	private ButtonSelecter outCondition12;

	private ButtonSelecter outCondition13;

	private ButtonSelecter outCondition14;

	private ButtonSelecter outCondition15;

	private ButtonSelecter outCondition16;

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

	public ButtonSelecter OutCondition5
	{
		get
		{
			if (outCondition5 == null)
			{
				outCondition5 = CreateOutCondition("SendData5", "OutCondition5", "RunParam_IO5 Output Condition");
			}
			outCondition5.InniterBusiness();
			return outCondition5;
		}
	}

	public ButtonSelecter OutCondition6
	{
		get
		{
			if (outCondition6 == null)
			{
				outCondition6 = CreateOutCondition("SendData6", "OutCondition6", "RunParam_IO6 Output Condition");
			}
			outCondition6.InniterBusiness();
			return outCondition6;
		}
	}

	public ButtonSelecter OutCondition7
	{
		get
		{
			if (outCondition7 == null)
			{
				outCondition7 = CreateOutCondition("SendData7", "OutCondition7", "RunParam_IO7 Output Condition");
			}
			outCondition7.InniterBusiness();
			return outCondition7;
		}
	}

	public ButtonSelecter OutCondition8
	{
		get
		{
			if (outCondition8 == null)
			{
				outCondition8 = CreateOutCondition("SendData8", "OutCondition8", "RunParam_IO8 Output Condition");
			}
			outCondition8.InniterBusiness();
			return outCondition8;
		}
	}

	public ButtonSelecter OutCondition9
	{
		get
		{
			if (outCondition9 == null)
			{
				outCondition9 = CreateOutCondition("SendData9", "OutCondition9", "RunParam_IO9 Output Condition");
			}
			outCondition9.InniterBusiness();
			return outCondition9;
		}
	}

	public ButtonSelecter OutCondition10
	{
		get
		{
			if (outCondition10 == null)
			{
				outCondition10 = CreateOutCondition("SendData10", "OutCondition10", "RunParam_IO10 Output Condition");
			}
			outCondition10.InniterBusiness();
			return outCondition10;
		}
	}

	public ButtonSelecter OutCondition11
	{
		get
		{
			if (outCondition11 == null)
			{
				outCondition11 = CreateOutCondition("SendData11", "OutCondition11", "RunParam_IO11 Output Condition");
			}
			outCondition11.InniterBusiness();
			return outCondition11;
		}
	}

	public ButtonSelecter OutCondition12
	{
		get
		{
			if (outCondition12 == null)
			{
				outCondition12 = CreateOutCondition("SendData12", "OutCondition12", "RunParam_IO12 Output Condition");
			}
			outCondition12.InniterBusiness();
			return outCondition12;
		}
	}

	public ButtonSelecter OutCondition13
	{
		get
		{
			if (outCondition13 == null)
			{
				outCondition13 = CreateOutCondition("SendData13", "OutCondition13", "RunParam_IO13 Output Condition");
			}
			outCondition13.InniterBusiness();
			return outCondition13;
		}
	}

	public ButtonSelecter OutCondition14
	{
		get
		{
			if (outCondition14 == null)
			{
				outCondition14 = CreateOutCondition("SendData14", "OutCondition14", "RunParam_IO14 Output Condition");
			}
			outCondition14.InniterBusiness();
			return outCondition14;
		}
	}

	public ButtonSelecter OutCondition15
	{
		get
		{
			if (outCondition15 == null)
			{
				outCondition15 = CreateOutCondition("SendData15", "OutCondition15", "RunParam_IO15 Output Condition");
			}
			outCondition15.InniterBusiness();
			return outCondition15;
		}
	}

	public ButtonSelecter OutCondition16
	{
		get
		{
			if (outCondition16 == null)
			{
				outCondition16 = CreateOutCondition("SendData16", "OutCondition16", "RunParam_IO16 Output Condition");
			}
			outCondition16.InniterBusiness();
			return outCondition16;
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
					OutCondition5.CurValueA = null;
					OutCondition6.CurValueA = null;
					OutCondition7.CurValueA = null;
					OutCondition8.CurValueA = null;
					OutCondition9.CurValueA = null;
					OutCondition10.CurValueA = null;
					OutCondition11.CurValueA = null;
					OutCondition12.CurValueA = null;
					OutCondition13.CurValueA = null;
					OutCondition14.CurValueA = null;
					OutCondition15.CurValueA = null;
					OutCondition16.CurValueA = null;
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
		string value = Value;
		if (string.IsNullOrWhiteSpace(value))
		{
			MyItemState = ItemState.SetParamErro;
			return -2;
		}
		MyItemState = ItemState.Normal;
		VmModule vmModule = GetVmModule();
		int num = -1;
		if (vmModule != null)
		{
			try
			{
				vmModule.Params[Name] = value;
			}
			catch (VmException ex)
			{
				MyItemState = ItemState.SetParamErro;
				OnSetValueCompleted(success: false);
				return ex.errorCode;
			}
		}
		OldValue = value;
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
		return "SendData1#SendData2#SendData3#SendData4#SendData5#SendData6#SendData7#SendData8#SendData9#SendData10#SendData11#SendData12#SendData13#SendData14#SendData15#SendData16";
	}

	private void HideEveryItem()
	{
		OutCondition1.CustomVisibleA = false;
		OutCondition2.CustomVisibleA = false;
		OutCondition3.CustomVisibleA = false;
		OutCondition4.CustomVisibleA = false;
		OutCondition5.CustomVisibleA = false;
		OutCondition6.CustomVisibleA = false;
		OutCondition7.CustomVisibleA = false;
		OutCondition8.CustomVisibleA = false;
		OutCondition9.CustomVisibleA = false;
		OutCondition10.CustomVisibleA = false;
		OutCondition11.CustomVisibleA = false;
		OutCondition12.CustomVisibleA = false;
		OutCondition13.CustomVisibleA = false;
		OutCondition14.CustomVisibleA = false;
		OutCondition15.CustomVisibleA = false;
		OutCondition16.CustomVisibleA = false;
	}

	private void ShowEveryItem()
	{
		OutCondition1.CustomVisibleA = true;
		OutCondition2.CustomVisibleA = true;
		OutCondition3.CustomVisibleA = true;
		OutCondition4.CustomVisibleA = true;
		OutCondition5.CustomVisibleA = true;
		OutCondition6.CustomVisibleA = true;
		OutCondition7.CustomVisibleA = true;
		OutCondition8.CustomVisibleA = true;
		OutCondition9.CustomVisibleA = true;
		OutCondition10.CustomVisibleA = true;
		OutCondition11.CustomVisibleA = true;
		OutCondition12.CustomVisibleA = true;
		OutCondition13.CustomVisibleA = true;
		OutCondition14.CustomVisibleA = true;
		OutCondition15.CustomVisibleA = true;
		OutCondition16.CustomVisibleA = true;
	}
}
