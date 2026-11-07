using System;
using System.Linq;
using System.Windows.Input;
using HikPcUI.ImageView;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class CircleSelector : NameValueItem
{
	private bool isGettingValue = false;

	public string CurValue;

	public string DefaultValue;

	private HikCircleMesRoiParameter _circle = null;

	public bool _ifCircleMesA = false;

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
				if (!isGettingValue)
				{
					SetParamUnit();
				}
			}
		}
	}

	public HikCircleMesRoiParameter Circle
	{
		get
		{
			return _circle;
		}
		set
		{
			if (value != _circle)
			{
				_circle = value;
				OnPropertyChanged("Circle");
				UpdateCurValue(_circle);
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

	public bool IfCircleMesA
	{
		get
		{
			return _ifCircleMesA;
		}
		set
		{
			if (_ifCircleMesA != value)
			{
				_ifCircleMesA = value;
				OnPropertyChanged("IfCircleMesA");
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
					imageView.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Remove(imageView.OnRoiCountChanged, new HikImageView.RoiCountChanged(RoiCountChanged));
					HikImageView imageView2 = ImageView;
					imageView2.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Remove(imageView2.OnRoiParamChange, new HikImageView.RoiParamChange(RoiParamChanged));
				}
				_imageView = value;
				OnPropertyChanged("ImageView");
				if (ImageView != null)
				{
					HikImageView imageView3 = ImageView;
					imageView3.OnRoiCountChanged = (HikImageView.RoiCountChanged)Delegate.Combine(imageView3.OnRoiCountChanged, new HikImageView.RoiCountChanged(RoiCountChanged));
					HikImageView imageView4 = ImageView;
					imageView4.OnRoiParamChange = (HikImageView.RoiParamChange)Delegate.Combine(imageView4.OnRoiParamChange, new HikImageView.RoiParamChange(RoiParamChanged));
				}
				if (ImageView == null)
				{
					Circle = null;
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

	public event EventHandler IsUsingEvent;

	public override int GetParamUnit()
	{
		isGettingValue = true;
		int paramUnit = base.GetParamUnit();
		isGettingValue = false;
		return paramUnit;
	}

	private void RoiCountChanged(object Roi, EventArgs e = null)
	{
		ImageView.UnPrepareRawRoi();
		IfCircleMesA = false;
		RoiParamChanged(Roi);
	}

	private void RoiParamChanged(object Roi)
	{
		if (Roi is HikCircleMesRoiParameter circle)
		{
			Circle = circle;
		}
	}

	private void UpdateCurValue(HikCircleMesRoiParameter circle)
	{
		if (circle != null)
		{
			CurValueA = $"{circle.CenterPoint.X} {circle.CenterPoint.Y} {circle.Radius}";
		}
	}

	private void IsUsingCmdExecute(string e)
	{
		IsUsingEvent?.Invoke(this, new EventArgs());
		ImageView.UnPrepareRawRoi();
		ImageView.RoiList.Remove(ImageView.RoiList.FirstOrDefault((object p) => p is HikCircleMesRoiParameter));
		ImageView.PrepareRawRoi(RoiUIType.HikCircleMesRoi);
	}
}
