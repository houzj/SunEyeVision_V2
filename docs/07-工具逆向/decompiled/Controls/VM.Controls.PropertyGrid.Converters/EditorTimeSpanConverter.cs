using System;
using System.Globalization;
using System.Windows.Data;

namespace VM.Controls.PropertyGrid.Converters;

public sealed class EditorTimeSpanConverter : IValueConverter
{
	public bool AllowNulls { get; set; }

	object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (AllowNulls && value == null)
		{
			return null;
		}
		TimeSpan timeSpan = ((value != null) ? ((TimeSpan)value) : TimeSpan.Zero);
		return DateTime.Today + timeSpan;
	}

	object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (AllowNulls && value == null)
		{
			return null;
		}
		return (value != null) ? ((DateTime)value).TimeOfDay : TimeSpan.Zero;
	}
}
