using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

public class ShortUpDownEditor : UpDownEditor<ShortUpDown, short?>
{
	protected override ShortUpDown CreateEditor()
	{
		return new PropertyGridEditorShortUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(short)));
	}
}
