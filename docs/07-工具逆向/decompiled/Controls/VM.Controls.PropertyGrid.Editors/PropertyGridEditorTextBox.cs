using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorTextBox : WatermarkTextBox
{
	static PropertyGridEditorTextBox()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorTextBox), new FrameworkPropertyMetadata(typeof(PropertyGridEditorTextBox)));
	}
}
