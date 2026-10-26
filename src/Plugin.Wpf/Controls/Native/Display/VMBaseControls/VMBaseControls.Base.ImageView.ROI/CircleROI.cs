using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(ICircleROI))]
public class CircleROI : ROIControlBase
{
	private static CircleROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty CircleRadiusProperty;

	public static readonly DependencyProperty MinRadiusProperty;

	public static readonly DependencyProperty CircleCenterPointProperty;

	private Grid move;

	private Rectangle SizeRect;

	private Rectangle InnerRect;

	private Point clickPoint;

	private ImageViewItemsPage parent;

	private double curRadius;

	private double curMinRadius;

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

	public double CircleRadius
	{
		get
		{
			return (double)GetValue(CircleRadiusProperty);
		}
		set
		{
			SetValue(CircleRadiusProperty, value);
		}
	}

	public double MinRadius
	{
		get
		{
			return (double)GetValue(MinRadiusProperty);
		}
		set
		{
			SetValue(MinRadiusProperty, value);
		}
	}

	public Point CircleCenterPoint
	{
		get
		{
			return (Point)GetValue(CircleCenterPointProperty);
		}
		set
		{
			SetValue(CircleCenterPointProperty, value);
		}
	}

	public double InnerCircleSize => MinRadius * ControlToImageRadio * 2.0;

	public double MinRadiusSize => MinRadius * ControlToImageRadio;

	public double RadiusSize => CircleRadius * ControlToImageRadio;

	public Point Center => new Point(ContainerWidth / 2.0, ContainerHeight / 2.0);

	static CircleROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(CircleROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(CircleROI), new PropertyMetadata(0.0));
		CircleRadiusProperty = DependencyProperty.Register("CircleRadius", typeof(double), typeof(CircleROI), new PropertyMetadata(0.0, OnCircleRadiusChanged));
		MinRadiusProperty = DependencyProperty.Register("MinRadius", typeof(double), typeof(CircleROI), new PropertyMetadata(0.0, OnMinRadiusChanged));
		CircleCenterPointProperty = DependencyProperty.Register("CircleCenterPoint", typeof(Point), typeof(CircleROI), new PropertyMetadata(default(Point), OnCircleCenterPointChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CircleROI), new FrameworkPropertyMetadata(typeof(CircleROI)));
	}

	~CircleROI()
	{
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new CircleROISubstitute();
		ROISubstitute.CenterPoint = ROIControlBase.mouseOffset;
		ROIControlBase.BaseAfterDraw(ROISubstitute, parent);
		parent.MouseMove += Parent_MouseMove;
		parent.MouseLeftButtonUp += Parent_MouseLeftButtonUp;
	}

	private static void Parent_MouseMove(object sender, MouseEventArgs e)
	{
		if (ROISubstitute != null)
		{
			if (e.LeftButton == MouseButtonState.Pressed && !ROIControlBase.IsMoved)
			{
				ROIControlBase.IsMoved = true;
			}
			Point position = e.GetPosition(sender as IInputElement);
			ROISubstitute.Radius = (position - ROIControlBase.mouseOffset).Length;
		}
	}

	private static void Parent_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (ROISubstitute == null)
		{
			return;
		}
		ROIItemsPage rOIItemsPage = sender as ROIItemsPage;
		if (!ROIControlBase.IsMoved && ROISubstitute.Radius > 0.0)
		{
			ROIControlBase.IsMoved = true;
		}
		if (ROIControlBase.IsMoved)
		{
			Point centerPoint = ROISubstitute.CenterPoint;
			double radius = ROISubstitute.Radius;
			double controlToImageRadio = ROISubstitute.ControlToImageRadio;
			ICircleROI circleROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ICircleROI;
			int num = 1;
			int num2 = 1;
			if (circleROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			circleROI.CenterPoint = new Point((centerPoint.X / controlToImageRadio - 0.0) / (double)num, (centerPoint.Y / controlToImageRadio - 0.0) / (double)num2);
			circleROI.Radius = radius / controlToImageRadio / (double)num;
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
			ROIControlBase.CompleteDraw(circleROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static void OnCircleRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleROI circleROI = d as CircleROI;
		circleROI.ChangeContainerSize();
		circleROI.ChangeContainerPosition();
		circleROI.OnPropertyChanged("RadiusSize");
	}

	private static void OnMinRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleROI circleROI = d as CircleROI;
		circleROI.OnPropertyChanged("InnerCircleSize");
		circleROI.OnPropertyChanged("MinRadiusSize");
	}

	private static void OnCircleCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleROI circleROI = d as CircleROI;
		circleROI.ChangeContainerPosition();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		SizeRect = Template.FindName("SizeRect", this) as Rectangle;
		SizeRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		InnerRect = Template.FindName("InnerRect", this) as Rectangle;
		InnerRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		curRadius = CircleRadius;
		curMinRadius = MinRadius;
		this.GetParent<ListViewItem>().RaiseEvent(e);
		frameworkElement.CaptureMouse();
		this.GetParent<ImageViewBox>().IsDragROI = true;
		frameworkElement.MouseMove += Move_MouseMove;
		frameworkElement.MouseLeftButtonUp += Move_MouseLeftButtonUp;
		e.Handled = true;
	}

	private void Move_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}
		_isMove = true;
		FrameworkElement frameworkElement = sender as FrameworkElement;
		Point position = e.GetPosition(parent);
		Vector vector = (position - clickPoint) / ControlToImageRadio;
		bool flag = false;
		if (position.X / ControlToImageRadio < CircleCenterPoint.X)
		{
			flag = true;
		}
		switch (frameworkElement.Name)
		{
		case "move":
			CircleCenterPoint += vector;
			clickPoint = position;
			break;
		case "SizeRect":
		{
			if (curRadius <= 0.0)
			{
				curRadius = 1.0;
			}
			Vector vector3 = new Vector(curRadius, 0.0);
			vector3.Normalize();
			if (flag)
			{
				vector3.Negate();
			}
			curRadius += vector * vector3;
			curRadius = Math.Abs(curRadius);
			if (MinRadius != 0.0 && curRadius < MinRadius)
			{
				curRadius = MinRadius;
			}
			CircleRadius = curRadius;
			clickPoint = position;
			break;
		}
		case "InnerRect":
		{
			Vector vector2 = new Vector(CircleRadius, 0.0);
			vector2.Normalize();
			if (flag)
			{
				vector2.Negate();
			}
			curMinRadius += vector * vector2;
			curMinRadius = Math.Abs(curMinRadius);
			if (curMinRadius > CircleRadius)
			{
				curMinRadius = CircleRadius;
			}
			MinRadius = curMinRadius;
			clickPoint = position;
			break;
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
		string name = e.Property.Name;
		if (!(name == "ControlToImageRadio"))
		{
			if (name == "StrokeThickness")
			{
				OnPropertyChanged("RadiusSize");
			}
		}
		else
		{
			ChangeContainerSize();
			ChangeContainerPosition();
			OnPropertyChanged("InnerCircleSize");
			OnPropertyChanged("MinRadiusSize");
			OnPropertyChanged("RadiusSize");
		}
	}

	private void ChangeContainerSize()
	{
		ContainerWidth = CircleRadius * ControlToImageRadio * 2.0;
		ContainerHeight = ContainerWidth;
		OnPropertyChanged("Center");
	}

	private void ChangeContainerPosition()
	{
		double x = (CircleCenterPoint.X - CircleRadius + 0.0) * ControlToImageRadio;
		double y = (CircleCenterPoint.Y - CircleRadius + 0.0) * ControlToImageRadio;
		ContainerPosition = new Point(x, y);
	}

	protected override bool Move(ROIMoveDirection rOIMoveDirection)
	{
		switch (rOIMoveDirection)
		{
		case ROIMoveDirection.Left:
			CircleCenterPoint = new Point(CircleCenterPoint.X - 1.0, CircleCenterPoint.Y);
			break;
		case ROIMoveDirection.Right:
			CircleCenterPoint = new Point(CircleCenterPoint.X + 1.0, CircleCenterPoint.Y);
			break;
		case ROIMoveDirection.Up:
			CircleCenterPoint = new Point(CircleCenterPoint.X, CircleCenterPoint.Y - 1.0);
			break;
		case ROIMoveDirection.Down:
			CircleCenterPoint = new Point(CircleCenterPoint.X, CircleCenterPoint.Y + 1.0);
			break;
		default:
			return false;
		}
		return true;
	}
}
