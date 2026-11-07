using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

public class LongUpDownEditor : UpDownEditor<LongUpDown, long?>
{
	protected override LongUpDown CreateEditor()
	{
		return new PropertyGridEditorLongUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(long)));
	}
}
