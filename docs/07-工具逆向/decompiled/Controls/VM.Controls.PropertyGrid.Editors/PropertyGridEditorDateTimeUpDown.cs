using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorDateTimeUpDown : DateTimeUpDown
{
	static PropertyGridEditorDateTimeUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorDateTimeUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorDateTimeUpDown)));
	}
}
