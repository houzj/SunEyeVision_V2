using SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Spinners;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Controls;

public class FieldTextBox : TextBox
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
