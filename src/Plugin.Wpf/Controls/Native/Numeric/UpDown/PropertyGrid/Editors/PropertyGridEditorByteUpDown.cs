using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;

public class PropertyGridEditorByteUpDown : ByteUpDown
{
	static PropertyGridEditorByteUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorByteUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorByteUpDown)));
	}
}
