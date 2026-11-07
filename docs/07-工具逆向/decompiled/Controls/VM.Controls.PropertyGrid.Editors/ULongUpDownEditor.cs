using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

internal class ULongUpDownEditor : UpDownEditor<ULongUpDown, ulong?>
{
	protected override ULongUpDown CreateEditor()
	{
		return new PropertyGridEditorULongUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(ulong)));
	}
}
