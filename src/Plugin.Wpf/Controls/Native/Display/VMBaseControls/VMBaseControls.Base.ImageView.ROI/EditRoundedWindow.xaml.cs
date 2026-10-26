using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Markup;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class EditRoundedWindow : BaseWindow, IComponentConnector
{
	public event Action<double> StartValueChanged;

	public EditRoundedWindow(double maxCornerRadius, double startValue)
	{
		InitializeComponent();
		sliderArrangeRounded.Maximum = maxCornerRadius;
		sliderArrangeRounded.StartValue = startValue;
		sliderArrangeRounded.OnStartValueChange += (object sender, RoutedPropertyChangedEventArgs<double> e) =>
		{
			double startValue2 = sliderArrangeRounded.StartValue;
			StartValueChanged?.Invoke(startValue2);
		};
	}
}
