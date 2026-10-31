using SunEyeVision.Plugin.Wpf.Controls.Popups;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Controls.Popups;

public class PopupTextBox : TextBox
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
