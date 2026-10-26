using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

public class EnumComboBoxEditor : ComboBoxEditor
{
	protected override IEnumerable CreateItemsSource(PropertyItem propertyItem)
	{
		return GetValues(propertyItem.PropertyType);
	}

	private static object[] GetValues(Type enumType)
	{
		List<object> list = new List<object>();
		if (enumType != null)
		{
			foreach (FieldInfo item in from x in enumType.GetFields()
				where x.IsLiteral
				select x)
			{
				object[] customAttributes = item.GetCustomAttributes(typeof(BrowsableAttribute), inherit: false);
				if (customAttributes.Length != 1 || ((BrowsableAttribute)customAttributes[0]).Browsable)
				{
					list.Add(item.GetValue(enumType));
				}
			}
		}
		return list.ToArray();
	}
}
