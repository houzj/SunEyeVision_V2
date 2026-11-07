using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class RadioSelecter : NameValueItem, IGroupRunParam
{
	public int CurValue;

	public int DefaultValue;

	private bool isSet = true;

	public int OldTmpValue = -1;

	public ObservableCollection<object> EnumEntrys;

	private DelegateCommand<object> _ValueChangedCommand;

	public int LangRadioSelecterWidth
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return 165;
			}
			return 89;
		}
	}

	public int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				if (isSet)
				{
					SetUserData(value.ToString());
				}
				OldTmpValue = CurValue;
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public int DefaultValueA
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

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			int result = 0;
			if (int.TryParse(value, out result))
			{
				CurValueA = result;
			}
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

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand<object>((Action<object>)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	string IGroupRunParam.DisplayForGroupType => null;

	protected override void SetDefaultValue()
	{
		isSet = false;
		CurValueA = DefaultValue;
		isSet = true;
	}

	private void ValueChangedCmdExecute(object e)
	{
		if (!IsUnConnect())
		{
			CurValueA = (int)e;
			int num = SetParamUnit();
			if (num >= 0)
			{
				PreExecuteRun();
			}
		}
	}

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return null;
	}

	protected IParamsConfig GetConfig()
	{
		return (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot) ? algorithmTabRoot.UserStringData : null;
	}
}
