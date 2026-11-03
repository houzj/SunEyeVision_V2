using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedPolygonROI))]
public class NormalizedPolygonROI : PolygonROI
{
	public NormalizedPolygonROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageView/base/imageview/normalizedroi/normalizedpolygonroi/normalizedpolygonroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		BindingBase binding = resourceDictionary["PolygonPointsBinding"] as BindingBase;
		SetBinding(PolygonROI.PolygonPointsProperty, binding);
        var converter=((MultiBinding)binding).Converter as SunEyeVision.Plugin.Wpf.Native.Dependencies.ValueConverters.PointListRightFixdMultiplicationConverter;
        Unloaded+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,this)){CancelInteraction();converter?.Disconnect();}};
        Loaded+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,this))BindingOperations.GetMultiBindingExpression(this,PolygonPointsProperty)?.UpdateTarget();};
	}
}
