using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Utilities;

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
