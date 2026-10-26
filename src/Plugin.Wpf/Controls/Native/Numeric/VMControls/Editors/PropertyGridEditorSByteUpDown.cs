using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

internal class PropertyGridEditorSByteUpDown : SByteUpDown
{
	static PropertyGridEditorSByteUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorSByteUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorSByteUpDown)));
	}
}
