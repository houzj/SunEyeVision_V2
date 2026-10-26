using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class LogBindingModel : INotifyPropertyChanged
{
	public ObservableConcurrentQueue<LogContect> _logInfoShow = new ObservableConcurrentQueue<LogContect>();

	public ObservableConcurrentQueue<LogContect> LogInfoShow
	{
		get
		{
			return _logInfoShow;
		}
		set
		{
			_logInfoShow = value;
			NotifyPropertyChanged("LogInfoShow");
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
