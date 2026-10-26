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

[ROIDisplayForInterfaceType(typeof(ICircleMesROI))]
public class CircleMesROI : ROIControlBase
{
	private static CircleROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty CircleRadiusProperty;

	public static readonly DependencyProperty CircleCenterPointProperty;

	private Grid move;

	private Rectangle SizeRect;

	private Point clickPoint;

	private ImageViewItemsPage parent;

	private double curRadius;

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

	static CircleMesROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(CircleMesROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(CircleMesROI), new PropertyMetadata(0.0));
		CircleRadiusProperty = DependencyProperty.Register("CircleRadius", typeof(double), typeof(CircleMesROI), new PropertyMetadata(0.0, OnCircleRadiusChanged));
		CircleCenterPointProperty = DependencyProperty.Register("CircleCenterPoint", typeof(Point), typeof(CircleMesROI), new PropertyMetadata(default(Point), OnCircleCenterPointChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CircleMesROI), new FrameworkPropertyMetadata(typeof(CircleMesROI)));
	}

	~CircleMesROI()
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
			ICircleMesROI circleMesROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ICircleMesROI;
			int num = 1;
			int num2 = 1;
			if (circleMesROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			circleMesROI.CenterPoint = new Point((centerPoint.X / controlToImageRadio - 0.0) / (double)num, (centerPoint.Y / controlToImageRadio - 0.0) / (double)num2);
			circleMesROI.Radius = radius / controlToImageRadio / (double)num;
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
			ROIControlBase.CompleteDraw(circleMesROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static void OnCircleRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleMesROI circleMesROI = d as CircleMesROI;
		circleMesROI.ChangeContainerSize();
		circleMesROI.ChangeContainerPosition();
	}

	private static void OnCircleCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleMesROI circleMesROI = d as CircleMesROI;
		circleMesROI.ChangeContainerPosition();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		SizeRect = Template.FindName("SizeRect", this) as Rectangle;
		SizeRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		curRadius = CircleRadius;
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
		string name = frameworkElement.Name;
		if (!(name == "move"))
		{
			if (name == "SizeRect")
			{
				if (curRadius <= 0.0)
				{
					curRadius = 1.0;
				}
				Vector vector2 = new Vector(curRadius, 0.0);
				vector2.Normalize();
				if (flag)
				{
					vector2.Negate();
				}
				curRadius += vector * vector2;
				curRadius = Math.Abs(curRadius);
				CircleRadius = curRadius;
				clickPoint = position;
			}
		}
		else
		{
			CircleCenterPoint += vector;
			clickPoint = position;
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
			}
		}
		else
		{
			ChangeContainerSize();
			ChangeContainerPosition();
		}
	}

	private void ChangeContainerSize()
	{
		ContainerWidth = CircleRadius * ControlToImageRadio * 2.0;
		ContainerHeight = ContainerWidth;
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
