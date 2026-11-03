using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedCircleCaliperROI))]
public class NormalizedCircleCaliperROI : CircleCaliperROI
{
	public NormalizedCircleCaliperROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageViewport/base/imageview/normalizedroi/normalizedcirclecaliperroi/normalizedcirclecaliperroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(CircleCaliperROI.CaliperWidthProperty, resourceDictionary["CaliperWidthBinding"] as BindingBase);
		SetBinding(CircleCaliperROI.CaliperHeightProperty, resourceDictionary["CaliperHeightBinding"] as BindingBase);
		SetBinding(CircleCaliperROI.CircleRadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
		SetBinding(CircleCaliperROI.CircleCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
