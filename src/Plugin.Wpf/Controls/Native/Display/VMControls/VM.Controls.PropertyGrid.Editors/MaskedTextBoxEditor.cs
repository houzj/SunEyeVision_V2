using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

public class MaskedTextBoxEditor : TypeEditor<MaskedTextBox>
{
	public string Mask { get; set; }

	public Type ValueDataType { get; set; }

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.BorderThickness = new Thickness(0.0);
		Editor.ValueDataType = ValueDataType;
		Editor.Mask = Mask;
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = ValueRangeTextBox.ValueProperty;
	}
}
