using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

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
