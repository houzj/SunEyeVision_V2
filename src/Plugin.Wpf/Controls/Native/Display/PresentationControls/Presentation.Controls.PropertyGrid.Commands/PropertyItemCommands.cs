using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Commands;

public static class PropertyItemCommands
{
	private static RoutedCommand _resetValueCommand = new RoutedCommand();

	public static RoutedCommand ResetValue => _resetValueCommand;
}
