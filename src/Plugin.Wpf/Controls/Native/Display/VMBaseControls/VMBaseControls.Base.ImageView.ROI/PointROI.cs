using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IPointROI))]
public class PointROI : ROIControlBase
{
	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty CenterPointProperty;

	private Canvas move;

	private ImageViewItemsPage parent;

	public double ContainerWidth
	{
		get
		{
			return (double)GetValue(ContainerWidthProperty);
		}
		set
		{
			SetValue(ContainerWidthProperty, value);
		}
	}

	public double ContainerHeight
	{
		get
		{
			return (double)GetValue(ContainerHeightProperty);
		}
		set
		{
			SetValue(ContainerHeightProperty, value);
		}
	}

	public Point CenterPoint
	{
		get
		{
			return (Point)GetValue(CenterPointProperty);
		}
		set
		{
			SetValue(CenterPointProperty, value);
		}
	}

	public Point Center => GetControlPoint(CenterPoint);

	static PointROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(PointROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(PointROI), new PropertyMetadata(0.0));
		CenterPointProperty = DependencyProperty.Register("CenterPoint", typeof(Point), typeof(PointROI), new PropertyMetadata(new Point(0.0, 0.0), OnCenterPointChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PointROI), new FrameworkPropertyMetadata(typeof(PointROI)));
	}

	public PointROI()
	{
		Loaded += PointROI_Loaded;
	}

	private void PointROI_Loaded(object sender, RoutedEventArgs e)
	{
		ChangeContainerSize();
	}

	~PointROI()
	{
	}

	public static void Draw(FrameworkElement sender, Type roiDataType)
	{
		ROIItemsPage rOIItemsPage = sender as ROIItemsPage;
		ROIControlBase.BaseBeforeDraw(rOIItemsPage, roiDataType);
		double controlToImageRadio = rOIItemsPage.ControlToImageRadio;
		IPointROI pointROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as IPointROI;
		int num = 1;
		int num2 = 1;
		if (pointROI is INormalizedROI)
		{
			num = rOIItemsPage.ImageWidth;
			num2 = rOIItemsPage.ImageHeight;
		}
		pointROI.CenterPoint = new Point((ROIControlBase.mouseOffset.X / controlToImageRadio - 0.0) / (double)num, (ROIControlBase.mouseOffset.Y / controlToImageRadio - 0.0) / (double)num2);
		rOIItemsPage.CompleteDrawSubstitute(pointROI);
	}

	private static void OnCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Canvas;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		this.GetParent<ListViewItem>().RaiseEvent(e);
		frameworkElement.CaptureMouse();
		this.GetParent<ImageViewBox>().IsDragROI = true;
		frameworkElement.MouseMove += Move_MouseMove;
		frameworkElement.MouseLeftButtonUp += Move_MouseLeftButtonUp;
		e.Handled = true;
	}

	private void Move_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			_isMove = true;
			FrameworkElement frameworkElement = sender as FrameworkElement;
			Point position = e.GetPosition(parent);
			string name = frameworkElement.Name;
			if (name == "move")
			{
				CenterPoint = GetImagePoint(position);
			}
		}
	}

	private void Move_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		frameworkElement.MouseMove -= Move_MouseMove;
		frameworkElement.MouseLeftButtonUp -= Move_MouseLeftButtonUp;
		frameworkElement.ReleaseMouseCapture();
		if (_isMove)
		{
			NotifyROIParamChanged();
			_isMove = false;
		}
		AfterMouseUp();
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		switch (e.Property.Name)
		{
		case "CenterPoint":
			ChangeContainerPosition();
			break;
		case "ControlToImageRadio":
			break;
		case "StrokeThickness":
			ChangeContainerSize();
			break;
		}
	}

	private void ChangeContainerSize()
	{
		ContainerWidth = HotStrokeThickness * 16.0;
		ContainerHeight = ContainerWidth;
		ChangeContainerPosition();
	}

	private void ChangeContainerPosition()
	{
		double x = Center.X - ContainerWidth / 2.0;
		double y = Center.Y - ContainerHeight / 2.0;
		ContainerPosition = new Point(x, y);
	}

	protected override bool Move(ROIMoveDirection rOIMoveDirection)
	{
		switch (rOIMoveDirection)
		{
		case ROIMoveDirection.Left:
			CenterPoint = new Point(CenterPoint.X - 1.0, CenterPoint.Y);
			break;
		case ROIMoveDirection.Right:
			CenterPoint = new Point(CenterPoint.X + 1.0, CenterPoint.Y);
			break;
		case ROIMoveDirection.Up:
			CenterPoint = new Point(CenterPoint.X, CenterPoint.Y - 1.0);
			break;
		case ROIMoveDirection.Down:
			CenterPoint = new Point(CenterPoint.X, CenterPoint.Y + 1.0);
			break;
		default:
			return false;
		}
		return true;
	}
}
