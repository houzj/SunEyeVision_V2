using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

internal class UIntegerUpDownEditor : UpDownEditor<UIntegerUpDown, uint?>
{
	protected override UIntegerUpDown CreateEditor()
	{
		return new PropertyGridEditorUIntegerUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(uint)));
	}
}
