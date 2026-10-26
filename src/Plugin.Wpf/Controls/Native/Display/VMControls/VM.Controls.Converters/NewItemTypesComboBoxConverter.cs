using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Converters;

public class NewItemTypesComboBoxConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length != 2)
		{
			throw new ArgumentException("The 'values' argument should contain 2 objects.");
		}
		if (values[1] != null)
		{
			if (!values[1].GetType().IsGenericType || (object)values[1].GetType().GetGenericArguments().First()
				.GetType() == null)
			{
				throw new ArgumentException("The 'value' argument is not of the correct type.");
			}
			return values[1];
		}
		if (values[0] != null)
		{
			if ((object)values[0].GetType() == null)
			{
				throw new ArgumentException("The 'value' argument is not of the correct type.");
			}
			List<Type> list = new List<Type>();
			Type listItemType = ListUtilities.GetListItemType((Type)values[0]);
			if (listItemType != null)
			{
				list.Add(listItemType);
			}
			return list;
		}
		return null;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
