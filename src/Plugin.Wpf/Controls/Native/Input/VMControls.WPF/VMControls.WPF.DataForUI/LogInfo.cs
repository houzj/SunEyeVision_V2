using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.DataForUI;

public class LogInfo : INotifyPropertyChanged
{
	private ulong _RowIndex;

	private string _Content;

	private string _AddTime;

	public ulong RowIndex
	{
		get
		{
			return _RowIndex;
		}
		set
		{
			if (_RowIndex != value)
			{
				_RowIndex = value;
				NotifyPropertyChanged("RowIndex");
			}
		}
	}

	public string Content
	{
		get
		{
			return _Content;
		}
		set
		{
			if (_Content != value)
			{
				_Content = value;
				NotifyPropertyChanged("Content");
			}
		}
	}

	public string AddTime
	{
		get
		{
			return _AddTime;
		}
		set
		{
			if (_AddTime != value)
			{
				_AddTime = value;
				NotifyPropertyChanged("AddTime");
			}
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
