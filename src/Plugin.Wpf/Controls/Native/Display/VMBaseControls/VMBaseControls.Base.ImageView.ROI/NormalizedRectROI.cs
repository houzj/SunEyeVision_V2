using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedRectROI))]
public class NormalizedRectROI : RectROI
{
	public NormalizedRectROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/normalizedroi/normalizedrectroi/normalizedrectroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(RectROI.RectWidthProperty, resourceDictionary["WidthBinding"] as BindingBase);
		SetBinding(RectROI.RectHeightProperty, resourceDictionary["HeightBinding"] as BindingBase);
		SetBinding(RectROI.RectCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
