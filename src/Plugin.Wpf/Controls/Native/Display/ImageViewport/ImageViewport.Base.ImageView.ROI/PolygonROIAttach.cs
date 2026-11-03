using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;

public class PolygonROIAttach
{
	public static readonly DependencyProperty CustomMaxPointNumProperty = DependencyProperty.RegisterAttached("CustomMaxPointNum", typeof(int), typeof(PolygonROIAttach), new PropertyMetadata(-1));

	public static int GetCustomMaxPointNum(DependencyObject obj)
	{
		return (int)obj.GetValue(CustomMaxPointNumProperty);
	}

	public static void SetCustomMaxPointNum(DependencyObject obj, int value)
	{
		obj.SetValue(CustomMaxPointNumProperty, value);
	}
}
