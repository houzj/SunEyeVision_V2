using System;
using System.Windows.Input;
using System.Windows.Media;
using Apps.XmlParser.ParamTab;
using HikPcUI.ImageView;
using Mvvm.Commands;

namespace VisionMaster.ParamTab;

public class RectangleSelector : NameValueItem
{
	public OnRoiChanged onRoiChanged;

	public string CurValue;

	public string DefaultValue;

	public bool _BoxA = false;

	private HikImageView _imageView = null;

	private DelegateCommand<string> _ValueChangedCommand;

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (value != CurValue)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
				SetParamUnit();
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
			OnPropertyChanged("DefaultValueA");
		}
	}

	public bool BoxA
	{
		get
		{
			return _BoxA;
		}
		set
		{
			if (_BoxA != value)
			{
				_BoxA = value;
				OnPropertyChanged("BoxA");
				if (_imageView == null)
				{
				}
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
			CurValueA = value;
		}
	}

	public HikImageView ImageView
	{
		get
		{
			return _imageView;
		}
		set
		{
			if (_imageView != value)
			{
				if (ImageView != null)
				{
					HikImageView imageView = ImageView;
					imageView.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(imageView.OnRoiParamChange, new HikImageView.RoiParamChange(RoiParamChanged));
				}
				_imageView = value;
				OnPropertyChanged("ImageView");
				if (ImageView != null)
				{
					HikImageView imageView2 = ImageView;
					imageView2.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(imageView2.OnRoiParamChange, new HikImageView.RoiParamChange(RoiParamChanged));
				}
			}
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand<string>((Action<string>)IsUsingCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public RectangleSelector()
	{
		AccessMode = "RW";
	}

	private void RoiParamChanged(object Roi)
	{
		if (Roi != null)
		{
			RectChanged(Roi);
			if (_BoxA)
			{
				ImageView.UnPrepareRawRoi();
				Update();
				ImageView.RoiList.Remove(Roi);
				BoxA = false;
			}
		}
	}

	private void Update()
	{
		if (!(ImageView.ImageDataSource.PixelFormat != PixelFormats.Rgb24))
		{
			onRoiChanged?.Invoke(Value);
		}
	}

	private void RectChanged(object Roi)
	{
		HikCoverRoiParameter hikCoverRoiParameter;
		if (Roi != null && (hikCoverRoiParameter = Roi as HikCoverRoiParameter) == null)
		{
		}
	}

	private void IsUsingCmdExecute(string e)
	{
		ImageView.PrepareRawRoi(RoiUIType.HikCoverRoi);
	}
}
