using System.Windows;
using System.Windows.Controls;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorTextBlock : TextBlock
{
	static PropertyGridEditorTextBlock()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorTextBlock), new FrameworkPropertyMetadata(typeof(PropertyGridEditorTextBlock)));
	}
}
