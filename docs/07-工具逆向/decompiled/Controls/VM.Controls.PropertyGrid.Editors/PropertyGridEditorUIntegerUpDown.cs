using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

internal class PropertyGridEditorUIntegerUpDown : UIntegerUpDown
{
	static PropertyGridEditorUIntegerUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorUIntegerUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorUIntegerUpDown)));
	}
}
