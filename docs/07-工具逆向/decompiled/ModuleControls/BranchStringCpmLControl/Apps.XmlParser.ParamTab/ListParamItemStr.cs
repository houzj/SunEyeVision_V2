using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class ListParamItemStr : NameValueItem
{
	private string id;

	private string inputValue;

	private bool ifdebugmode;

	private DelegateCommand _ValueChangedCommand;

	private DelegateCommand _UpTemplateCommand;

	private DelegateCommand _DownTemplateCommand;

	public string BitValue => "默认值";

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
			if (value.Length <= 30 && !value.Contains("#") && !value.Contains("$"))
			{
				inputValue = value;
				ValueValue = value;
				OnPropertyChanged("InputValue");
			}
		}
	}

	public bool IfDebugMode
	{
		get
		{
			return ifdebugmode;
		}
		set
		{
			if (ifdebugmode != value)
			{
				ifdebugmode = value;
			}
			OnPropertyChanged("IfDebugMode");
		}
	}

	public bool IsFirst
	{
		get
		{
			if (GetParentNode() is ListParamStr listParamStr)
			{
				return listParamStr.IsFirstElement(this);
			}
			return false;
		}
	}

	public bool IsLast
	{
		get
		{
			if (GetParentNode() is ListParamStr listParamStr)
			{
				return listParamStr.IsLastElement(this);
			}
			return false;
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
		if (GetParentNode() is ListParamStr listParamStr && (parentNode as ListParamStr).SelectedTypeA == 0)
		{
			listParamStr.ValueChangedCmdExecute();
		}
	}

	private void UpTemplateCmdExecute()
	{
		if (GetParentNode() is ListParamStr listParamStr)
		{
			listParamStr.UpTemplate(this);
		}
	}

	private void DownTemplateCmdExecute()
	{
		if (GetParentNode() is ListParamStr listParamStr)
		{
			listParamStr.DownTemplate(this);
		}
	}

	public void UpdateProperty()
	{
		OnPropertyChanged("IsFirst");
		OnPropertyChanged("IsLast");
	}
}
