using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedSectorCaliperROI))]
public class NormalizedSectorCaliperROI : SectorCaliperROI
{
	public NormalizedSectorCaliperROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/normalizedroi/normalizedsectorcaliperroi/normalizedsectorcaliperroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(SectorCaliperROI.StartPointProperty, resourceDictionary["StartPointBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.EndPointProperty, resourceDictionary["EndPointBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.CaliperWidthProperty, resourceDictionary["CaliperWidthBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.CaliperHeightProperty, resourceDictionary["CaliperHeightBinding"] as BindingBase);
		SetBinding(SectorCaliperROI.RadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
	}
}
