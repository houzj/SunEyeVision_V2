using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Mvvm.Commands;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class StringImgName_Selecter : ButtonSelecter
{
	private const int NAME_TYPE_INDEX = 0;

	private const int NAME_TYPE_TIME = 1;

	private const int NAME_TYPE_NONE = 2;

	private string imgName;

	public new string CurValue;

	public string ImgNameType;

	public string DefaultValue;

	public int Length;

	public int NameType;

	public ObservableCollection<object> EnumEntrysNameType;

	private const int TIME_FORMAT_0 = 0;

	private const int TIME_FORMAT_1 = 1;

	private const int TIME_FORMAT_2 = 2;

	private const int TIME_FORMAT_3 = 3;

	private Visibility timeVisibility = System.Windows.Visibility.Collapsed;

	private bool bInitFormat = false;

	private int TimeFormat = 0;

	public ObservableCollection<object> EnumEntrysTimeFormat;

	public bool IgnoreInfix;

	private DelegateCommand _ValueChangedCommand = null;

	private DelegateCommand _TimeFormatChangedCommand = null;

	public bool IsSaveTrigger { get; set; }

	public int TriggerCondition { get; set; }

	public string ImgNameTypeA
	{
		get
		{
			return ImgNameType;
		}
		set
		{
			ImgNameType = value;
			OnPropertyChanged("ImgNameType");
			ImgName = "%" + GenerateImgName() + "%";
		}
	}

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
				if (NameType == 1)
				{
					TimeVisibility = System.Windows.Visibility.Visible;
				}
				else
				{
					TimeVisibility = System.Windows.Visibility.Collapsed;
				}
				ImgName = "%" + GenerateImgName() + "%";
				OnPropertyChanged("NameTypeA");
			}
		}
	}

	public int TimeFormatA
	{
		get
		{
			if (!bInitFormat)
			{
				bInitFormat = true;
				MyItemState = ItemState.Normal;
				try
				{
					IUserStringData userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
					if (userStringData != null)
					{
						string value = userStringData[ComName + "Format"];
						TimeFormat = Convert.ToInt32(value);
					}
				}
				catch
				{
					MyItemState = ItemState.GetParamErro;
					OnGetValueCompleted(success: false);
				}
				OnGetValueCompleted(success: true);
			}
			return TimeFormat;
		}
		set
		{
			if (TimeFormat != value)
			{
				if (!bInitFormat)
				{
					bInitFormat = true;
				}
				TimeFormat = value;
				ImgName = "%" + GenerateImgName() + "%";
				OnPropertyChanged("TimeFormatA");
			}
		}
	}

	public Visibility TimeVisibility
	{
		get
		{
			return timeVisibility;
		}
		set
		{
			if (timeVisibility != value)
			{
				timeVisibility = value;
				OnPropertyChanged("TimeVisibility");
			}
		}
	}

	public ObservableCollection<object> EnumEntrysTimeFormatA
	{
		get
		{
			return EnumEntrysTimeFormat;
		}
		set
		{
			if (EnumEntrysTimeFormat != value)
			{
				EnumEntrysTimeFormat = value;
			}
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

	public ObservableCollection<object> EnumEntrysNameTypeA
	{
		get
		{
			return EnumEntrysNameType;
		}
		set
		{
			if (EnumEntrysNameType != value)
			{
				EnumEntrysNameType = value;
			}
		}
	}

	public override string CurValueA
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
				ImgName = "%" + GenerateImgName() + "%";
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

	public ICommand ValueChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public ICommand TimeFormatChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_TimeFormatChangedCommand == null)
			{
				_TimeFormatChangedCommand = new DelegateCommand((Action)TimeFormatChangedCmdExecute);
			}
			return (ICommand)_TimeFormatChangedCommand;
		}
	}

	private void AnalyseNameRule(string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			try
			{
				NameTypeA = Convert.ToInt32(value);
			}
			catch (Exception)
			{
				NameTypeA = 2;
			}
		}
	}

	private string GenerateNameRule()
	{
		return NameTypeA.ToString();
	}

	public string GenerateImgName()
	{
		string lang = AppLocalizationService.GetLang("Lang_Eg");
		lang += "xxx";
		getSaveTriggerConditon();
		if (IsSaveTrigger)
		{
			if (TriggerCondition == 0 || 1 == TriggerCondition)
			{
				lang += "_OK";
			}
			else if (2 == TriggerCondition)
			{
				lang += "_NG";
			}
		}
		if (NameTypeA == 0)
		{
			lang += "_01";
		}
		else if (NameTypeA == 1)
		{
			if (TimeFormatA == 0)
			{
				lang = lang + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
			}
			else if (TimeFormatA == 1)
			{
				lang = lang + "_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff");
			}
			else if (TimeFormatA == 2)
			{
				lang = lang + "_" + DateTime.Now.ToString("yyyy_MM_dd HH_mm_ss_fff");
			}
			else if (TimeFormatA == 3)
			{
				lang = lang + "_" + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss.fff");
			}
		}
		return lang;
	}

	private void getSaveTriggerConditon()
	{
		Tab tab = GetParentNode(2u) as Tab;
		object obj = tab.FindBaseNodeByName("ImageSaveTrigger");
		if (obj != null)
		{
			IsSaveTrigger = (obj as Boolean).CurValueA;
		}
		obj = tab.FindBaseNodeByName("ImageSaveCondition");
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

	private void TimeFormatChangedCmdExecute()
	{
		if (IsUnConnect())
		{
			return;
		}
		string value = TimeFormatA.ToString();
		MyItemState = ItemState.Normal;
		try
		{
			IUserStringData userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
			if (userStringData != null)
			{
				userStringData[ComName + "Format"] = value;
			}
		}
		catch
		{
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
		}
		OnSetValueCompleted(success: true);
	}

	public override int GetParamUnit()
	{
		return GetParamUnitByZmq();
	}

	public override int SetParamUnit()
	{
		string value = Value;
		return SetParamUnitByZmq(value);
	}
}
