using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;

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
