using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

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
