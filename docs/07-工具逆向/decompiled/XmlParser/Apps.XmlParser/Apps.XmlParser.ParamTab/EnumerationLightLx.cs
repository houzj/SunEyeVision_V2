using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml;
using Hik.Xml;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class EnumerationLightLx : NameValueItem
{
	private ObservableCollection<object> EnumEntrys;

	public string CurValue = "0";

	private int DefaultValue;

	private int EnumCurValue;

	private bool isTriggerCameraSelectChange = true;

	public new bool IsGetShowError = true;

	public int selectedIndex;

	private DelegateCommand<object> _SelectedChangedCommand = null;

	private DelegateCommand _ClickCommand = null;

	protected bool CanClick = true;

	public override string DisplayType => "EnumerationLx";

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
				OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public int EnumCurValueA
	{
		get
		{
			return EnumCurValue;
		}
		set
		{
			EnumCurValue = value;
			OnPropertyChanged("EnumCurValueA");
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

	public new string AlgorithmIndexA
	{
		get
		{
			return AlgorithmIndex;
		}
		set
		{
			AlgorithmIndex = value;
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
			CurValueA = value;
		}
	}

	public override bool NeedInit => true;

	public int SelectedIndex => selectedIndex;

	public ICommand SelectedChangedCommand
	{
		get
		{
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand<object>((Action<object>)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public event EventHandler OnClick;

	public override BaseNode Init(XmlNode node)
	{
		IsGetShowError = false;
		base.Init(node);
		return this;
	}

	private void SelectedChangedCmdExecute(object value)
	{
		selectedIndex = (int)value;
		if (!IsUnConnect() && isTriggerCameraSelectChange && (int)value >= 0 && (int)value < EnumEntrysA.Count && (EnumEntrysA[(int)value] as EnumEntryLx).StringValueA != CurValueA)
		{
			CurValueA = (EnumEntrysA[(int)value] as EnumEntryLx).StringValueA;
		}
	}

	private void ClickCommandCmdExecute()
	{
		if (OnClick != null)
		{
			OnClick(this, new EventArgs());
		}
	}

	public override int GetParamUnit()
	{
		return 0;
	}
}
