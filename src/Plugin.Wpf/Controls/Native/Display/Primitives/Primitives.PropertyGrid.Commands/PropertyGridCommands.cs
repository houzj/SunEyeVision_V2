using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Commands;

public class PropertyGridCommands
{
	private static RoutedCommand _clearFilterCommand = new RoutedCommand();

	public static RoutedCommand ClearFilter => _clearFilterCommand;
}
