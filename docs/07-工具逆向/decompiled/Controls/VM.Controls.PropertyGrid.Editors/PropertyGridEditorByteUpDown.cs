using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorByteUpDown : ByteUpDown
{
	static PropertyGridEditorByteUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorByteUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorByteUpDown)));
	}
}
