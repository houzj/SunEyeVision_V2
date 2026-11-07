using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class ColorEditor : TypeEditor<ColorPicker>
{
	protected override ColorPicker CreateEditor()
	{
		return new PropertyGridEditorColorPicker();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.BorderThickness = new Thickness(0.0);
		Editor.DisplayColorAndName = true;
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = ColorPicker.SelectedColorProperty;
	}
}
