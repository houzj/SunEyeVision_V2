using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class LogContect : INotifyPropertyChanged
{
	private string _loginfotime = "";

	private string _logtype = "";

	private string _logevent = "";

	public string LogInfoTime
	{
		get
		{
			return _loginfotime;
		}
		set
		{
			if (_loginfotime != value)
			{
				_loginfotime = value;
			}
			NotifyPropertyChanged("LogInfoTime");
		}
	}

	public string LogType
	{
		get
		{
			return _logtype;
		}
		set
		{
			if (_logtype != value)
			{
				_logtype = value;
			}
			NotifyPropertyChanged("LogType");
		}
	}

	public string LogEvent
	{
		get
		{
			return _logevent;
		}
		set
		{
			if (_logevent != value)
			{
				_logevent = value;
			}
			NotifyPropertyChanged("LogEvent");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
