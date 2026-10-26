using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal class KeyboardUtilities
{
	internal static bool IsKeyModifyingPopupState(KeyEventArgs e)
	{
		if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt || (e.SystemKey != Key.Down && e.SystemKey != Key.Up))
		{
			return e.Key == Key.F4;
		}
		return true;
	}
}
