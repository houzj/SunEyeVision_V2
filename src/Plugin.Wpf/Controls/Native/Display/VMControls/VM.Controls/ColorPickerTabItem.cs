using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

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
