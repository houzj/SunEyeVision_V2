using System;
using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

public class DateTimeUpDownEditor : UpDownEditor<DateTimeUpDown, DateTime?>
{
	protected override DateTimeUpDown CreateEditor()
	{
		return new PropertyGridEditorDateTimeUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(DateTime)));
	}
}
