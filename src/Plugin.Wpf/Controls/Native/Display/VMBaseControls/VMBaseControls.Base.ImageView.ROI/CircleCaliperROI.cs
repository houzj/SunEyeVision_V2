using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(ICircleCaliperROI))]
public class CircleCaliperROI : ROIControlBase
{
	private static CircleCaliperROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty CircleRadiusProperty;

	public static readonly DependencyProperty StartAngleProperty;

	public static readonly DependencyProperty CircleCenterPointProperty;

	private Grid move;

	private Rectangle sizeRect;

	private DrawingCanvas drawCanvas;

	private Point clickPoint;

	private RotateTransform caliperRotate = new RotateTransform();

	private ImageViewItemsPage parent;

	private Area caliperRect;

	private Area left;

	private Area top;

	private Area right;

	private Area bottom;

	private RotateTransform RT;

	private double curRadius;

	public static readonly DependencyProperty CaliperHeightProperty;

	public static readonly DependencyProperty CaliperWidthProperty;

	public static readonly DependencyProperty CaliperNumProperty;

	public static readonly DependencyProperty IsLimitFlagProperty;

	public static readonly DependencyProperty CaliperRotateAngleProperty;

	private Point RectCenterPoint;

	private Brush brush = Brushes.Transparent;

	private Pen pen = new Pen();

	public double RectWidth => CaliperWidth * ControlToImageRadio;

	public double RectHeight => CaliperHeight * ControlToImageRadio;

	public Point RectTopLeft => new Point(RectCenterPoint.X - RectWidth / 2.0, RectCenterPoint.Y - RectHeight / 2.0);

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

	public double StartAngle
	{
		get
		{
			return (double)GetValue(StartAngleProperty);
		}
		set
		{
			SetValue(StartAngleProperty, value);
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

	public double CaliperHeight
	{
		get
		{
			return (double)GetValue(CaliperHeightProperty);
		}
		set
		{
			SetValue(CaliperHeightProperty, value);
		}
	}

	public double CaliperWidth
	{
		get
		{
			return (double)GetValue(CaliperWidthProperty);
		}
		set
		{
			SetValue(CaliperWidthProperty, value);
		}
	}

	public uint CaliperNum
	{
		get
		{
			return (uint)GetValue(CaliperNumProperty);
		}
		set
		{
			SetValue(CaliperNumProperty, value);
		}
	}

	public bool IsLimitFlag
	{
		get
		{
			return (bool)GetValue(IsLimitFlagProperty);
		}
		set
		{
			SetValue(IsLimitFlagProperty, value);
		}
	}

	public double CaliperRotateAngle
	{
		get
		{
			return (double)GetValue(CaliperRotateAngleProperty);
		}
		set
		{
			SetValue(CaliperRotateAngleProperty, value);
		}
	}

	static CircleCaliperROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(CircleCaliperROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(CircleCaliperROI), new PropertyMetadata(0.0));
		CircleRadiusProperty = DependencyProperty.Register("CircleRadius", typeof(double), typeof(CircleCaliperROI), new PropertyMetadata(double.NaN, OnCircleRadiusChanged));
		StartAngleProperty = DependencyProperty.Register("StartAngle", typeof(double), typeof(CircleCaliperROI), new PropertyMetadata(0.0, OnStartAngleChanged));
		CircleCenterPointProperty = DependencyProperty.Register("CircleCenterPoint", typeof(Point), typeof(CircleCaliperROI), new PropertyMetadata(default(Point), OnCircleCenterPointChanged));
		CaliperHeightProperty = DependencyProperty.Register("CaliperHeight", typeof(double), typeof(CircleCaliperROI), new PropertyMetadata(double.NaN, OnCaliperHeightChanged));
		CaliperWidthProperty = DependencyProperty.Register("CaliperWidth", typeof(double), typeof(CircleCaliperROI), new PropertyMetadata(double.NaN, OnCaliperWidthChanged));
		CaliperNumProperty = DependencyProperty.Register("CaliperNum", typeof(uint), typeof(CircleCaliperROI), new PropertyMetadata(OnCaliperNumChanged));
		IsLimitFlagProperty = DependencyProperty.Register("IsLimitFlag", typeof(bool), typeof(CircleCaliperROI), new PropertyMetadata(true));
		CaliperRotateAngleProperty = DependencyProperty.Register("CaliperRotateAngle", typeof(double), typeof(CircleCaliperROI), new PropertyMetadata(0.0, OnCaliperRotateAngleChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CircleCaliperROI), new FrameworkPropertyMetadata(typeof(CircleCaliperROI)));
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new CircleCaliperROISubstitute();
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
			ICircleCaliperROI circleCaliperROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ICircleCaliperROI;
			int num = 1;
			int num2 = 1;
			if (circleCaliperROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			circleCaliperROI.CenterPoint = new Point((centerPoint.X / controlToImageRadio - 0.0) / (double)num, (centerPoint.Y / controlToImageRadio - 0.0) / (double)num2);
			circleCaliperROI.Radius = radius / controlToImageRadio / (double)num;
			circleCaliperROI.CaliperNum = 30u;
			circleCaliperROI.CaliperHeight = circleCaliperROI.Radius / 3.0;
			circleCaliperROI.CaliperWidth = 5.0 / (double)num2;
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
			ROIControlBase.CompleteDraw(circleCaliperROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static void OnCircleRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleCaliperROI circleCaliperROI = d as CircleCaliperROI;
		circleCaliperROI.ChangeContainerSize();
		circleCaliperROI.ChangeContainerPosition();
		circleCaliperROI.DrawCaliper();
	}

	private static void OnStartAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleCaliperROI circleCaliperROI = d as CircleCaliperROI;
		circleCaliperROI.DrawCaliper();
	}

	private static void OnCircleCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CircleCaliperROI circleCaliperROI = d as CircleCaliperROI;
		circleCaliperROI.ChangeContainerPosition();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		sizeRect = Template.FindName("sizeRect", this) as Rectangle;
		sizeRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		caliperRect = Template.FindName("caliperRect", this) as Area;
		caliperRect.RenderTransform = caliperRotate;
		caliperRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		drawCanvas = Template.FindName("drawCanvas", this) as DrawingCanvas;
		parent = this.GetParent<ImageViewItemsPage>();
		left = Template.FindName("left", this) as Area;
		left.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		top = Template.FindName("top", this) as Area;
		top.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		right = Template.FindName("right", this) as Area;
		right.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		bottom = Template.FindName("bottom", this) as Area;
		bottom.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		RT = Template.FindName("RT", this) as RotateTransform;
		RT.Angle = CaliperRotateAngle;
		DrawCaliper();
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
		switch (frameworkElement.Name)
		{
		case "move":
			CircleCenterPoint += vector;
			clickPoint = position;
			break;
		case "sizeRect":
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
			break;
		}
		case "caliperRect":
			SetCaliperSize(position, width: true, height: true);
			break;
		case "top":
		case "bottom":
			SetCaliperSize(position, width: false, height: true);
			break;
		case "left":
		case "right":
			SetCaliperSize(position, width: true, height: false);
			break;
		}
	}

	private void SetCaliperSize(Point curPoint, bool width, bool height)
	{
		curPoint = parent.TranslatePoint(curPoint, this);
		Vector vector = new Vector(RectCenterPoint.X - ContainerWidth / 2.0, RectCenterPoint.Y - ContainerHeight / 2.0);
		Vector vector2 = new Vector(curPoint.X - RectCenterPoint.X, curPoint.Y - RectCenterPoint.Y);
		double num = Math.Abs(Vector.AngleBetween(vector, vector2)) * Math.PI / 180.0;
		double distance = GetDistance(curPoint.X, curPoint.Y, RectCenterPoint.X, RectCenterPoint.Y);
		if (width)
		{
			double num2 = 2.0 * distance * Math.Abs(Math.Sin(num)) / ControlToImageRadio;
			if (num2 < 1.0)
			{
				num2 = 1.0;
			}
			CaliperWidth = num2;
		}
		if (height)
		{
			double num3 = 2.0 * distance * Math.Abs(Math.Cos(num)) / ControlToImageRadio;
			if (num3 < 1.0)
			{
				num3 = 1.0;
			}
			CaliperHeight = num3;
		}
	}

	private double GetDistance(double A_x, double A_y, double B_x, double B_y)
	{
		double num = Math.Abs(B_x - A_x);
		double num2 = Math.Abs(B_y - A_y);
		return Math.Sqrt(num * num + num2 * num2);
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
				DrawCaliper();
			}
		}
		else
		{
			ChangeContainerSize();
			ChangeContainerPosition();
			DrawCaliper();
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

	private static void OnCaliperHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as CircleCaliperROI).DrawCaliper();
	}

	private static void OnCaliperWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as CircleCaliperROI).DrawCaliper();
	}

	private static void OnCaliperNumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as CircleCaliperROI).DrawCaliper();
	}

	private static void OnCaliperRotateAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is CircleCaliperROI { RT: not null } circleCaliperROI)
		{
			circleCaliperROI.RT.Angle = (double)e.NewValue;
		}
	}

	private void DrawCaliper()
	{
		if (double.IsNaN(CircleRadius) || double.IsNaN(CaliperWidth) || double.IsNaN(CaliperHeight) || drawCanvas == null)
		{
			return;
		}
		if (CircleRadius <= 0.0)
		{
			(DataContext as ICircleCaliperROI).CaliperWidth = 0.0;
			(DataContext as ICircleCaliperROI).CaliperHeight = 0.0;
			RectCenterPoint = new Point(0.0, 0.0);
			caliperRect.Visibility = Visibility.Hidden;
			SetRect();
			return;
		}
		if (caliperRect.Visibility == Visibility.Hidden)
		{
			caliperRect.Visibility = Visibility.Visible;
		}
		int CaliperMaxWidth = 500;
		if (CaliperWidth > (double)CaliperMaxWidth)
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				CaliperWidth = CaliperMaxWidth;
			}));
			return;
		}
		drawCanvas.ClearVisuals();
		if (IsLimitFlag)
		{
			double num = 2.0 * CircleRadius;
			if (CaliperHeight > num)
			{
				CaliperHeight = num;
			}
		}
		pen.Thickness = StrokeThickness;
		if (pen.Brush == null)
		{
			pen.Brush = (Brush)GetResource("ROIBaseColor");
		}
		double num2 = 360.0 / (double)CaliperNum;
		for (int num3 = 1; num3 < CaliperNum + 1; num3++)
		{
			double angle = num2 * (double)(num3 - 1) + num2 / 2.0 + StartAngle;
			RotateTransform rotateTransform = new RotateTransform();
			rotateTransform.CenterX = ContainerWidth / 2.0;
			rotateTransform.CenterY = ContainerWidth / 2.0;
			rotateTransform.Angle = angle;
			Point point = rotateTransform.Transform(new Point(ContainerWidth, ContainerHeight / 2.0));
			double num4 = CaliperWidth * ControlToImageRadio;
			double num5 = CaliperHeight * ControlToImageRadio;
			double num6 = point.Y - num5 / 2.0;
			double num7 = point.X - num4 / 2.0;
			double rotate = GetRotate(point);
			RotateTransform rotateTransform2 = new RotateTransform();
			rotateTransform2.CenterX = num7 + num4 / 2.0;
			rotateTransform2.CenterY = num6 + num5 / 2.0;
			rotateTransform2.Angle = rotate;
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.DrawRectangle(brush, pen, new Rect(new Point(num7, num6), new Size(num4, num5)));
			}
			drawingVisual.Transform = rotateTransform2;
			if ((double)num3 == Math.Ceiling((double)(CaliperNum * 3) / 4.0 - StartAngle / num2))
			{
				CaliperRotateAngle = rotate;
				caliperRect.SetValue(Canvas.TopProperty, num6);
				caliperRect.SetValue(Canvas.LeftProperty, num7 + num4);
				caliperRotate.CenterX = (caliperRect.ActualWidth - num4) / 2.0;
				caliperRotate.CenterY = (caliperRect.ActualHeight + num5) / 2.0;
				caliperRotate.Angle = CaliperRotateAngle;
				RectCenterPoint = point;
			}
			else
			{
				drawCanvas.AddVisual(drawingVisual);
			}
			SetRect();
		}
	}

	private void SetRect()
	{
		OnPropertyChanged("RectWidth");
		OnPropertyChanged("RectHeight");
		OnPropertyChanged("RectTopLeft");
	}

	private double GetRotate(Point p)
	{
		Vector vector = new Vector(p.X - ContainerWidth / 2.0, p.Y - ContainerHeight / 2.0);
		Vector vector2 = new Vector(0.0, -1.0);
		return Vector.AngleBetween(vector2, vector);
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
