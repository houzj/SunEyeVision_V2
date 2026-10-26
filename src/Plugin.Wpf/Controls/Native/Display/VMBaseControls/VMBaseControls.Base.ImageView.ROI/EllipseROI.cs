using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Apps.UIHelper.Extern;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IEllipseROI))]
public class EllipseROI : ROIControlBase
{
	private static EllipseROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty ContainerAngleProperty;

	public static readonly DependencyProperty EllipseRadiusXProperty;

	public static readonly DependencyProperty EllipseRadiusYProperty;

	public static readonly DependencyProperty EllipseCenterPointProperty;

	public static readonly DependencyProperty EllipseAngleProperty;

	private Grid move;

	private Area SizeRectX;

	private Area SizeRectY;

	private RotateArea rightRotate;

	private ImageViewItemsPage parent;

	private RectangleGeometry eRect;

	private Point clickPoint;

	private double curRadiusX;

	private double curRadiusY;

	private RotateTransform rotateTransform = new RotateTransform();

	public new double NegativeStrokeThickness => -1.0 * StrokeThickness;

	public Thickness MoveRotateMargin => new Thickness(4.0 * dwidth * NegativeStrokeThickness);

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

	public double ContainerAngle
	{
		get
		{
			return (double)GetValue(ContainerAngleProperty);
		}
		set
		{
			SetValue(ContainerAngleProperty, value);
		}
	}

	public double EllipseRadiusX
	{
		get
		{
			return (double)GetValue(EllipseRadiusXProperty);
		}
		set
		{
			SetValue(EllipseRadiusXProperty, value);
		}
	}

	public double EllipseRadiusY
	{
		get
		{
			return (double)GetValue(EllipseRadiusYProperty);
		}
		set
		{
			SetValue(EllipseRadiusYProperty, value);
		}
	}

	public Point EllipseCenterPoint
	{
		get
		{
			return (Point)GetValue(EllipseCenterPointProperty);
		}
		set
		{
			SetValue(EllipseCenterPointProperty, value);
		}
	}

	public double EllipseAngle
	{
		get
		{
			return (double)GetValue(EllipseAngleProperty);
		}
		set
		{
			SetValue(EllipseAngleProperty, value);
		}
	}

	static EllipseROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(EllipseROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(EllipseROI), new PropertyMetadata(0.0));
		ContainerAngleProperty = DependencyProperty.Register("ContainerAngle", typeof(double), typeof(EllipseROI), new PropertyMetadata(0.0));
		EllipseRadiusXProperty = DependencyProperty.Register("EllipseRadiusX", typeof(double), typeof(EllipseROI), new PropertyMetadata(0.0, OnEllipseRadiusXChanged));
		EllipseRadiusYProperty = DependencyProperty.Register("EllipseRadiusY", typeof(double), typeof(EllipseROI), new PropertyMetadata(0.0, OnEllipseRadiusYChanged));
		EllipseCenterPointProperty = DependencyProperty.Register("EllipseCenterPoint", typeof(Point), typeof(EllipseROI), new PropertyMetadata(default(Point), OnEllipseCenterPointChanged));
		EllipseAngleProperty = DependencyProperty.Register("EllipseAngle", typeof(double), typeof(EllipseROI), new PropertyMetadata(0.0, OnEllipseAngleChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(EllipseROI), new FrameworkPropertyMetadata(typeof(EllipseROI)));
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new EllipseROISubstitute();
		ROISubstitute.CenterPoint = ROIControlBase.mouseOffset;
		ROIControlBase.BaseAfterDraw(ROISubstitute, parent);
		parent.MouseMove += Parent_MouseMove;
		parent.MouseLeftButtonUp += Parent_MouseLeftButtonUp;
	}

	private static void Parent_MouseMove(object sender, MouseEventArgs e)
	{
		if (ROISubstitute != null && e.LeftButton == MouseButtonState.Pressed)
		{
			if (!ROIControlBase.IsMoved)
			{
				ROIControlBase.IsMoved = true;
			}
			Point position = e.GetPosition(sender as IInputElement);
			Vector vector = position - ROIControlBase.mouseOffset;
			double controlToAxisXRadio = (sender as ROIItemsPage).ControlToAxisXRadio;
			double controlToAxisYRadio = (sender as ROIItemsPage).ControlToAxisYRadio;
			if (controlToAxisXRadio == 0.0 || controlToAxisYRadio == 0.0)
			{
				ROISubstitute.RadiusX = Math.Abs(vector.X);
				ROISubstitute.RadiusY = Math.Abs(vector.Y);
			}
			else
			{
				ROISubstitute.RadiusX = Math.Abs(vector.X);
				ROISubstitute.RadiusY = Math.Abs(vector.X * controlToAxisXRadio / controlToAxisYRadio);
			}
		}
	}

	private static void Parent_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (ROISubstitute != null)
		{
			FrameworkElement frameworkElement = sender as FrameworkElement;
			Point centerPoint = ROISubstitute.CenterPoint;
			double radiusX = ROISubstitute.RadiusX;
			double radiusY = ROISubstitute.RadiusY;
			double controlToImageRadio = ROISubstitute.ControlToImageRadio;
			IEllipseROI ellipseROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as IEllipseROI;
			ellipseROI.CenterPoint = new Point(centerPoint.X / controlToImageRadio - 0.0, centerPoint.Y / controlToImageRadio - 0.0);
			ellipseROI.RadiusX = radiusX / controlToImageRadio;
			ellipseROI.RadiusY = radiusY / controlToImageRadio;
			frameworkElement.MouseMove -= Parent_MouseMove;
			frameworkElement.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
			ROIControlBase.CompleteDraw(ellipseROI, frameworkElement);
			ROIControlBase.BaseFinishDraw(ROISubstitute, frameworkElement);
		}
	}

	private static void OnEllipseRadiusXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		EllipseROI ellipseROI = d as EllipseROI;
		ellipseROI.ChangeContainerWidth();
		ellipseROI.ChangeContainerPosition();
	}

	private static void OnEllipseRadiusYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		EllipseROI ellipseROI = d as EllipseROI;
		ellipseROI.ChangeContainerHeight();
		ellipseROI.ChangeContainerPosition();
	}

	private static void OnEllipseCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		EllipseROI ellipseROI = d as EllipseROI;
		ellipseROI.ChangeContainerPosition();
	}

	private static void OnEllipseAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		EllipseROI ellipseROI = d as EllipseROI;
		double containerAngle = (double)e.NewValue;
		ellipseROI.ContainerAngle = containerAngle;
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		SizeRectX = Template.FindName("SizeRectX", this) as Area;
		SizeRectX.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		SizeRectY = Template.FindName("SizeRectY", this) as Area;
		SizeRectY.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		rightRotate = Template.FindName("rightRotate", this) as RotateArea;
		rightRotate.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		eRect = Template.FindName("eRect", this) as RectangleGeometry;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		curRadiusX = EllipseRadiusX;
		curRadiusY = EllipseRadiusY;
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
			Vector vector = (position - clickPoint) / ControlToImageRadio;
			rotateTransform.Angle = EllipseAngle;
			Point point = rotateTransform.Inverse.Transform(new Point(vector.X, vector.Y));
			Vector vector2 = new Vector(point.X, point.Y);
			switch (frameworkElement.Name)
			{
			case "move":
				EllipseCenterPoint += vector;
				clickPoint = position;
				break;
			case "SizeRectX":
				curRadiusX += vector2.X;
				EllipseRadiusX = Math.Abs(curRadiusX);
				clickPoint = position;
				break;
			case "SizeRectY":
			{
				Vector vector5 = new Vector(0.0, 1.0);
				curRadiusY += vector.Y;
				EllipseRadiusY = Math.Abs(curRadiusY);
				clickPoint = position;
				break;
			}
			case "rightRotate":
			{
				Vector vector3 = new Vector(position.X / ControlToImageRadio - EllipseCenterPoint.X, position.Y / ControlToImageRadio - EllipseCenterPoint.Y);
				Vector vector4 = vector3 + vector;
				double num = Vector.AngleBetween(vector3, vector4);
				EllipseAngle += num;
				clickPoint = position;
				break;
			}
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
				OnPropertyChanged("MoveRotateMargin");
				OnPropertyChanged("NegativeStrokeThickness");
				double num = StrokeThickness * 9.0;
				if (eRect != null)
				{
					eRect.Rect = new Rect(num, 0.0, num, num);
				}
			}
		}
		else
		{
			ChangeContainerWidth();
			ChangeContainerHeight();
			ChangeContainerPosition();
		}
	}

	private void ChangeContainerWidth()
	{
		ContainerWidth = EllipseRadiusX * ControlToImageRadio * 2.0;
	}

	private void ChangeContainerHeight()
	{
		ContainerHeight = EllipseRadiusY * ControlToImageRadio * 2.0;
	}

	private void ChangeContainerPosition()
	{
		double x = (EllipseCenterPoint.X - EllipseRadiusX + 0.0) * ControlToImageRadio;
		double y = (EllipseCenterPoint.Y - EllipseRadiusY + 0.0) * ControlToImageRadio;
		ContainerPosition = new Point(x, y);
	}

	protected override bool Move(ROIMoveDirection rOIMoveDirection)
	{
		switch (rOIMoveDirection)
		{
		case ROIMoveDirection.Left:
			EllipseCenterPoint = new Point(EllipseCenterPoint.X - 1.0, EllipseCenterPoint.Y);
			break;
		case ROIMoveDirection.Right:
			EllipseCenterPoint = new Point(EllipseCenterPoint.X + 1.0, EllipseCenterPoint.Y);
			break;
		case ROIMoveDirection.Up:
			EllipseCenterPoint = new Point(EllipseCenterPoint.X, EllipseCenterPoint.Y - 1.0);
			break;
		case ROIMoveDirection.Down:
			EllipseCenterPoint = new Point(EllipseCenterPoint.X, EllipseCenterPoint.Y + 1.0);
			break;
		default:
			return false;
		}
		return true;
	}
}
