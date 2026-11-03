using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;

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
