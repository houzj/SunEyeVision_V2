using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;

public class CheckBoxEditor : TypeEditor<CheckBox>
{
	protected override CheckBox CreateEditor()
	{
		return new PropertyGridEditorCheckBox();
	}

	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = ToggleButton.IsCheckedProperty;
	}
}
