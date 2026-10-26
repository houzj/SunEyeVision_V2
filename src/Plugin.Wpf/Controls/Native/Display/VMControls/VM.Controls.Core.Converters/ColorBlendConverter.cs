using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Converters;

public class ColorBlendConverter : IValueConverter
{
	private double _blendedColorRatio;

	public double BlendedColorRatio
	{
		get
		{
			return _blendedColorRatio;
		}
		set
		{
			if (value < 0.0 || value > 1.0)
			{
				throw new ArgumentException("BlendedColorRatio must greater than or equal to 0 and lower than or equal to 1 ");
			}
			_blendedColorRatio = value;
		}
	}

	public Color BlendedColor { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null || value.GetType() != typeof(Color))
		{
			return null;
		}
		Color color = (Color)value;
		return new Color
		{
			A = BlendValue(color.A, BlendedColor.A),
			R = BlendValue(color.R, BlendedColor.R),
			G = BlendValue(color.G, BlendedColor.G),
			B = BlendValue(color.B, BlendedColor.B)
		};
	}

	private byte BlendValue(byte original, byte blend)
	{
		double blendedColorRatio = BlendedColorRatio;
		double num = 1.0 - blendedColorRatio;
		double a = (double)(int)original * num + (double)(int)blend * blendedColorRatio;
		a = Math.Round(a);
		a = Math.Min(255.0, Math.Max(0.0, a));
		return System.Convert.ToByte(a);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
