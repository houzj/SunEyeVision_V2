using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Commands;

public static class PropertyItemCommands
{
	private static RoutedCommand _resetValueCommand = new RoutedCommand();

	public static RoutedCommand ResetValue => _resetValueCommand;
}
