using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;

internal class UShortUpDownEditor : UpDownEditor<UShortUpDown, ushort?>
{
	protected override UShortUpDown CreateEditor()
	{
		return new PropertyGridEditorUShortUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(ushort)));
	}
}
