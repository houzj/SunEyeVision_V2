using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Input;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(ICrossROI))]
public class CrossROI : ROIControlBase
{
	public static readonly DependencyProperty CrossCenterPointProperty;

	public Point CrossCenterPoint
	{
		get
		{
			return (Point)GetValue(CrossCenterPointProperty);
		}
		set
		{
			SetValue(CrossCenterPointProperty, value);
		}
	}

	static CrossROI()
	{
		CrossCenterPointProperty = DependencyProperty.Register("CrossCenterPoint", typeof(Point), typeof(CrossROI), new PropertyMetadata(default(Point), OnCrossCenterPoint));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CrossROI), new FrameworkPropertyMetadata(typeof(CrossROI)));
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROIItemsPage rOIItemsPage = parent as ROIItemsPage;
		Point position = Mouse.GetPosition(rOIItemsPage);
		double controlToImageRadio = rOIItemsPage.ControlToImageRadio;
		ICrossROI crossROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ICrossROI;
		int num = 1;
		int num2 = 1;
		if (crossROI is INormalizedROI)
		{
			num = rOIItemsPage.ImageWidth;
			num2 = rOIItemsPage.ImageHeight;
		}
		crossROI.CenterPoint = new Point((position.X / controlToImageRadio - 0.0) / (double)num, (position.Y / controlToImageRadio - 0.0) / (double)num2);
		ROIControlBase.CompleteDraw(crossROI, parent);
		ROIControlBase.BaseFinishDraw(null, parent);
	}

	private static void OnCrossCenterPoint(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CrossROI crossROI = d as CrossROI;
		crossROI.ChangeContainerPosition();
	}

	private void ChangeContainerPosition()
	{
		double x = (CrossCenterPoint.X + 0.0) * ControlToImageRadio;
		double y = (CrossCenterPoint.Y + 0.0) * ControlToImageRadio;
		ContainerPosition = new Point(x, y);
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		string name = e.Property.Name;
		if (name == "ControlToImageRadio")
		{
			ChangeContainerPosition();
		}
	}
}
