using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedSectorROI))]
public class NormalizedSectorROI : SectorROI
{
	public NormalizedSectorROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/normalizedroi/normalizedsectorroi/normalizedsectorroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(SectorROI.MinRadiusProperty, resourceDictionary["MinRadiusBinding"] as BindingBase);
		SetBinding(SectorROI.CircleRadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
		SetBinding(SectorROI.CircleCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
