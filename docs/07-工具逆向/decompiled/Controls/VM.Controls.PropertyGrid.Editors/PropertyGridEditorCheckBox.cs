using System.Windows;
using System.Windows.Controls;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorCheckBox : CheckBox
{
	static PropertyGridEditorCheckBox()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorCheckBox), new FrameworkPropertyMetadata(typeof(PropertyGridEditorCheckBox)));
	}
}
