using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

internal class PropertyGridEditorSByteUpDown : SByteUpDown
{
	static PropertyGridEditorSByteUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorSByteUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorSByteUpDown)));
	}
}
