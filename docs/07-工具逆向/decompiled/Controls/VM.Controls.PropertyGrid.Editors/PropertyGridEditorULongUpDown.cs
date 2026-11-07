using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

internal class PropertyGridEditorULongUpDown : ULongUpDown
{
	static PropertyGridEditorULongUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorULongUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorULongUpDown)));
	}
}
