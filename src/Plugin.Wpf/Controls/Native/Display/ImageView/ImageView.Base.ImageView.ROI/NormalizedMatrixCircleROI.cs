using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedMatrixCircleROI))]
public class NormalizedMatrixCircleROI : MatrixCircleROI
{
	public NormalizedMatrixCircleROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageView/base/imageview/normalizedroi/normalizedmatrixcircleroi/normalizedmatrixcircleroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(RectROI.RectWidthProperty, resourceDictionary["WidthBinding"] as BindingBase);
		SetBinding(RectROI.RectHeightProperty, resourceDictionary["HeightBinding"] as BindingBase);
		SetBinding(RectROI.RectCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
		SetBinding(MatrixCircleROI.CircleRadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
		SetBinding(MatrixCircleROI.MinCirleRadiusProperty, resourceDictionary["MinRadiusBinding"] as BindingBase);
	}
}
