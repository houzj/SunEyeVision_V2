using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedPolygonROI))]
public class NormalizedPolygonROI : PolygonROI
{
	public NormalizedPolygonROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedPolygonROI/NormalizedPolygonROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		BindingBase binding = resourceDictionary["PolygonPointsBinding"] as BindingBase;
		SetBinding(PolygonROI.PolygonPointsProperty, binding);
	}
}
