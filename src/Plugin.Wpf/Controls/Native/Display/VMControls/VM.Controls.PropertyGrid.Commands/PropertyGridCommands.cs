using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Commands;

public class PropertyGridCommands
{
	private static RoutedCommand _clearFilterCommand = new RoutedCommand();

	public static RoutedCommand ClearFilter => _clearFilterCommand;
}
