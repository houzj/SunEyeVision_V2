using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public class CameraInfo : INotifyPropertyChanged
{
	private bool isCurrentCamera = false;

	private bool _isShow;

	public string Type { get; set; }

	public bool IsCurrentCamera
	{
		get
		{
			return isCurrentCamera;
		}
		set
		{
			isCurrentCamera = value;
			OnPropertyChanged("isCurrentCamera");
		}
	}

	public bool IsShow
	{
		get
		{
			return _isShow;
		}
		set
		{
			_isShow = value;
			OnPropertyChanged("IsShow");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	/// <summary>
	/// 通知属性变化
	/// </summary>
	/// <param name="name"></param>
	public virtual void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}
}
