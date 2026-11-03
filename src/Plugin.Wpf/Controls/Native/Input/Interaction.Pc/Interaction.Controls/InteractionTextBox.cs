using SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.Controls;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls;

public class InteractionTextBox : TextBox
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
