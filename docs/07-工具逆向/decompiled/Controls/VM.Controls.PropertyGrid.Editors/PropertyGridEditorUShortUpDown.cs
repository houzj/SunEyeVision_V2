using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

internal class PropertyGridEditorUShortUpDown : UShortUpDown
{
	static PropertyGridEditorUShortUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorUShortUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorUShortUpDown)));
	}
}
