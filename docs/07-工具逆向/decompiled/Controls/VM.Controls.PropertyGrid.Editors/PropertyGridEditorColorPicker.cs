using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorColorPicker : ColorPicker
{
	static PropertyGridEditorColorPicker()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorColorPicker), new FrameworkPropertyMetadata(typeof(PropertyGridEditorColorPicker)));
	}
}
