using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;

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
