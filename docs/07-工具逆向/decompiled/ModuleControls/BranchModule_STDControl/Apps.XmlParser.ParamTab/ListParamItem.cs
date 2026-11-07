using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class ListParamItem : NameValueItem
{
	private string id;

	private string inputValue;

	private bool IsEnabled = true;

	private bool _IsVisible = true;

	private DelegateCommand _ValueChangedCommand;

	private DelegateCommand _UpTemplateCommand;

	private DelegateCommand _DownTemplateCommand;

	public string BitValue => "Defaults";

	public string BitValueN => "N";

	public string ValueValue { get; set; }

	public string ID
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

	public string InputValue
	{
		get
		{
			return inputValue;
		}
		set
		{
			if (IsUintNo(value))
			{
				if (!string.IsNullOrWhiteSpace(value) && value.Length > 1 && value.Substring(0, 1) == "0")
				{
					value = value.Substring(1);
				}
				inputValue = value;
				if ((parentNode as ListParam).SelectedTypeA == 0)
				{
					ValueValue = value;
				}
				OnPropertyChanged("InputValue");
			}
		}
	}

	public bool IsFirst
	{
		get
		{
			if (GetParentNode() is ListParam listParam)
			{
				return listParam.IsFirstElement(this);
			}
			return false;
		}
	}

	public bool IsLast
	{
		get
		{
			if (GetParentNode() is ListParam listParam)
			{
				return listParam.IsLastElement(this);
			}
			return false;
		}
	}

	public bool IsEnabledA
	{
		get
		{
			return IsEnabled;
		}
		set
		{
			if (IsEnabled != value)
			{
				IsEnabled = value;
				OnPropertyChanged("IsEnabledA");
			}
		}
	}

	public bool IsVisible
	{
		get
		{
			return _IsVisible;
		}
		set
		{
			if (_IsVisible != value)
			{
				_IsVisible = value;
				OnPropertyChanged("IsVisible");
			}
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public ICommand UpTemplateCommand
	{
		get
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			if (_UpTemplateCommand == null)
			{
				_UpTemplateCommand = new DelegateCommand((Action)UpTemplateCmdExecute);
			}
			return (ICommand)_UpTemplateCommand;
		}
	}

	public ICommand DownTemplateCommand
	{
		get
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			if (_DownTemplateCommand == null)
			{
				_DownTemplateCommand = new DelegateCommand((Action)DownTemplateCmdExecute);
			}
			return (ICommand)_DownTemplateCommand;
		}
	}

	private void ValueChangedCmdExecute()
	{
		if (GetParentNode() is ListParam listParam && (parentNode as ListParam).SelectedTypeA == 0)
		{
			listParam.ValueChangedCmdExecute();
		}
	}

	private void UpTemplateCmdExecute()
	{
		if (GetParentNode() is ListParam listParam)
		{
			listParam.UpTemplate(this);
		}
	}

	private void DownTemplateCmdExecute()
	{
		if (GetParentNode() is ListParam listParam)
		{
			listParam.DownTemplate(this);
		}
	}

	public void UpdateProperty()
	{
		OnPropertyChanged("IsFirst");
		OnPropertyChanged("IsLast");
	}

	private bool IsUintNo(string strValue)
	{
		int result = 0;
		if (string.IsNullOrWhiteSpace(strValue))
		{
			return true;
		}
		if (strValue == BitValue)
		{
			return true;
		}
		if (int.TryParse(strValue, out result))
		{
			return true;
		}
		return false;
	}
}
