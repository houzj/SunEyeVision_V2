using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Converters;

public class SelectedObjectConverter : IValueConverter
{
	private const string ValidParameterMessage = "parameter must be one of the following strings: 'Type', 'TypeName', 'SelectedObjectName'";

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (parameter == null)
		{
			throw new ArgumentNullException("parameter");
		}
		if (!(parameter is string))
		{
			throw new ArgumentException("parameter must be one of the following strings: 'Type', 'TypeName', 'SelectedObjectName'");
		}
		if (CompareParam(parameter, "Type"))
		{
			return ConvertToType(value, culture);
		}
		if (CompareParam(parameter, "TypeName"))
		{
			return ConvertToTypeName(value, culture);
		}
		if (CompareParam(parameter, "SelectedObjectName"))
		{
			return ConvertToSelectedObjectName(value, culture);
		}
		throw new ArgumentException("parameter must be one of the following strings: 'Type', 'TypeName', 'SelectedObjectName'");
	}

	private bool CompareParam(object parameter, string parameterValue)
	{
		return string.Compare((string)parameter, parameterValue, ignoreCase: true) == 0;
	}

	private object ConvertToType(object value, CultureInfo culture)
	{
		return value?.GetType();
	}

	private object ConvertToTypeName(object value, CultureInfo culture)
	{
		if (value == null)
		{
			return string.Empty;
		}
		Type type = value.GetType();
		if (type.GetInterface("ICustomTypeProvider", ignoreCase: true) != null)
		{
			type = type.GetMethod("GetCustomType").Invoke(value, null) as Type;
		}
		DisplayNameAttribute displayNameAttribute = type.GetCustomAttributes(inherit: false).OfType<DisplayNameAttribute>().FirstOrDefault();
		if (displayNameAttribute != null)
		{
			return displayNameAttribute.DisplayName;
		}
		return type.Name;
	}

	private object ConvertToSelectedObjectName(object value, CultureInfo culture)
	{
		if (value == null)
		{
			return string.Empty;
		}
		PropertyInfo[] properties = value.GetType().GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			if (propertyInfo.Name == "Name")
			{
				return propertyInfo.GetValue(value, null);
			}
		}
		return string.Empty;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
