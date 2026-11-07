using System;
using System.Windows;
using System.Windows.Data;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedMatrixCircleROI))]
public class NormalizedMatrixCircleROI : MatrixCircleROI
{
	public NormalizedMatrixCircleROI()
	{
		Uri resourceLocator = new Uri("/VMBaseControls;component/Base/ImageView/NormalizedROI/NormalizedMatrixCircleROI/NormalizedMatrixCircleROIStyle.xaml", UriKind.Relative);
		ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
		SetBinding(RectROI.RectWidthProperty, resourceDictionary["WidthBinding"] as BindingBase);
		SetBinding(RectROI.RectHeightProperty, resourceDictionary["HeightBinding"] as BindingBase);
		SetBinding(RectROI.RectCenterPointProperty, resourceDictionary["CenterPointBinding"] as BindingBase);
		SetBinding(MatrixCircleROI.CircleRadiusProperty, resourceDictionary["RadiusBinding"] as BindingBase);
		SetBinding(MatrixCircleROI.MinCirleRadiusProperty, resourceDictionary["MinRadiusBinding"] as BindingBase);
	}
}
