using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedLineROI))]
public class NormalizedLineROI : LineROI
{
	public NormalizedLineROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedLineROI/NormalizedLineROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(LineROI.StartPointProperty, resourceDictionary["StartPointBinding"] as BindingBase);
		SetBinding(LineROI.EndPointProperty, resourceDictionary["EndPointBinding"] as BindingBase);
	}
}
