using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

internal class UShortUpDownEditor : UpDownEditor<UShortUpDown, ushort?>
{
	protected override UShortUpDown CreateEditor()
	{
		return new PropertyGridEditorUShortUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(ushort)));
	}
}
