using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.DataForUI;

/// <summary>
/// 图像视图数据类
/// </summary>
public class ImageViewItem : INotifyPropertyChanged
{
	public const int CheckedMaxCount = 4;

	private string _displayName;

	private object _displayValue;

	private object _displayObject;

	private bool _isCheck;

	private bool _isEnable;

	private bool _isBackgroundImage;

	private bool _isCallBackEnable;

	/// <summary>
	/// 显示名称
	/// </summary>
	public string DisplayName
	{
		get
		{
			return _displayName;
		}
		set
		{
			if (_displayName != value)
			{
				_displayName = value;
				NotifyPropertyChanged("DisplayName");
			}
		}
	}

	/// <summary>
	/// 显示值
	/// </summary>
	public object DisplayValue
	{
		get
		{
			return _displayValue;
		}
		set
		{
			if (_displayValue != value)
			{
				_displayValue = value;
				NotifyPropertyChanged("DisplayValue");
			}
		}
	}

	/// <summary>
	/// 显示值
	/// </summary>
	public object DisplayObject
	{
		get
		{
			return _displayObject;
		}
		set
		{
			if (_displayObject != value)
			{
				_displayObject = value;
				NotifyPropertyChanged("DisplayObject");
			}
		}
	}

	/// <summary>
	/// 是否选中
	/// </summary>
	public bool IsCheck
	{
		get
		{
			return _isCheck;
		}
		set
		{
			if (_isCheck != value)
			{
				_isCheck = value;
				NotifyPropertyChanged("IsCheck");
			}
		}
	}

	/// <summary>
	/// 是否使能
	/// </summary>
	public bool IsEnable
	{
		get
		{
			return _isEnable;
		}
		set
		{
			if (_isEnable != value)
			{
				_isEnable = value;
				NotifyPropertyChanged("IsEnable");
			}
		}
	}

	/// <summary>
	/// 是否背景图
	/// </summary>
	public bool IsBackgroundImage
	{
		get
		{
			return _isBackgroundImage;
		}
		set
		{
			if (_isBackgroundImage != value)
			{
				_isBackgroundImage = value;
				NotifyPropertyChanged("IsBackgroundImage");
			}
		}
	}

	/// <summary>
	/// 是否订阅回调
	/// </summary>
	public bool IsCallBackEnable
	{
		get
		{
			return _isCallBackEnable;
		}
		set
		{
			if (_isCallBackEnable != value)
			{
				_isCallBackEnable = value;
				NotifyPropertyChanged("IsCallBackEnable");
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

	public override string ToString()
	{
		return DisplayName;
	}

	public ImageViewItem Clone()
	{
		return new ImageViewItem
		{
			DisplayName = DisplayName,
			DisplayValue = DisplayValue,
			DisplayObject = DisplayObject,
			IsEnable = IsEnable
		};
	}
}
