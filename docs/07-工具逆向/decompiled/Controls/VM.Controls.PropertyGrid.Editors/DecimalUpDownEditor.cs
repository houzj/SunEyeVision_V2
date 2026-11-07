using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

public class DecimalUpDownEditor : UpDownEditor<DecimalUpDown, decimal?>
{
	protected override DecimalUpDown CreateEditor()
	{
		return new PropertyGridEditorDecimalUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(decimal)));
	}
}
