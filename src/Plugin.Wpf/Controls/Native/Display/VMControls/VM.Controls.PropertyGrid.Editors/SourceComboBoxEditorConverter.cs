using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

internal class SourceComboBoxEditorConverter : IValueConverter
{
	private TypeConverter _typeConverter;

	internal SourceComboBoxEditorConverter(TypeConverter typeConverter)
	{
		_typeConverter = typeConverter;
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (_typeConverter != null && _typeConverter.CanConvertTo(typeof(string)))
		{
			return _typeConverter.ConvertTo(value, typeof(string));
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (_typeConverter != null && _typeConverter.CanConvertFrom(value.GetType()))
		{
			return _typeConverter.ConvertFrom(value);
		}
		return value;
	}
}
