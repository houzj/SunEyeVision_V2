using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.Shape;

[ShapeDisplayForInterfaceType(typeof(ICross))]
public class CrossShape : ShapeContainer
{
	static CrossShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CrossShape), new FrameworkPropertyMetadata(typeof(CrossShape)));
	}
}
