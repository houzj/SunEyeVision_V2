using System;
using System.Collections;
using System.Windows.Input;
using Apps.Data;
using Apps.Log;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class EnumerationSendEvent : NameValueItem
{
	public delegate void SelectedChangedHandler(object sender, EventArgs e);

	public int CurValue;

	private IList EnumEntrys;

	private const int ModuleId = 0;

	private IList eventParamsList;

	private DelegateCommand _SelectedChangedCommand = null;

	private DelegateCommand _RefreshListCommand = null;

	private DelegateCommand _ClickCommand = null;

	public override string DisplayType => "EnumerationSendEvent";

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
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public override string Value
	{
		get
		{
			return CurValue.ToString();
		}
		set
		{
			CurValueA = int.Parse(value);
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			EnumEntrys = CommunicationMgrHelper.GetAllSendEvents();
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

	public IList EventParamsList
	{
		get
		{
			eventParamsList = CommunicationMgrHelper.GetAllSendEventParams(CurValueA);
			return eventParamsList;
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
	}

	public ICommand RefreshListCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_RefreshListCommand == null)
			{
				_RefreshListCommand = new DelegateCommand((Action)RefreshListCmdExecute);
			}
			return (ICommand)_RefreshListCommand;
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
				_ClickCommand = new DelegateCommand((Action)OpenGlobalLight);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public event SelectedChangedHandler OnSelectedChanged;

	private void RefreshListCmdExecute()
	{
		EnumEntrysA = CommunicationMgrHelper.GetAllSendEvents();
		OnPropertyChanged("CurValueA");
	}

	public void OpenGlobalLight()
	{
		try
		{
			FunctionManager.Execute("GlobalLightSet");
		}
		catch (Exception ex)
		{
			LogHelper.Error("function GlobalCameraSet execute error:" + ex.ToString());
		}
	}

	public void SelectedChangedCmdExecute()
	{
		if (IsUnConnect())
		{
			return;
		}
		string value = Value;
		if (OldValue != value)
		{
			if (SetParamUnit() == 0)
			{
				OldValue = value;
				PreExecuteRun();
			}
			if (OnSelectedChanged != null)
			{
				EventArgs e = new EventArgs();
				OnSelectedChanged(this, e);
			}
		}
		if (CurValueA > 0)
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
			}
		}
	}

	public override int SetParamUnit()
	{
		return base.SetParamUnit();
	}

	public override int GetParamUnit()
	{
		return base.GetParamUnit();
	}
}
