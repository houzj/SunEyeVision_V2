using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorDecimalUpDown : DecimalUpDown
{
	static PropertyGridEditorDecimalUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorDecimalUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorDecimalUpDown)));
	}
}
