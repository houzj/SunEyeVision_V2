using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

public class ByteUpDownEditor : UpDownEditor<ByteUpDown, byte?>
{
	protected override ByteUpDown CreateEditor()
	{
		return new PropertyGridEditorByteUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(byte)));
	}
}
