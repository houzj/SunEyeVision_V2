using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorDoubleUpDown : DoubleUpDown
{
	static PropertyGridEditorDoubleUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorDoubleUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorDoubleUpDown)));
	}
}
