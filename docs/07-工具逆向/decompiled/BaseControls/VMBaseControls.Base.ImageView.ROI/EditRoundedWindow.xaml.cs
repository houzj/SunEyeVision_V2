using System;
using System.Windows;
using System.Windows.Markup;
using VM.Controls;

namespace VMBaseControls.Base.ImageView.ROI;

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
