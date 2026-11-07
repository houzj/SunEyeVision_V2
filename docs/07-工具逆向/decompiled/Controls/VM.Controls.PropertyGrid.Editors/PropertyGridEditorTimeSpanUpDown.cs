using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorTimeSpanUpDown : TimeSpanUpDown
{
	static PropertyGridEditorTimeSpanUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorTimeSpanUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorTimeSpanUpDown)));
	}
}
