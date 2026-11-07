using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorLongUpDown : LongUpDown
{
	static PropertyGridEditorLongUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorLongUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorLongUpDown)));
	}
}
