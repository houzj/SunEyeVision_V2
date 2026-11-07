using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

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
