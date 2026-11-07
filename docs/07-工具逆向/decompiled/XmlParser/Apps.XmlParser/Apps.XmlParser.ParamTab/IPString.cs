using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class IPString : NameValueItem
{
	private const int ONEIP = 224;

	public string CurValue;

	public string DefaultValue;

	private bool isModfied = false;

	public bool IsHostIP = false;

	private bool IsValid = true;

	private DelegateCommand _ValueChangedCommand = null;

	public bool IsValidA
	{
		get
		{
			return IsValid;
		}
		set
		{
			if (IsValid != value)
			{
				IsValid = value;
				OnPropertyChanged("IsValidA");
			}
		}
	}

	public bool IsHostIPA
	{
		get
		{
			return IsHostIP;
		}
		set
		{
			IsHostIP = value;
		}
	}

	public string CurValueA
	{
		get
		{
			if (IsHostIP && !isModfied)
			{
				string hostIP = GetHostIP();
				if (!string.IsNullOrEmpty(hostIP))
				{
					CurValue = hostIP;
				}
			}
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				if (IsValidIP(value))
				{
					IsValidA = true;
				}
				isModfied = true;
				CurValue = value;
				OnPropertyChanged("CurValueA");
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

	public override string Value
	{
		get
		{
			return CurValueA;
		}
		set
		{
			CurValueA = value;
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

	private void ValueChangedCmdExecute()
	{
		if (IsUnConnect())
		{
			return;
		}
		if (!IsValidIP(Value))
		{
			IsValidA = false;
		}
		else if (OldValue != Value)
		{
			int num = SetParamUnit();
			OldValue = Value;
			if (num >= 0)
			{
				PreExecuteRun();
			}
		}
	}

	private string GetHostIP()
	{
		string result = "";
		string hostName = Dns.GetHostName();
		IPAddress[] hostAddresses = Dns.GetHostAddresses(hostName);
		IPAddress[] array = hostAddresses;
		foreach (IPAddress iPAddress in array)
		{
			if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
			{
				return iPAddress.ToString();
			}
		}
		return result;
	}

	private bool IsValidIP(string ip)
	{
		if (!string.IsNullOrEmpty(ip))
		{
			string[] array = ip.Split('.');
			if (array.Length != 0)
			{
				string s = array[0];
				int num = int.Parse(s);
				if (num < 224)
				{
					return true;
				}
			}
		}
		return false;
	}
}
