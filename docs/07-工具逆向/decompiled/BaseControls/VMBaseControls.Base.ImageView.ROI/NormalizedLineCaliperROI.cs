using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedLineCaliperROI))]
public class NormalizedLineCaliperROI : LineCaliperROI
{
	public NormalizedLineCaliperROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedLineCaliperROI/NormalizedLineCaliperROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(LineCaliperROI.CaliperWidthProperty, resourceDictionary["CaliperWidthBinding"] as BindingBase);
		SetBinding(LineCaliperROI.CaliperHeightProperty, resourceDictionary["CaliperHeightBinding"] as BindingBase);
		SetBinding(LineCaliperROI.StartPointProperty, resourceDictionary["StartPointBinding"] as BindingBase);
		SetBinding(LineCaliperROI.EndPointProperty, resourceDictionary["EndPointBinding"] as BindingBase);
	}
}
