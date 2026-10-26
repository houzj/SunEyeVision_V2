using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

public class ByteUpDownEditor : UpDownEditor<ByteUpDown, byte?>
{
	protected override ByteUpDown CreateEditor()
	{
		return new PropertyGridEditorByteUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(byte)));
	}
}
