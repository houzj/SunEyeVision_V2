using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

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
