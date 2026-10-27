using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedPolygonROI))]
public class NormalizedPolygonROI : PolygonROI
{
	public NormalizedPolygonROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/normalizedroi/normalizedpolygonroi/normalizedpolygonroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		BindingBase binding = resourceDictionary["PolygonPointsBinding"] as BindingBase;
		SetBinding(PolygonROI.PolygonPointsProperty, binding);
        var converter=((MultiBinding)binding).Converter as SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData.PointListRightFixdMultiplicationConverter;
        Unloaded+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,this)){CancelInteraction();converter?.Disconnect();}};
        Loaded+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,this))BindingOperations.GetMultiBindingExpression(this,PolygonPointsProperty)?.UpdateTarget();};
	}
}
