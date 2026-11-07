using System.Windows;
using System.Windows.Controls;

namespace VM.Controls.PropertyGrid.Editors;

public class TextBlockEditor : TypeEditor<TextBlock>
{
	protected override TextBlock CreateEditor()
	{
		return new PropertyGridEditorTextBlock();
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = TextBlock.TextProperty;
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
	}
}
