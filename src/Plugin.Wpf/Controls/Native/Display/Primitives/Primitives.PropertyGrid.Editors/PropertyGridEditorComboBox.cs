using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;

public class PropertyGridEditorComboBox : ComboBox
{
	static PropertyGridEditorComboBox()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorComboBox), new FrameworkPropertyMetadata(typeof(PropertyGridEditorComboBox)));
	}
}
