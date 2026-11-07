using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

public class SingleUpDownEditor : UpDownEditor<SingleUpDown, float?>
{
	protected override SingleUpDown CreateEditor()
	{
		return new PropertyGridEditorSingleUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		Editor.AllowInputSpecialValues = AllowedSpecialValues.Any;
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(float)));
	}
}
