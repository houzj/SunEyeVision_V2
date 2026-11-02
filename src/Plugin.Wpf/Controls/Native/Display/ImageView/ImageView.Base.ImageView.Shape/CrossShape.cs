using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.Shape;

[ShapeDisplayForInterfaceType(typeof(ICross))]
public class CrossShape : ShapeContainer
{
	static CrossShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CrossShape), new FrameworkPropertyMetadata(typeof(CrossShape)));
	}
}
