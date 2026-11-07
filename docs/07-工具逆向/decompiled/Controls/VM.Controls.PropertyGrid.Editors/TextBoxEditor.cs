using System.ComponentModel.DataAnnotations;
using System.Windows.Controls;

namespace VM.Controls.PropertyGrid.Editors;

public class TextBoxEditor : TypeEditor<WatermarkTextBox>
{
	protected override WatermarkTextBox CreateEditor()
	{
		return new PropertyGridEditorTextBox();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		DisplayAttribute attribute = PropertyGridUtilities.GetAttribute<DisplayAttribute>(propertyItem.PropertyDescriptor);
		if (attribute != null)
		{
			Editor.Watermark = attribute.GetPrompt();
		}
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = TextBox.TextProperty;
	}
}
