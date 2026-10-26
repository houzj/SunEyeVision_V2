using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public class HikTextBox : TextBox
{
	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Return)
		{
			MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			e.Handled = true;
		}
	}
}
