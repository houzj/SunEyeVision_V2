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

[ROIDisplayForInterfaceType(typeof(ISectorCaliperROI))]
public class SectorCaliperROI : ROIControlBase
{
	private static SectorCaliperROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty IsLargeArcProperty;

	public static readonly DependencyProperty IsClockwiseProperty;

	public static readonly DependencyProperty RadiusProperty;

	public static readonly DependencyProperty StartPointProperty;

	public static readonly DependencyProperty EndPointProperty;

	public static readonly DependencyProperty CaliperHeightProperty;

	public static readonly DependencyProperty CaliperWidthProperty;

	public static readonly DependencyProperty CaliperNumProperty;

	public static readonly DependencyProperty CaliperRotateAngleProperty;

	public static readonly DependencyProperty ArcRectAngleProperty;

	public static readonly DependencyProperty IsLimitFlagProperty;

	private Point _Start;

	private Point _End;

	private Point _CircleCenter;

	private bool _IsCircle;

	private Area left;

	private Area top;

	private Area right;

	private Area bottom;

	private Ellipse startPoint;

	private Ellipse endPoint;

	private Canvas move;

	private Area caliperRect;

	private Area arcRect;

	private DrawingCanvas drawCanvas;

	private Point clickPoint;

	private Point curStartPoint;

	private Point curEndPoint;

	private Vector startVector;

	private bool curWise;

	private RotateTransform caliperRotate = new RotateTransform();

	private ImageViewItemsPage parent;

	private PathFigure PFigure;

	private ArcSegment ASegment;

	private EllipseGeometry Eg;

	private RotateTransform RT;

	private Size _ArcSize;

	private Brush brush = Brushes.Transparent;

	private Pen pen = new Pen();

	private Pen pen2 = new Pen
	{
		Thickness = 0.0
	};

	private double CaliperMaxWidth = 0.0;

	private Point RectCenterPoint;

	private bool canDrawCaliper = true;

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

	public bool IsLargeArc
	{
		get
		{
			return (bool)GetValue(IsLargeArcProperty);
		}
		set
		{
			SetValue(IsLargeArcProperty, value);
		}
	}

	public bool IsClockwise
	{
		get
		{
			return (bool)GetValue(IsClockwiseProperty);
		}
		set
		{
			SetValue(IsClockwiseProperty, value);
		}
	}

	public double Radius
	{
		get
		{
			return (double)GetValue(RadiusProperty);
		}
		set
		{
			SetValue(RadiusProperty, value);
		}
	}

	public Point StartPoint
	{
		get
		{
			return (Point)GetValue(StartPointProperty);
		}
		set
		{
			SetValue(StartPointProperty, value);
		}
	}

	public Point EndPoint
	{
		get
		{
			return (Point)GetValue(EndPointProperty);
		}
		set
		{
			SetValue(EndPointProperty, value);
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

	public double ArcRectAngle
	{
		get
		{
			return (double)GetValue(ArcRectAngleProperty);
		}
		set
		{
			SetValue(ArcRectAngleProperty, value);
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

	public Point Start
	{
		get
		{
			return _Start;
		}
		set
		{
			if (_Start != value)
			{
				_Start = value;
				if (PFigure != null)
				{
					PFigure.StartPoint = _Start;
				}
				OnPropertyChanged("Start");
			}
		}
	}

	public Point End
	{
		get
		{
			return _End;
		}
		set
		{
			if (_End != value)
			{
				_End = value;
				if (ASegment != null)
				{
					ASegment.Point = _End;
				}
				OnPropertyChanged("End");
			}
		}
	}

	public Point CircleCenter
	{
		get
		{
			return _CircleCenter;
		}
		set
		{
			if (_CircleCenter != value)
			{
				_CircleCenter = value;
				if (Eg != null)
				{
					Eg.Center = _CircleCenter;
				}
				OnPropertyChanged("CircleCenter");
			}
		}
	}

	public bool IsCircle
	{
		get
		{
			return _IsCircle;
		}
		set
		{
			if (_IsCircle != value)
			{
				_IsCircle = value;
				OnPropertyChanged("IsCircle");
			}
		}
	}

	public Size ArcSize
	{
		get
		{
			return _ArcSize;
		}
		set
		{
			if (_ArcSize != value)
			{
				_ArcSize = value;
				if (ASegment != null)
				{
					ASegment.Size = _ArcSize;
				}
				if (Eg != null)
				{
					Eg.RadiusX = _ArcSize.Width;
					Eg.RadiusY = _ArcSize.Height;
				}
				OnPropertyChanged("ArcSize");
			}
		}
	}

	static SectorCaliperROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(SectorCaliperROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(SectorCaliperROI), new PropertyMetadata(0.0));
		IsLargeArcProperty = DependencyProperty.Register("IsLargeArc", typeof(bool), typeof(SectorCaliperROI), new PropertyMetadata(false, OnIsLargeArcChanged));
		IsClockwiseProperty = DependencyProperty.Register("IsClockwise", typeof(bool), typeof(SectorCaliperROI), new PropertyMetadata(true, OnIsClockwiseChanged));
		RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(SectorCaliperROI), new PropertyMetadata(double.NaN, OnCircleRadiusChanged));
		StartPointProperty = DependencyProperty.Register("StartPoint", typeof(Point), typeof(SectorCaliperROI), new PropertyMetadata(new Point(0.0, 0.0), OnStartPointChanged));
		EndPointProperty = DependencyProperty.Register("EndPoint", typeof(Point), typeof(SectorCaliperROI), new PropertyMetadata(new Point(0.0, 0.0), OnEndPointChanged));
		CaliperHeightProperty = DependencyProperty.Register("CaliperHeight", typeof(double), typeof(SectorCaliperROI), new PropertyMetadata(OnCaliperHeightChanged));
		CaliperWidthProperty = DependencyProperty.Register("CaliperWidth", typeof(double), typeof(SectorCaliperROI), new PropertyMetadata(double.NaN, OnCaliperWidthChanged));
		CaliperNumProperty = DependencyProperty.Register("CaliperNum", typeof(uint), typeof(SectorCaliperROI), new PropertyMetadata(OnCaliperNumChanged));
		CaliperRotateAngleProperty = DependencyProperty.Register("CaliperRotateAngle", typeof(double), typeof(SectorCaliperROI), new PropertyMetadata(0.0, OnCliperRotateAngleChanged));
		ArcRectAngleProperty = DependencyProperty.Register("ArcRectAngle", typeof(double), typeof(SectorCaliperROI), new PropertyMetadata(0.0));
		IsLimitFlagProperty = DependencyProperty.Register("IsLimitFlag", typeof(bool), typeof(SectorCaliperROI), new PropertyMetadata(true));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(SectorCaliperROI), new FrameworkPropertyMetadata(typeof(SectorCaliperROI)));
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new SectorCaliperROISubstitute();
		ROISubstitute.Start = ROIControlBase.mouseOffset;
		ROISubstitute.End = ROIControlBase.mouseOffset;
		ROIControlBase.BaseAfterDraw(ROISubstitute, parent);
		parent.MouseMove += Parent_MouseMove;
		parent.MouseDown += Parent_MouseDown;
	}

	private static void Parent_MouseMove(object sender, MouseEventArgs e)
	{
		if (ROISubstitute != null)
		{
			if (!ROIControlBase.IsMoved)
			{
				ROIControlBase.IsMoved = true;
			}
			Point position = e.GetPosition(sender as IInputElement);
			ROISubstitute.End = position;
			double num = (position - ROIControlBase.mouseOffset).Length / 2.0;
			double radius = num * 2.0;
			ROISubstitute.Radius = radius;
		}
	}

	private static void Parent_MouseDown(object sender, MouseButtonEventArgs e)
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
			Point start = ROISubstitute.Start;
			Point end = ROISubstitute.End;
			double radius = ROISubstitute.Radius;
			double controlToImageRadio = ROISubstitute.ControlToImageRadio;
			ISectorCaliperROI sectorCaliperROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ISectorCaliperROI;
			int num = 1;
			int num2 = 1;
			if (sectorCaliperROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			sectorCaliperROI.StartPoint = new Point((start.X / controlToImageRadio - 0.0) / (double)num, (start.Y / controlToImageRadio - 0.0) / (double)num2);
			sectorCaliperROI.EndPoint = new Point((end.X / controlToImageRadio - 0.0) / (double)num, (end.Y / controlToImageRadio - 0.0) / (double)num2);
			sectorCaliperROI.Radius = radius / controlToImageRadio / (double)num;
			sectorCaliperROI.CaliperNum = 6u;
			sectorCaliperROI.CaliperHeight = sectorCaliperROI.Radius / 4.0;
			sectorCaliperROI.CaliperWidth = 5.0 / (double)num2;
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseDown -= Parent_MouseDown;
			ROIControlBase.CompleteDraw(sectorCaliperROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static void OnIsLargeArcChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROI sectorCaliperROI)
		{
			if (sectorCaliperROI.ASegment != null)
			{
				sectorCaliperROI.ASegment.IsLargeArc = (bool)e.NewValue;
			}
			sectorCaliperROI.DrawCaliper();
		}
	}

	private static void OnIsClockwiseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROI sectorCaliperROI)
		{
			sectorCaliperROI.ArcSize = sectorCaliperROI.GetArcSize();
			if (sectorCaliperROI.ASegment != null)
			{
				sectorCaliperROI.ASegment.SweepDirection = sectorCaliperROI.GetWise();
			}
			sectorCaliperROI.DrawCaliper();
		}
	}

	private SweepDirection GetWise()
	{
		return IsClockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
	}

	private static void OnCircleRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROI sectorCaliperROI)
		{
			sectorCaliperROI.ArcSize = sectorCaliperROI.GetArcSize();
			sectorCaliperROI.DrawCaliper();
		}
	}

	private static void OnStartPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROI sectorCaliperROI)
		{
			sectorCaliperROI.Start = sectorCaliperROI.GetControlPoint((Point)e.NewValue);
			sectorCaliperROI.DrawCaliper();
		}
	}

	private static void OnEndPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROI sectorCaliperROI)
		{
			sectorCaliperROI.End = sectorCaliperROI.GetControlPoint((Point)e.NewValue);
			sectorCaliperROI.DrawCaliper();
		}
	}

	private static void OnCaliperHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as SectorCaliperROI).DrawCaliper();
	}

	private static void OnCaliperWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as SectorCaliperROI).DrawCaliper();
	}

	private static void OnCaliperNumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as SectorCaliperROI).DrawCaliper();
	}

	private static void OnCliperRotateAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROI { RT: not null } sectorCaliperROI)
		{
			sectorCaliperROI.RT.Angle = (double)e.NewValue;
		}
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Canvas;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		caliperRect = Template.FindName("caliperRect", this) as Area;
		caliperRect.RenderTransform = caliperRotate;
		caliperRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		startPoint = Template.FindName("startPoint", this) as Ellipse;
		startPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		endPoint = Template.FindName("endPoint", this) as Ellipse;
		endPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		arcRect = Template.FindName("arcRect", this) as Area;
		arcRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
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
		PFigure = Template.FindName("PFigure", this) as PathFigure;
		PFigure.StartPoint = Start;
		ASegment = Template.FindName("ASegment", this) as ArcSegment;
		ASegment.Size = ArcSize;
		ASegment.Point = End;
		ASegment.IsLargeArc = IsLargeArc;
		ASegment.SweepDirection = GetWise();
		Eg = Template.FindName("Eg", this) as EllipseGeometry;
		Eg.Center = CircleCenter;
		Eg.RadiusX = ArcSize.Width;
		Eg.RadiusY = ArcSize.Height;
		RT = Template.FindName("RT", this) as RotateTransform;
		RT.Angle = CaliperRotateAngle;
		DrawCaliper();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		curStartPoint = StartPoint;
		curEndPoint = EndPoint;
		Point point = new Point((End.X + Start.X) / 2.0, (End.Y + Start.Y) / 2.0);
		startVector = new Vector(clickPoint.X - point.X, clickPoint.Y - point.Y);
		curWise = IsClockwise;
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
		switch (frameworkElement.Name)
		{
		case "move":
		{
			double num4 = (position.Y - clickPoint.Y) / ControlToImageRadio;
			double num5 = (position.X - clickPoint.X) / ControlToImageRadio;
			double x = curStartPoint.X + num5;
			double y = curStartPoint.Y + num4;
			double x2 = curEndPoint.X + num5;
			double y2 = curEndPoint.Y + num4;
			canDrawCaliper = false;
			StartPoint = new Point(x, y);
			canDrawCaliper = true;
			EndPoint = new Point(x2, y2);
			break;
		}
		case "startPoint":
			StartPoint = GetImagePoint(position);
			break;
		case "endPoint":
			EndPoint = GetImagePoint(position);
			break;
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
		case "arcRect":
		{
			Point point = new Point((End.X + Start.X) / 2.0, (End.Y + Start.Y) / 2.0);
			Vector vector = new Vector(End.X - Start.X, End.Y - Start.Y);
			Vector vector2 = new Vector(position.X - point.X, position.Y - point.Y);
			double num = vector.Length / 2.0;
			double length = vector2.Length;
			if (length == 0.0)
			{
				break;
			}
			double num2 = Math.Abs(Vector.AngleBetween(startVector, vector2));
			if (num2 > 90.0)
			{
				IsClockwise = !curWise;
			}
			else
			{
				IsClockwise = curWise;
			}
			double num3 = (num * num + length * length) / (2.0 * length);
			if (num3 < length)
			{
				if (!IsLargeArc)
				{
					IsLargeArc = true;
				}
			}
			else if (IsLargeArc)
			{
				IsLargeArc = false;
			}
			num3 /= ControlToImageRadio;
			num3 = ((num3 > 10000.0) ? 10000.0 : num3);
			Radius = num3;
			break;
		}
		}
	}

	private void SetCaliperSize(Point curPoint, bool width, bool height)
	{
		Point centerPoint = GetCenterPoint();
		Vector vector = new Vector(RectCenterPoint.X - centerPoint.X, RectCenterPoint.Y - centerPoint.Y);
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

	private static double GetDistance(double A_x, double A_y, double B_x, double B_y)
	{
		double num = B_x - A_x;
		double num2 = B_y - A_y;
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
			ArcSize = GetArcSize();
			Start = GetControlPoint(StartPoint);
			End = GetControlPoint(EndPoint);
			DrawCaliper();
		}
	}

	private Size GetArcSize()
	{
		return new Size(Radius * ControlToImageRadio, Radius * ControlToImageRadio);
	}

	private void DrawCaliper()
	{
		if (double.IsNaN(CaliperWidth) || double.IsNaN(Radius) || drawCanvas == null || !canDrawCaliper)
		{
			return;
		}
		int num = 500;
		if (CaliperWidth > (double)num)
		{
			CaliperWidth = num;
			return;
		}
		if (IsLimitFlag)
		{
			double CaliperMaxHeight = 2.0 * Radius;
			if (CaliperHeight > CaliperMaxHeight)
			{
				Dispatcher.BeginInvoke((Action)(() =>
				{
					CaliperHeight = CaliperMaxHeight;
				}));
				return;
			}
		}
		drawCanvas.ClearVisuals();
		double num2 = GetDistance(StartPoint.X, StartPoint.Y, EndPoint.X, EndPoint.Y) / 2.0;
		if (Radius < num2)
		{
			Radius = num2;
		}
		pen.Thickness = StrokeThickness;
		if (pen.Brush == null)
		{
			pen.Brush = (Brush)GetResource("ROIBaseColor");
		}
		Point start = Start;
		Point centerPoint = GetCenterPoint();
		double angleRange = GetAngleRange(centerPoint);
		double num3 = angleRange / (double)CaliperNum;
		RotateTransform rotateTransform = new RotateTransform();
		rotateTransform.CenterX = centerPoint.X;
		rotateTransform.CenterY = centerPoint.Y;
		rotateTransform.Angle = (IsClockwise ? (angleRange / 2.0) : ((0.0 - angleRange) / 2.0));
		Point point = rotateTransform.Transform(start);
		arcRect.SetValue(Canvas.LeftProperty, point.X);
		arcRect.SetValue(Canvas.TopProperty, point.Y);
		Vector vector = new Vector(point.X - centerPoint.X, point.Y - centerPoint.Y);
		Vector vector2 = new Vector(0.0, 1.0);
		ArcRectAngle = Vector.AngleBetween(vector2, vector);
		SetArcRectAngle();
		for (int num4 = 1; num4 < CaliperNum + 1; num4++)
		{
			double num5 = num3 * (double)(num4 - 1) + num3 / 2.0;
			num5 = (IsClockwise ? num5 : (0.0 - num5));
			rotateTransform.CenterX = centerPoint.X;
			rotateTransform.CenterY = centerPoint.Y;
			rotateTransform.Angle = num5;
			point = rotateTransform.Transform(start);
			double num6 = CaliperWidth * ControlToImageRadio;
			double num7 = CaliperHeight * ControlToImageRadio;
			double num8 = point.Y - num7 / 2.0;
			double num9 = point.X - num6 / 2.0;
			double rotate = GetRotate(point, centerPoint);
			RotateTransform rotateTransform2 = new RotateTransform();
			rotateTransform2.CenterX = num9 + num6 / 2.0;
			rotateTransform2.CenterY = num8 + num7 / 2.0;
			rotateTransform2.Angle = rotate;
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.DrawRectangle(brush, pen, new Rect(new Point(num9, num8), new Size(num6, num7)));
			}
			drawingVisual.Transform = rotateTransform2;
			if (num4 == CaliperNum / 2 + 1)
			{
				CaliperRotateAngle = rotate;
				caliperRect.SetValue(Canvas.TopProperty, num8);
				caliperRect.SetValue(Canvas.LeftProperty, num9 + num6);
				caliperRotate.CenterX = (caliperRect.ActualWidth - num6) / 2.0;
				caliperRotate.CenterY = (caliperRect.ActualHeight + num7) / 2.0;
				caliperRotate.Angle = rotate;
				RectCenterPoint = point;
			}
			else
			{
				drawCanvas.AddVisual(drawingVisual);
			}
		}
		OnPropertyChanged("RectWidth");
		OnPropertyChanged("RectHeight");
		OnPropertyChanged("RectTopLeft");
		DrawCircle();
	}

	private void SetArcRectAngle()
	{
		arcRect.RenderTransformOrigin = new Point(0.5, 0.5);
		arcRect.RenderTransform = new RotateTransform(ArcRectAngle);
	}

	private double GetAngleRange(Point circleCenter)
	{
		Vector vector = new Vector(Start.X - circleCenter.X, Start.Y - circleCenter.Y);
		Vector vector2 = new Vector(End.X - circleCenter.X, End.Y - circleCenter.Y);
		double num = Math.Abs(Vector.AngleBetween(vector, vector2));
		return IsLargeArc ? (360.0 - num) : num;
	}

	private Point GetCenterPoint()
	{
		Vector vector = new Vector(End.X - Start.X, End.Y - Start.Y);
		Vector vector2;
		if (vector.X != 0.0)
		{
			vector2 = new Vector((0.0 - vector.Y) / vector.X, 1.0);
			if (vector.X < 0.0)
			{
				vector2.Negate();
			}
		}
		else
		{
			vector2 = new Vector(-1.0, 0.0);
			if (vector.Y < 0.0)
			{
				vector2.Negate();
			}
		}
		if (IsLargeArc && IsClockwise)
		{
			vector2.Negate();
		}
		else if ((!IsLargeArc || IsClockwise) && (IsLargeArc || !IsClockwise) && !IsLargeArc && !IsClockwise)
		{
			vector2.Negate();
		}
		vector2.Normalize();
		double num = vector.Length / 2.0;
		double num2 = Radius * ControlToImageRadio;
		double num3 = Math.Sqrt(Math.Abs(num2 * num2 - num * num));
		Point point = new Point((End.X + Start.X) / 2.0, (End.Y + Start.Y) / 2.0);
		return point + vector2 * num3;
	}

	private double GetRotate(Point p, Point center)
	{
		Vector vector = new Vector(p.X - center.X, p.Y - center.Y);
		Vector vector2 = new Vector(0.0, -1.0);
		return Vector.AngleBetween(vector2, vector);
	}

	private void DrawCircle()
	{
		if (StartPoint == EndPoint && IsLargeArc)
		{
			IsCircle = true;
			CircleCenter = GetCenterPoint();
		}
		else
		{
			IsCircle = false;
		}
	}

	protected override bool Move(ROIMoveDirection rOIMoveDirection)
	{
		switch (rOIMoveDirection)
		{
		case ROIMoveDirection.Left:
			StartPoint = new Point(StartPoint.X - 1.0, StartPoint.Y);
			EndPoint = new Point(EndPoint.X - 1.0, EndPoint.Y);
			break;
		case ROIMoveDirection.Right:
			StartPoint = new Point(StartPoint.X + 1.0, StartPoint.Y);
			EndPoint = new Point(EndPoint.X + 1.0, EndPoint.Y);
			break;
		case ROIMoveDirection.Up:
			StartPoint = new Point(StartPoint.X, StartPoint.Y - 1.0);
			EndPoint = new Point(EndPoint.X, EndPoint.Y - 1.0);
			break;
		case ROIMoveDirection.Down:
			StartPoint = new Point(StartPoint.X, StartPoint.Y + 1.0);
			EndPoint = new Point(EndPoint.X, EndPoint.Y + 1.0);
			break;
		default:
			return false;
		}
		return true;
	}
}
