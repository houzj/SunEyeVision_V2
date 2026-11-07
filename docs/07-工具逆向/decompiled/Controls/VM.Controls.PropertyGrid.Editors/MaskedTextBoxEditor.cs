using System;
using System.Windows;
using VM.Controls.Primitives;

namespace VM.Controls.PropertyGrid.Editors;

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
