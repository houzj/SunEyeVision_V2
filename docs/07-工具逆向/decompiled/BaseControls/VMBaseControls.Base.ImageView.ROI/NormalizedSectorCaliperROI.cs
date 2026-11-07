using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedSectorCaliperROI))]
public class NormalizedSectorCaliperROI : SectorCaliperROI
{
	public NormalizedSectorCaliperROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedSectorCaliperROI/NormalizedSectorCaliperROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(SectorCaliperROI.StartPointProperty, resourceDictionary["StartPointBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.EndPointProperty, resourceDictionary["EndPointBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.CaliperWidthProperty, resourceDictionary["CaliperWidthBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.CaliperHeightProperty, resourceDictionary["CaliperHeightBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.RadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
	}
}
