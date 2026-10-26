using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorDecimalUpDown : DecimalUpDown
{
	static PropertyGridEditorDecimalUpDown()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorDecimalUpDown), new FrameworkPropertyMetadata(typeof(PropertyGridEditorDecimalUpDown)));
	}
}
