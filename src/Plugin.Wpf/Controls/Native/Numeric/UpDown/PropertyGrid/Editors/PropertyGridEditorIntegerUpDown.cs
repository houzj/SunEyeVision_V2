using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;

public class PropertyGridEditorIntegerUpDown : IntegerUpDown
{
	static PropertyGridEditorIntegerUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorIntegerUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorIntegerUpDown)));
	}
}
