using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_SpectrumDisplay", Type = typeof(Rectangle))]
public class ColorSpectrumSlider : Slider
{
	private const string PART_SpectrumDisplay = "PART_SpectrumDisplay";

	private Rectangle _spectrumDisplay;

	private LinearGradientBrush _pickerBrush;

	public static readonly DependencyProperty SelectedColorProperty;

	public Color SelectedColor
	{
		get
		{
			return (Color)GetValue(SelectedColorProperty);
		}
		set
		{
			SetValue(SelectedColorProperty, value);
		}
	}

	static ColorSpectrumSlider()
	{
		SelectedColorProperty = DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorSpectrumSlider), new PropertyMetadata(Colors.Transparent));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorSpectrumSlider), new FrameworkPropertyMetadata(typeof(ColorSpectrumSlider)));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		_spectrumDisplay = (Rectangle)GetTemplateChild("PART_SpectrumDisplay");
		CreateSpectrum();
		OnValueChanged(double.NaN, Value);
	}

	protected override void OnValueChanged(double oldValue, double newValue)
	{
		base.OnValueChanged(oldValue, newValue);
		Color selectedColor = ColorUtilities.ConvertHsvToRgb(360.0 - newValue, 1.0, 1.0);
		SelectedColor = selectedColor;
	}

	private void CreateSpectrum()
	{
		_pickerBrush = new LinearGradientBrush();
		_pickerBrush.StartPoint = new Point(0.5, 0.0);
		_pickerBrush.EndPoint = new Point(0.5, 1.0);
		_pickerBrush.ColorInterpolationMode = ColorInterpolationMode.SRgbLinearInterpolation;
		List<Color> list = ColorUtilities.GenerateHsvSpectrum();
		double num = 1.0 / (double)list.Count;
		int i;
		for (i = 0; i < list.Count; i++)
		{
			_pickerBrush.GradientStops.Add(new GradientStop(list[i], (double)i * num));
		}
		_pickerBrush.GradientStops[i - 1].Offset = 1.0;
		if (_spectrumDisplay != null)
		{
			_spectrumDisplay.Fill = _pickerBrush;
		}
	}
}
