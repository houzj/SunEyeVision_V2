using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedLineCaliperROI))]
public class NormalizedLineCaliperROI : LineCaliperROI
{
	public NormalizedLineCaliperROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/normalizedroi/normalizedlinecaliperroi/normalizedlinecaliperroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(LineCaliperROI.CaliperWidthProperty, resourceDictionary["CaliperWidthBinding"] as BindingBase);
		SetBinding(LineCaliperROI.CaliperHeightProperty, resourceDictionary["CaliperHeightBinding"] as BindingBase);
		SetBinding(LineCaliperROI.StartPointProperty, resourceDictionary["StartPointBinding"] as BindingBase);
		SetBinding(LineCaliperROI.EndPointProperty, resourceDictionary["EndPointBinding"] as BindingBase);
	}
}
