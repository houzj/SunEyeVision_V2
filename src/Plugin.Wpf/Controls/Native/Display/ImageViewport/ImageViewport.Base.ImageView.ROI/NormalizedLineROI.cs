using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedLineROI))]
public class NormalizedLineROI : LineROI
{
	public NormalizedLineROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageViewport/base/imageview/normalizedroi/normalizedlineroi/normalizedlineroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(LineROI.StartPointProperty, resourceDictionary["StartPointBinding"] as BindingBase);
		SetBinding(LineROI.EndPointProperty, resourceDictionary["EndPointBinding"] as BindingBase);
	}
}
