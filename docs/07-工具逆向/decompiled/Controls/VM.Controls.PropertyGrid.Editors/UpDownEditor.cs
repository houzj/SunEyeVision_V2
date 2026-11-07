using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using VM.Controls.Primitives;

namespace VM.Controls.PropertyGrid.Editors;

public class UpDownEditor<TEditor, TType> : TypeEditor<TEditor> where TEditor : UpDownBase<TType>, new()
{
	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.TextAlignment = TextAlignment.Left;
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = UpDownBase<TType>.ValueProperty;
	}

	internal void SetMinMaxFromRangeAttribute(PropertyDescriptor propertyDescriptor, TypeConverter converter)
	{
		if (propertyDescriptor != null)
		{
			RangeAttribute attribute = PropertyGridUtilities.GetAttribute<RangeAttribute>(propertyDescriptor);
			if (attribute != null)
			{
				Editor.Maximum = (TType)converter.ConvertFrom(attribute.Maximum.ToString());
				Editor.Minimum = (TType)converter.ConvertFrom(attribute.Minimum.ToString());
			}
		}
	}
}
