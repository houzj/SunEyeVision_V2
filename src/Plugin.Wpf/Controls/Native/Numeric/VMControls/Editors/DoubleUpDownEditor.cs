using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

public class DoubleUpDownEditor : UpDownEditor<DoubleUpDown, double?>
{
	protected override DoubleUpDown CreateEditor()
	{
		return new PropertyGridEditorDoubleUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		Editor.AllowInputSpecialValues = AllowedSpecialValues.Any;
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(double)));
	}
}
