using System;
using System.ComponentModel;

namespace VM.Controls.PropertyGrid.Editors;

public class TimeSpanUpDownEditor : UpDownEditor<TimeSpanUpDown, TimeSpan?>
{
	protected override TimeSpanUpDown CreateEditor()
	{
		return new PropertyGridEditorTimeSpanUpDown();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		base.SetControlProperties(propertyItem);
		SetMinMaxFromRangeAttribute(propertyItem.PropertyDescriptor, TypeDescriptor.GetConverter(typeof(TimeSpan)));
	}
}
