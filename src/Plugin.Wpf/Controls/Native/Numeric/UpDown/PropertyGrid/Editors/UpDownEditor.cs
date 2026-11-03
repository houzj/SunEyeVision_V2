using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;

public class UpDownEditor<TEditor, TType> : TypeEditor<TEditor> where TEditor : UpDownBase<TType>, new()
{
	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.TextAlignment = TextAlignment.Left;
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = UpDownBase<TType>.ValueProperty;
	}

	internal void SetMinMaxFromRangeAttribute(PropertyDescriptor propertyDescriptor, TypeConverter converter)
	{
		if (propertyDescriptor != null)
		{
			RangeAttribute attribute = PropertyGridUtilities.GetAttribute<RangeAttribute>(propertyDescriptor);
			if (attribute != null)
			{
				Editor.Maximum = (TType)converter.ConvertFrom(attribute.Maximum.ToString());
				Editor.Minimum = (TType)converter.ConvertFrom(attribute.Minimum.ToString());
			}
		}
	}
}
