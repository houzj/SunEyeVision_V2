using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Release.Convert;

public class BindingPropertyConvert : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		ContentControl contentControl = values[1] as ContentControl;
		BaseValueSource baseValueSource = DependencyPropertyHelper.GetValueSource(contentControl, Control.ForegroundProperty).BaseValueSource;
		BaseValueSource baseValueSource2 = DependencyPropertyHelper.GetValueSource(contentControl, Control.BackgroundProperty).BaseValueSource;
		BaseValueSource baseValueSource3 = DependencyPropertyHelper.GetValueSource(contentControl, Control.BorderThicknessProperty).BaseValueSource;
		BaseValueSource baseValueSource4 = DependencyPropertyHelper.GetValueSource(contentControl, Control.BorderBrushProperty).BaseValueSource;
		ContentControl contentControl2 = values[0] as ContentControl;
		if (baseValueSource != BaseValueSource.Default && baseValueSource != BaseValueSource.Inherited)
		{
			contentControl2.SetBinding(Control.ForegroundProperty, new Binding("Foreground")
			{
				Source = contentControl,
				Mode = BindingMode.TwoWay
			});
		}
		else
		{
			contentControl.SetBinding(Control.ForegroundProperty, new Binding("Foreground")
			{
				Source = contentControl2,
				Mode = BindingMode.TwoWay
			});
		}
		if (baseValueSource2 != BaseValueSource.Default)
		{
			contentControl2.SetBinding(Control.BackgroundProperty, new Binding("Background")
			{
				Source = contentControl,
				Mode = BindingMode.TwoWay
			});
		}
		else
		{
			contentControl.SetBinding(Control.BackgroundProperty, new Binding("Background")
			{
				Source = contentControl2,
				Mode = BindingMode.TwoWay
			});
		}
		if (baseValueSource3 != BaseValueSource.Default)
		{
			contentControl2.SetBinding(Control.BorderThicknessProperty, new Binding("BorderThickness")
			{
				Source = contentControl,
				Mode = BindingMode.TwoWay
			});
		}
		else
		{
			contentControl.SetBinding(Control.BorderThicknessProperty, new Binding("BorderThickness")
			{
				Source = contentControl2,
				Mode = BindingMode.TwoWay
			});
		}
		if (baseValueSource4 != BaseValueSource.Default)
		{
			contentControl2.SetBinding(Control.BorderBrushProperty, new Binding("BorderBrush")
			{
				Source = contentControl,
				Mode = BindingMode.TwoWay
			});
		}
		else
		{
			contentControl.SetBinding(Control.BorderBrushProperty, new Binding("BorderBrush")
			{
				Source = contentControl2,
				Mode = BindingMode.TwoWay
			});
		}
		contentControl = null;
		contentControl2 = null;
		return "";
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
