using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedRectROI))]
public class NormalizedRectROI : RectROI
{
	public NormalizedRectROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedRectROI/NormalizedRectROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(RectROI.RectWidthProperty, resourceDictionary["WidthBinding"] as BindingBase);
		SetBinding(RectROI.RectHeightProperty, resourceDictionary["HeightBinding"] as BindingBase);
		SetBinding(RectROI.RectCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
