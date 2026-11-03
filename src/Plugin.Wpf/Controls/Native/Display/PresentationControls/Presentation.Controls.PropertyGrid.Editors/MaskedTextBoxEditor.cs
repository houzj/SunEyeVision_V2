using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

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
