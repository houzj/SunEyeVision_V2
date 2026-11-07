using System.Windows.Input;

namespace VM.Controls.PropertyGrid.Commands;

public static class PropertyItemCommands
{
	private static RoutedCommand _resetValueCommand = new RoutedCommand();

	public static RoutedCommand ResetValue => _resetValueCommand;
}
