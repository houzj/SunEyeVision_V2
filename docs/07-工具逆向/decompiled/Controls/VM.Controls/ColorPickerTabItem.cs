using System.Windows.Controls;
using System.Windows.Input;

namespace VM.Controls;

public class ColorPickerTabItem : TabItem
{
	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		if (e.Source != this && IsSelected)
		{
			base.OnMouseLeftButtonDown(e);
		}
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		if (e.Source == this || !IsSelected)
		{
			base.OnMouseLeftButtonDown(e);
		}
		base.OnMouseLeftButtonUp(e);
	}
}
