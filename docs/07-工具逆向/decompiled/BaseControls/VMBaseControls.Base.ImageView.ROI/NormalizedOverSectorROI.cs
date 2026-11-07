using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedOverSectorROI))]
public class NormalizedOverSectorROI : OverSectorROI
{
	public NormalizedOverSectorROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedOverSectorROI/NormalizedOverSectorROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(OverSectorROI.MinRadiusProperty, resourceDictionary["MinRadiusBinding"] as BindingBase);
		SetBinding(OverSectorROI.CircleRadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
		SetBinding(OverSectorROI.CircleCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
