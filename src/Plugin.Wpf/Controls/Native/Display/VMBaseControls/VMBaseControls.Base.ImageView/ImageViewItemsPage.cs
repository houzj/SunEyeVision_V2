using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public class ImageViewItemsPage : ListView
{
	public static readonly DependencyProperty IsHitTestVisibleOfHostProperty = DependencyProperty.Register("IsHitTestVisibleOfHost", typeof(bool), typeof(ImageViewItemsPage), new PropertyMetadata(true));

	public static readonly DependencyProperty ThicknessScaleProperty = DependencyProperty.Register("ThicknessScale", typeof(double), typeof(ImageViewItemsPage), new PropertyMetadata(1.0));

	public static readonly DependencyProperty ControlToImageRadioProperty = DependencyProperty.Register("ControlToImageRadio", typeof(double), typeof(ImageViewItemsPage), new PropertyMetadata(1.0));

	public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register("ImageWidth", typeof(int), typeof(ImageViewItemsPage), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register("ImageHeight", typeof(int), typeof(ImageViewItemsPage), new PropertyMetadata(0));

	public static readonly DependencyProperty CopyRoiCommandProperty = DependencyProperty.Register("CopyRoiCommand", typeof(ICommand), typeof(ImageViewItemsPage), new PropertyMetadata(null));

	public static readonly DependencyProperty DeleteRoiCommandProperty = DependencyProperty.Register("DeleteRoiCommand", typeof(ICommand), typeof(ImageViewItemsPage), new PropertyMetadata(null));

	private Canvas Child;

	public bool IsHitTestVisibleOfHost
	{
		get
		{
			return (bool)GetValue(IsHitTestVisibleOfHostProperty);
		}
		set
		{
			SetValue(IsHitTestVisibleOfHostProperty, value);
		}
	}

	public double ThicknessScale
	{
		get
		{
			return (double)GetValue(ThicknessScaleProperty);
		}
		set
		{
			SetValue(ThicknessScaleProperty, value);
		}
	}

	public double ControlToImageRadio
	{
		get
		{
			return (double)GetValue(ControlToImageRadioProperty);
		}
		set
		{
			SetValue(ControlToImageRadioProperty, value);
		}
	}

	public int ImageWidth
	{
		get
		{
			return (int)GetValue(ImageWidthProperty);
		}
		set
		{
			SetValue(ImageWidthProperty, value);
		}
	}

	public int ImageHeight
	{
		get
		{
			return (int)GetValue(ImageHeightProperty);
		}
		set
		{
			SetValue(ImageHeightProperty, value);
		}
	}

	public ICommand CopyRoiCommand
	{
		get
		{
			return (ICommand)GetValue(CopyRoiCommandProperty);
		}
		set
		{
			SetValue(CopyRoiCommandProperty, value);
		}
	}

	public ICommand DeleteRoiCommand
	{
		get
		{
			return (ICommand)GetValue(DeleteRoiCommandProperty);
		}
		set
		{
			SetValue(DeleteRoiCommandProperty, value);
		}
	}

	public Canvas SubstituteHost => Child;

	protected override void OnIsMouseCapturedChanged(DependencyPropertyChangedEventArgs e)
	{
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		Child = Template.FindName("Bd", this) as Canvas;
	}
}
