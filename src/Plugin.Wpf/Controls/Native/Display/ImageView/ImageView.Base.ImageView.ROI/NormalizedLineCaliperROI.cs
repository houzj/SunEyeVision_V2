using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedLineCaliperROI))]
public class NormalizedLineCaliperROI : LineCaliperROI
{
	public NormalizedLineCaliperROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageView/base/imageview/normalizedroi/normalizedlinecaliperroi/normalizedlinecaliperroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(LineCaliperROI.CaliperWidthProperty, resourceDictionary["CaliperWidthBinding"] as BindingBase);
		SetBinding(LineCaliperROI.CaliperHeightProperty, resourceDictionary["CaliperHeightBinding"] as BindingBase);
		SetBinding(LineCaliperROI.StartPointProperty, resourceDictionary["StartPointBinding"] as BindingBase);
		SetBinding(LineCaliperROI.EndPointProperty, resourceDictionary["EndPointBinding"] as BindingBase);
	}
}
