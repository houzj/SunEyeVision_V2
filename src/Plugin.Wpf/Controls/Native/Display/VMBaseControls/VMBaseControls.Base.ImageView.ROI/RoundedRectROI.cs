using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Input;
using Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IRoundedRectROI))]
public class RoundedRectROI : RectROI
{
	public static readonly DependencyProperty CornerRadiusProperty;

	private double MaxCornerRadius { get; set; }

	public CornerRadius CornerRadius
	{
		get
		{
			return (CornerRadius)GetValue(CornerRadiusProperty);
		}
		set
		{
			SetValue(CornerRadiusProperty, value);
		}
	}

	public ICommand OpenRoundedSettingCommand { get; set; }

	static RoundedRectROI()
	{
		CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(RoundedRectROI), new PropertyMetadata(new CornerRadius(-1.0), CornerRadiusPropertyChangedCallback));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(RoundedRectROI), new FrameworkPropertyMetadata(typeof(RoundedRectROI)));
	}

	public RoundedRectROI()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected Obj, but got Unknown
		OpenRoundedSettingCommand = (ICommand)new DelegateCommand((Action)ExecuteOpenRoundedSettingCommand);
		SizeChanged += RoundedRectROI_SizeChanged;
	}

	private static void CornerRadiusPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is RoundedRectROI { DataContext: IRoundedRectROI dataContext } roundedRectROI)
		{
			dataContext.CornerRadius = roundedRectROI.CornerRadius.BottomLeft / roundedRectROI.ControlToImageRadio;
		}
	}

	private void ExecuteOpenRoundedSettingCommand()
	{
		EditRoundedWindow editRoundedWindow = new EditRoundedWindow(MaxCornerRadius, CornerRadius.BottomLeft / ControlToImageRadio);
		editRoundedWindow.StartValueChanged += (double value) =>
		{
			CornerRadius = new CornerRadius(value * ControlToImageRadio);
		};
		editRoundedWindow.ShowDialog();
	}

	private void RoundedRectROI_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		MaxCornerRadius = RectWidth * 0.5;
		if (RectWidth > RectHeight)
		{
			MaxCornerRadius = RectHeight * 0.5;
		}
		if (CornerRadius.BottomLeft > MaxCornerRadius * ControlToImageRadio || CornerRadius.BottomLeft < 0.0)
		{
			CornerRadius = new CornerRadius(MaxCornerRadius * ControlToImageRadio);
		}
	}
}
