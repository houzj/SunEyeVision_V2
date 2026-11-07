using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class EnumerationIP : NameValueItem
{
	private bool isModfied = false;

	private ObservableCollection<object> EnumEntrys;

	public string CurValue;

	private DelegateCommand _SelectedChangedCommand = null;

	public override string DisplayType
	{
		get
		{
			return "EnumerationIP";
		}
		set
		{
		}
	}

	public ObservableCollection<object> EnumEntrysA
	{
		get
		{
			if (EnumEntrys == null)
			{
				GetHostIP();
			}
			if (!isModfied)
			{
				CurValueA = (EnumEntrys[0] as EnumEntry)?.StrValueA;
				SetParamUnit();
			}
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
			return CurValueA;
		}
		set
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				CurValueA = value;
				isModfied = true;
			}
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

	private void GetHostIP()
	{
		string text = "";
		string hostName = Dns.GetHostName();
		IPAddress[] hostAddresses = Dns.GetHostAddresses(hostName);
		if (EnumEntrys == null)
		{
			EnumEntrys = new ObservableCollection<object>();
		}
		for (int i = 0; i < hostAddresses.Count(); i++)
		{
			if (hostAddresses[i].AddressFamily == AddressFamily.InterNetwork)
			{
				text = hostAddresses[i].ToString();
				EnumEntry item = new EnumEntry
				{
					DisplayNameA = text,
					StrValueA = text
				};
				EnumEntrys.Add(item);
			}
		}
	}

	public void SelectedChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value)
		{
			SetParamUnit();
			OldValue = Value;
		}
	}
}
