using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorSingleUpDown : SingleUpDown
{
	static PropertyGridEditorSingleUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorSingleUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorSingleUpDown)));
	}
}
