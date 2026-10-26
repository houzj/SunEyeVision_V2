using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

[ShapeDisplayForInterfaceType(typeof(ICross))]
public class CrossShape : ShapeContainer
{
	static CrossShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CrossShape), new FrameworkPropertyMetadata(typeof(CrossShape)));
	}
}
