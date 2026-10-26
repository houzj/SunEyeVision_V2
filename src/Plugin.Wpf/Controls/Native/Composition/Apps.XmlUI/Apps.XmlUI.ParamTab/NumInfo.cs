using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public class NumInfo : INotifyPropertyChanged
{
	private string _StrNum;

	public bool _IsNum = false;

	public string StrNum
	{
		get
		{
			return _StrNum;
		}
		set
		{
			_StrNum = value;
			RaisePropertyChanged("StrNum");
		}
	}

	public bool IsNum
	{
		get
		{
			return _IsNum;
		}
		set
		{
			_IsNum = value;
			RaisePropertyChanged("IsNum");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void RaisePropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
