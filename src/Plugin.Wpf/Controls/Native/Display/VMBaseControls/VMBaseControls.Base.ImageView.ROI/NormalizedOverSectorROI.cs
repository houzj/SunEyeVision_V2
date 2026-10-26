using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedOverSectorROI))]
public class NormalizedOverSectorROI : OverSectorROI
{
	public NormalizedOverSectorROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/normalizedroi/normalizedoversectorroi/normalizedoversectorroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(OverSectorROI.MinRadiusProperty, resourceDictionary["MinRadiusBinding"] as BindingBase);
		SetBinding(OverSectorROI.CircleRadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
		SetBinding(OverSectorROI.CircleCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
