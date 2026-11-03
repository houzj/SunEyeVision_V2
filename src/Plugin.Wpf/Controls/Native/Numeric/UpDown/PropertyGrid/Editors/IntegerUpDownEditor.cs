using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;

public class IntegerUpDownEditor : UpDownEditor<IntegerUpDown, int?>
{
	protected override IntegerUpDown CreateEditor()
	{
		return new PropertyGridEditorIntegerUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(int)));
	}
}
