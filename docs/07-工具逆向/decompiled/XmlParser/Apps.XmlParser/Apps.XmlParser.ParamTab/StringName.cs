using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class StringName : NameValueItem
{
	private const int NAME_TYPE_INDEX = 0;

	private const int NAME_TYPE_TIME = 1;

	private const int NAME_TYPE_NONE = 2;

	public int NameType;

	public ButtonSelecter FileName;

	private string imgName;

	public ObservableCollection<object> EnumEntrys;

	public string DefaultValue;

	public int Length;

	public bool IgnoreInfix;

	private DelegateCommand _ValueChangedCommand = null;

	public int NameTypeA
	{
		get
		{
			return NameType;
		}
		set
		{
			if (NameType != value)
			{
				NameType = value;
				OnPropertyChanged("NameTypeA");
				ImgName = "%" + GenerateImgName() + "%";
			}
		}
	}

	public int TriggerCondition { get; set; }

	public ButtonSelecter FileNameA
	{
		get
		{
			return FileName;
		}
		set
		{
			FileName = value;
			OnPropertyChanged("FileNameA");
		}
	}

	public string ImgName
	{
		get
		{
			imgName = "%" + GenerateImgName() + "%";
			return imgName;
		}
		set
		{
			imgName = value;
			OnPropertyChanged("ImgName");
		}
	}

	public ObservableCollection<object> EnumEntrysA
	{
		get
		{
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

	public string DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
		}
	}

	public int LengthA
	{
		get
		{
			return Length;
		}
		set
		{
			Length = value;
		}
	}

	public override string Value
	{
		get
		{
			return GenerateNameRule();
		}
		set
		{
			AnalyseNameRule(value);
		}
	}

	public bool IsSaveTrigger { get; set; }

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand(ValueChangedCmdExecute);
			}
			return _ValueChangedCommand;
		}
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (FileName != null)
		{
			FileName.InniterBusiness();
			if (string.IsNullOrEmpty(FileName.CurValueA))
			{
				FileName.CurValueA = DefaultValueA;
			}
		}
	}

	private void AnalyseNameRule(string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			bool flag = false;
			try
			{
				NameTypeA = Convert.ToInt32(value);
			}
			catch
			{
				NameTypeA = 2;
			}
		}
	}

	private string GenerateNameRule()
	{
		return NameTypeA.ToString();
	}

	private string GenerateImgName()
	{
		string lang = AppLocalizationService.GetLang("Lang_Eg");
		lang += "xxx";
		if (!IgnoreInfix)
		{
			getSaveTriggerConditon();
			if (IsSaveTrigger)
			{
				if (TriggerCondition == 0 || 1 == TriggerCondition)
				{
					lang += "_OK_";
				}
				else if (2 == TriggerCondition)
				{
					lang += "_NG_";
				}
			}
		}
		else
		{
			lang = lang + "_" + AppLocalizationService.GetLang("Module ID");
		}
		if (NameTypeA == 0)
		{
			lang += "_01";
		}
		else if (NameTypeA == 1)
		{
			lang = lang + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
		}
		return lang;
	}

	private void getSaveTriggerConditon()
	{
		Tab tab = GetParentNode(2u) as Tab;
		object obj = tab.FindBaseNodeByName("SaveTrigger");
		if (obj != null)
		{
			IsSaveTrigger = (obj as Boolean).CurValueA;
		}
		obj = tab.FindBaseNodeByName("SaveCondition");
		if (obj != null)
		{
			TriggerCondition = (obj as Enumeration).CurValueA;
		}
	}

	private void ValueChangedCmdExecute()
	{
		if (IsUnConnect())
		{
			return;
		}
		string value = Value;
		if (OldValue != value)
		{
			int num = SetParamUnit();
			OldValue = value;
			if (num >= 0)
			{
				PreExecuteRun();
			}
		}
	}
}
