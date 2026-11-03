using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;

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
