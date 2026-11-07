using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedPointROI))]
public class NormalizedPointROI : PointROI
{
	public NormalizedPointROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedPointROI/NormalizedPointROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(PointROI.CenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
