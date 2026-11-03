using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

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
