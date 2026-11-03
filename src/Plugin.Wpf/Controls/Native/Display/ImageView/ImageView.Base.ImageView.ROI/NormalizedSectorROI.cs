using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedSectorROI))]
public class NormalizedSectorROI : SectorROI
{
	public NormalizedSectorROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageView/base/imageview/normalizedroi/normalizedsectorroi/normalizedsectorroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(SectorROI.MinRadiusProperty, resourceDictionary["MinRadiusBinding"] as BindingBase);
		SetBinding(SectorROI.CircleRadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
		SetBinding(SectorROI.CircleCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
	}
}
