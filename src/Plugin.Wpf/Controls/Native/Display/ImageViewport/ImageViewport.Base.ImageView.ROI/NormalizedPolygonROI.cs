using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedPolygonROI))]
public class NormalizedPolygonROI : PolygonROI
{
	public NormalizedPolygonROI()
	{
		Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageViewport/base/imageview/normalizedroi/normalizedpolygonroi/normalizedpolygonroistyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		BindingBase binding = resourceDictionary["PolygonPointsBinding"] as BindingBase;
		SetBinding(PolygonROI.PolygonPointsProperty, binding);
        var converter=((MultiBinding)binding).Converter as SunEyeVision.Plugin.Wpf.Native.Dependencies.DataAdapters.PointListRightFixdMultiplicationConverter;
        Unloaded+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,this)){CancelInteraction();converter?.Disconnect();}};
        Loaded+=(_,args)=>{if(ReferenceEquals(args.OriginalSource,this))BindingOperations.GetMultiBindingExpression(this,PolygonPointsProperty)?.UpdateTarget();};
	}
}
