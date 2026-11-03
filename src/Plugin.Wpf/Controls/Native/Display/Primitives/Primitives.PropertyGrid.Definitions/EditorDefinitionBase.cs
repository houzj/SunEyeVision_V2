using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Definitions;

public abstract class EditorDefinitionBase : PropertyDefinitionBase
{
	internal EditorDefinitionBase()
	{
	}

	internal FrameworkElement GenerateEditingElementInternal(PropertyItemBase propertyItem)
	{
		return GenerateEditingElement(propertyItem);
	}

	protected virtual FrameworkElement GenerateEditingElement(PropertyItemBase propertyItem)
	{
		return null;
	}

	internal void UpdateProperty(FrameworkElement element, DependencyProperty elementProp, DependencyProperty definitionProperty)
	{
		object value = GetValue(definitionProperty);
		object obj = ReadLocalValue(definitionProperty);
		object value2 = element.GetValue(elementProp);
		bool flag = false;
		if (obj != DependencyProperty.UnsetValue)
		{
			if (value2 != null && value != null)
			{
				flag = ((value2.GetType().IsValueType && value.GetType().IsValueType) ? value2.Equals(value) : (value == element.GetValue(elementProp)));
			}
			if (!flag)
			{
				element.SetValue(elementProp, value);
			}
			else
			{
				element.ClearValue(elementProp);
			}
		}
	}
}
