using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core.Utilities;

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
