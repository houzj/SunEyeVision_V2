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

[ROIDisplayForInterfaceType(typeof(ISectorROI))]
public class SectorROI : ROIControlBase
{
	private static SectorROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty MinEndPointProperty;

	public static readonly DependencyProperty MinStartPointProperty;

	public static readonly DependencyProperty MinMiddlePointProperty;

	public static readonly DependencyProperty IsLargeArcProperty;

	public static readonly DependencyProperty StartPointProperty;

	public static readonly DependencyProperty EndPointProperty;

	public static readonly DependencyProperty ArcPointProperty;

	public static readonly DependencyProperty MiddlePointProperty;

	public static readonly DependencyProperty MiddleStartPointProperty;

	public static readonly DependencyProperty MiddleEndPointProperty;

	public static readonly DependencyProperty MinSizeProperty;

	public static readonly DependencyProperty MaxSizeProperty;

	public static readonly DependencyProperty CircleRadiusProperty;

	public static readonly DependencyProperty MinRadiusProperty;

	private RotateTransform tlt = new RotateTransform();

	public static readonly DependencyProperty StartAngleProperty;

	public static readonly DependencyProperty EndAngleProperty;

	public static readonly DependencyProperty MiddleEndAngleProperty;

	public static readonly DependencyProperty MiddleStartAngleProperty;

	public static readonly DependencyProperty MinMiddleAngleProperty;

	private bool IsFresh = true;

	public static readonly DependencyProperty CircleCenterPointProperty;

	private Grid move;

	private Area minMiddlePoint;

	private Area middlePoint;

	private Area middleStartPoint;

	private Area middleEndPoint;

	private Path arcPoint;

	private Path arcArrow;

	private Path startArrow;

	private Path endArrow;

	private Ellipse startPoint;

	private Ellipse endPoint;

	private Point clickPoint;

	private ImageViewItemsPage parent;

	private double curRadius;

	private double curMinRadius;

	public const double MaxRadius = 10000.0;

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

	public Point MinEndPoint
	{
		get
		{
			return (Point)GetValue(MinEndPointProperty);
		}
		set
		{
			SetValue(MinEndPointProperty, value);
		}
	}

	public Point MinStartPoint
	{
		get
		{
			return (Point)GetValue(MinStartPointProperty);
		}
		set
		{
			SetValue(MinStartPointProperty, value);
		}
	}

	public Point MinMiddlePoint
	{
		get
		{
			return (Point)GetValue(MinMiddlePointProperty);
		}
		set
		{
			SetValue(MinMiddlePointProperty, value);
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

	public Point ArcPoint
	{
		get
		{
			return (Point)GetValue(ArcPointProperty);
		}
		set
		{
			SetValue(ArcPointProperty, value);
		}
	}

	public Point MiddlePoint
	{
		get
		{
			return (Point)GetValue(MiddlePointProperty);
		}
		set
		{
			SetValue(MiddlePointProperty, value);
		}
	}

	public Point MiddleStartPoint
	{
		get
		{
			return (Point)GetValue(MiddleStartPointProperty);
		}
		set
		{
			SetValue(MiddleStartPointProperty, value);
		}
	}

	public Point MiddleEndPoint
	{
		get
		{
			return (Point)GetValue(MiddleEndPointProperty);
		}
		set
		{
			SetValue(MiddleEndPointProperty, value);
		}
	}

	public Size MinSize
	{
		get
		{
			return (Size)GetValue(MinSizeProperty);
		}
		set
		{
			SetValue(MinSizeProperty, value);
		}
	}

	public Size MaxSize
	{
		get
		{
			return (Size)GetValue(MaxSizeProperty);
		}
		set
		{
			SetValue(MaxSizeProperty, value);
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

	private double AngleForUI
	{
		get
		{
			double num = (StartAngle + EndAngle) / 2.0;
			return (EndAngle > StartAngle) ? num : (num + 180.0);
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

	public double EndAngle
	{
		get
		{
			return (double)GetValue(EndAngleProperty);
		}
		set
		{
			SetValue(EndAngleProperty, value);
		}
	}

	public double MiddleEndAngle
	{
		get
		{
			return (double)GetValue(MiddleEndAngleProperty);
		}
		set
		{
			SetValue(MiddleEndAngleProperty, value);
		}
	}

	public double MiddleStartAngle
	{
		get
		{
			return (double)GetValue(MiddleStartAngleProperty);
		}
		set
		{
			SetValue(MiddleStartAngleProperty, value);
		}
	}

	public double MinMiddleAngle
	{
		get
		{
			return (double)GetValue(MinMiddleAngleProperty);
		}
		set
		{
			SetValue(MinMiddleAngleProperty, value);
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

	static SectorROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		MinEndPointProperty = DependencyProperty.Register("MinEndPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(0.0, 0.0)));
		MinStartPointProperty = DependencyProperty.Register("MinStartPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(0.0, 0.0)));
		MinMiddlePointProperty = DependencyProperty.Register("MinMiddlePoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(0.0, 0.0)));
		IsLargeArcProperty = DependencyProperty.Register("IsLargeArc", typeof(bool), typeof(SectorROI), new PropertyMetadata(false));
		StartPointProperty = DependencyProperty.Register("StartPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(100.0, 0.0), OnStartPointChanged));
		EndPointProperty = DependencyProperty.Register("EndPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(100.0, 0.0), OnEndPointChanged));
		ArcPointProperty = DependencyProperty.Register("ArcPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(0.0, 0.0)));
		MiddlePointProperty = DependencyProperty.Register("MiddlePoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(0.0, 0.0)));
		MiddleStartPointProperty = DependencyProperty.Register("MiddleStartPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(0.0, 0.0)));
		MiddleEndPointProperty = DependencyProperty.Register("MiddleEndPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(new Point(0.0, 0.0)));
		MinSizeProperty = DependencyProperty.Register("MinSize", typeof(Size), typeof(SectorROI), new PropertyMetadata(new Size(0.0, 0.0)));
		MaxSizeProperty = DependencyProperty.Register("MaxSize", typeof(Size), typeof(SectorROI), new PropertyMetadata(new Size(0.0, 0.0)));
		CircleRadiusProperty = DependencyProperty.Register("CircleRadius", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0, OnCircleRadiusChanged));
		MinRadiusProperty = DependencyProperty.Register("MinRadius", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0, OnMinRadiusChanged));
		StartAngleProperty = DependencyProperty.Register("StartAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0, OnStartAngleChanged));
		EndAngleProperty = DependencyProperty.Register("EndAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0, OnEndAngleChanged));
		MiddleEndAngleProperty = DependencyProperty.Register("MiddleEndAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		MiddleStartAngleProperty = DependencyProperty.Register("MiddleStartAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		MinMiddleAngleProperty = DependencyProperty.Register("MinMiddleAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		CircleCenterPointProperty = DependencyProperty.Register("CircleCenterPoint", typeof(Point), typeof(SectorROI), new PropertyMetadata(default(Point), OnCircleCenterPointChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(SectorROI), new FrameworkPropertyMetadata(typeof(SectorROI)));
	}

	private void Init()
	{
		CalPoints();
		ChangeStartAngleCursor();
		ChangeEndAngleCursor();
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new SectorROISubstitute();
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
			ISectorROI sectorROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ISectorROI;
			int num = 1;
			int num2 = 1;
			if (sectorROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			sectorROI.CenterPoint = new Point((centerPoint.X / controlToImageRadio - 0.0) / (double)num, (centerPoint.Y / controlToImageRadio - 0.0) / (double)num2);
			sectorROI.Radius = radius / controlToImageRadio / (double)num;
			sectorROI.StartAngle = 0.0;
			sectorROI.AngleRange = 90.0;
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
			ROIControlBase.CompleteDraw(sectorROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static void OnStartPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
	}

	private static void OnEndPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
	}

	private static void OnCircleRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROI sectorROI = d as SectorROI;
		sectorROI.ChangeContainerSize();
		sectorROI.ChangeContainerPosition();
		sectorROI.CalPoints();
	}

	private void CalPoints()
	{
		if (IsFresh)
		{
			MaxSize = new Size(ContainerWidth / 2.0, ContainerWidth / 2.0);
			tlt.CenterX = ContainerWidth / 2.0;
			tlt.CenterY = ContainerHeight / 2.0;
			tlt.Angle = StartAngle;
			Point point = new Point(ContainerWidth, ContainerHeight / 2.0);
			StartPoint = tlt.Transform(point);
			tlt.Angle = EndAngle;
			EndPoint = tlt.Transform(point);
			tlt.Angle = AngleForUI;
			MiddlePoint = tlt.Transform(point);
			SetArc();
			CalMinPoints();
		}
	}

	private static void OnMinRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROI sectorROI = d as SectorROI;
		sectorROI.CalMinPoints();
	}

	private void CalMinPoints()
	{
		if (IsFresh)
		{
			MinSize = new Size(InnerCircleSize / 2.0, InnerCircleSize / 2.0);
			tlt.CenterX = ContainerWidth / 2.0;
			tlt.CenterY = ContainerHeight / 2.0;
			tlt.Angle = StartAngle;
			Point point = new Point(ContainerWidth / 2.0 + InnerCircleSize / 2.0, ContainerHeight / 2.0);
			MinStartPoint = tlt.Transform(point);
			tlt.Angle = EndAngle;
			MinEndPoint = tlt.Transform(point);
			tlt.Angle = AngleForUI;
			MinMiddlePoint = tlt.Transform(point);
			point = new Point((3.0 * ContainerWidth + InnerCircleSize) / 4.0, ContainerHeight / 2.0);
			tlt.Angle = StartAngle;
			MiddleStartPoint = tlt.Transform(point);
			tlt.Angle = EndAngle;
			MiddleEndPoint = tlt.Transform(point);
			SetArrow();
		}
	}

	private static void OnStartAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROI sectorROI = d as SectorROI;
		sectorROI.CalPoints();
		sectorROI.SetIsLargeArc();
		sectorROI.ChangeStartAngleCursor();
	}

	private static void OnEndAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROI sectorROI = d as SectorROI;
		sectorROI.CalPoints();
		sectorROI.SetIsLargeArc();
		sectorROI.ChangeEndAngleCursor();
	}

	private void ChangeEndAngleCursor()
	{
		if (middleEndPoint != null)
		{
			Vector vector = new Vector(EndPoint.X - ContainerWidth / 2.0, EndPoint.Y - ContainerHeight / 2.0);
			Vector vector2 = new Vector(1.0, 0.0);
			MiddleEndAngle = Vector.AngleBetween(vector2, vector);
			middleEndPoint.RenderTransformOrigin = new Point(0.5, 0.5);
			middleEndPoint.RenderTransform = new RotateTransform(MiddleEndAngle);
			ChangeRadiusCursor();
		}
	}

	private void ChangeStartAngleCursor()
	{
		if (middleStartPoint != null)
		{
			Vector vector = new Vector(StartPoint.X - ContainerWidth / 2.0, StartPoint.Y - ContainerHeight / 2.0);
			Vector vector2 = new Vector(1.0, 0.0);
			MiddleStartAngle = Vector.AngleBetween(vector2, vector);
			middleStartPoint.RenderTransformOrigin = new Point(0.5, 0.5);
			middleStartPoint.RenderTransform = new RotateTransform(MiddleStartAngle);
			ChangeRadiusCursor();
		}
	}

	private void ChangeRadiusCursor()
	{
		Vector vector = new Vector(MiddlePoint.X - ContainerWidth / 2.0, MiddlePoint.Y - ContainerHeight / 2.0);
		Vector vector2 = new Vector(1.0, 0.0);
		MinMiddleAngle = Vector.AngleBetween(vector2, vector);
		minMiddlePoint.RenderTransformOrigin = new Point(0.5, 0.5);
		minMiddlePoint.RenderTransform = new RotateTransform(MinMiddleAngle);
		middlePoint.RenderTransformOrigin = new Point(0.5, 0.5);
		middlePoint.RenderTransform = new RotateTransform(MinMiddleAngle);
	}

	private void SetIsLargeArc()
	{
		if (IsFresh)
		{
			double num = ((EndAngle <= 0.0) ? (0.0 - EndAngle) : (360.0 - EndAngle));
			double num2 = ((StartAngle < 0.0) ? (360.0 + StartAngle) : StartAngle);
			if ((num2 + num) % 360.0 > 180.0)
			{
				IsLargeArc = false;
			}
			else
			{
				IsLargeArc = true;
			}
		}
	}

	private static void OnCircleCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROI sectorROI = d as SectorROI;
		sectorROI.ChangeContainerPosition();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		minMiddlePoint = Template.FindName("minMiddlePoint", this) as Area;
		minMiddlePoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		middlePoint = Template.FindName("middlePoint", this) as Area;
		middlePoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		middleStartPoint = Template.FindName("middleStartPoint", this) as Area;
		middleStartPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		middleEndPoint = Template.FindName("middleEndPoint", this) as Area;
		middleEndPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		arcPoint = Template.FindName("arcPoint", this) as Path;
		arcPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		arcArrow = Template.FindName("arcArrow", this) as Path;
		arcArrow.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		startArrow = Template.FindName("startArrow", this) as Path;
		startArrow.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		endArrow = Template.FindName("endArrow", this) as Path;
		endArrow.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		startPoint = Template.FindName("startPoint", this) as Ellipse;
		startPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		endPoint = Template.FindName("endPoint", this) as Ellipse;
		endPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
		Init();
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
		Vector vector2 = new Vector(MiddlePoint.X - ContainerWidth / 2.0, MiddlePoint.Y - ContainerHeight / 2.0);
		if (vector2.X == 0.0 && vector2.Y == 0.0)
		{
			vector2 = new Vector(1.0, 1.0);
		}
		vector2.Normalize();
		switch (frameworkElement.Name)
		{
		case "move":
			CircleCenterPoint += vector;
			clickPoint = position;
			break;
		case "minMiddlePoint":
			curMinRadius += vector * vector2;
			curMinRadius = Math.Abs(curMinRadius);
			if (curMinRadius > CircleRadius)
			{
				curMinRadius = CircleRadius;
			}
			MinRadius = curMinRadius;
			clickPoint = position;
			break;
		case "middlePoint":
			curRadius += vector * vector2;
			curRadius = Math.Abs(curRadius);
			if (curRadius < MinRadius)
			{
				curRadius = MinRadius;
			}
			CircleRadius = curRadius;
			clickPoint = position;
			break;
		case "middleStartPoint":
		{
			double num5 = Math.Atan2(clickPoint.Y - (ContainerPosition.Y + ContainerHeight / 2.0), clickPoint.X - (ContainerPosition.X + ContainerWidth / 2.0)) * 180.0 / Math.PI;
			double num6 = Math.Atan2(position.Y - (ContainerPosition.Y + ContainerHeight / 2.0), position.X - (ContainerPosition.X + ContainerWidth / 2.0)) * 180.0 / Math.PI;
			StartAngle += num6 - num5;
			clickPoint = position;
			break;
		}
		case "middleEndPoint":
		{
			double num12 = Math.Atan2(clickPoint.Y - (ContainerPosition.Y + ContainerHeight / 2.0), clickPoint.X - (ContainerPosition.X + ContainerWidth / 2.0)) * 180.0 / Math.PI;
			double num13 = Math.Atan2(position.Y - (ContainerPosition.Y + ContainerHeight / 2.0), position.X - (ContainerPosition.X + ContainerWidth / 2.0)) * 180.0 / Math.PI;
			EndAngle += num13 - num12;
			clickPoint = position;
			break;
		}
		case "arcPoint":
		{
			if (curRadius <= 0.0)
			{
				break;
			}
			Point point10 = position;
			Point point11 = MiddlePoint;
			Point point12 = StartPoint;
			Point point13 = EndPoint;
			Point containerPosition2 = ContainerPosition;
			Point point14 = StartPoint + new Vector(containerPosition2.X, containerPosition2.Y);
			Point point15 = EndPoint + new Vector(containerPosition2.X, containerPosition2.Y);
			Vector vector7 = new Vector(point15.X - point14.X, point15.Y - point14.Y);
			Vector vector8 = new Vector(point14.X - point10.X, point14.Y - point10.Y);
			Vector vector9 = new Vector(point15.X - point10.X, point15.Y - point10.Y);
			double num7 = 0.0;
			double num8 = 0.0;
			Vector vector10;
			if (vector7.Length == 0.0)
			{
				num7 = vector8.Length;
				if (num7 == 0.0)
				{
					break;
				}
				num8 = (num7 * num7 + Math.Pow(vector7.Length / 2.0, 2.0)) / (2.0 * num7);
				vector10 = -vector8;
				vector7 = new Vector(vector10.Y, 0.0 - vector10.X);
				if (Vector.CrossProduct(vector7, vector10) > 0.0)
				{
					vector7 = -vector7;
				}
			}
			else
			{
				num7 = Math.Abs(Vector.CrossProduct(vector8, vector9) / vector7.Length);
				if (num7 == 0.0)
				{
					break;
				}
				num8 = (num7 * num7 + Math.Pow(vector7.Length / 2.0, 2.0)) / (2.0 * num7);
				vector10 = new Vector(vector7.Y, 0.0 - vector7.X);
				if (Vector.CrossProduct(vector10, vector7) < 0.0)
				{
					vector10 = -vector10;
				}
			}
			Vector vector11 = vector8 + vector9;
			vector11.Normalize();
			if (!(Vector.CrossProduct(vector11, vector7) < 0.0))
			{
				vector10 = -vector10;
				Point point16 = point14;
				point14 = point15;
				point15 = point16;
			}
			vector10.Normalize();
			double num9 = 10000.0 * ControlToImageRadio;
			if (!(num8 >= num9) && !(num8 - (curRadius - curMinRadius) >= num9))
			{
				Vector vector12 = vector10 * num7;
				Point point17 = new Point((point15.X + point14.X) / 2.0, (point15.Y + point14.Y) / 2.0) + vector12;
				Point p = point17 + -vector10 * num8;
				double num10 = num8 / ControlToImageRadio;
				double num11 = num10 - (curRadius - curMinRadius);
				double minRadius2 = ((num11 > 0.0) ? num11 : 0.0);
				double startAngle2 = Math.Atan2(point14.Y - p.Y, point14.X - p.X) * 180.0 / Math.PI;
				double endAngle2 = Math.Atan2(point15.Y - p.Y, point15.X - p.X) * 180.0 / Math.PI;
				IsFresh = false;
				CircleCenterPoint = GetImagePoint(p);
				CircleRadius = num10;
				MinRadius = minRadius2;
				StartAngle = startAngle2;
				EndAngle = endAngle2;
				IsFresh = true;
				SetNewShape();
			}
			break;
		}
		case "endPoint":
		{
			Point point18 = new Point(MiddlePoint.X, MiddlePoint.Y);
			Point point19 = StartPoint;
			Point point20 = EndPoint;
			Point containerPosition3 = ContainerPosition;
			Point point21 = StartPoint + new Vector(containerPosition3.X, containerPosition3.Y);
			Point point22 = EndPoint + new Vector(containerPosition3.X, containerPosition3.Y);
			Point point23 = new Point((point19.X + point20.X) / 2.0, (point19.Y + point20.Y) / 2.0);
			double length2 = new Vector(point23.X - point18.X, point23.Y - point18.Y).Length;
			if (length2 == 0.0)
			{
				break;
			}
			Point point24 = position;
			Vector vector13 = new Vector(point24.X - point21.X, point24.Y - point21.Y);
			double num14 = (length2 * length2 + Math.Pow(vector13.Length / 2.0, 2.0)) / (2.0 * length2);
			double num15 = 10000.0 * ControlToImageRadio;
			if (num14 >= num15 || num14 - (curRadius - curMinRadius) >= num15)
			{
				break;
			}
			Point point25 = new Point((CircleCenterPoint.X + 0.0) * ControlToImageRadio, (CircleCenterPoint.Y + 0.0) * ControlToImageRadio);
			Vector vector15;
			if (vector13.Length == 0.0)
			{
				Vector vector14 = new Vector(point21.X - point25.X, point21.Y - point25.Y);
				vector15 = -vector14;
				vector13 = new Vector(vector15.Y, 0.0 - vector15.X);
				if (Vector.CrossProduct(vector13, vector15) > 0.0)
				{
					vector13 = -vector13;
				}
			}
			else
			{
				vector15 = new Vector(vector13.Y, 0.0 - vector13.X);
			}
			if (Vector.CrossProduct(vector15, vector13) < 0.0)
			{
				vector15 = -vector15;
			}
			vector15.Normalize();
			Vector vector16 = vector15 * length2;
			Point point26 = new Point((point24.X + point21.X) / 2.0, (point24.Y + point21.Y) / 2.0) + vector16;
			point25 = point26 + -vector15 * num14;
			double num16 = num14 / ControlToImageRadio;
			double num17 = num16 - (curRadius - curMinRadius);
			double minRadius3 = ((num17 > 0.0) ? num17 : 0.0);
			double startAngle3 = Math.Atan2(point21.Y - point25.Y, point21.X - point25.X) * 180.0 / Math.PI;
			double endAngle3 = Math.Atan2(point24.Y - point25.Y, point24.X - point25.X) * 180.0 / Math.PI;
			IsFresh = false;
			CircleCenterPoint = GetImagePoint(point25);
			CircleRadius = num16;
			MinRadius = minRadius3;
			StartAngle = startAngle3;
			EndAngle = endAngle3;
			IsFresh = true;
			SetNewShape();
			break;
		}
		case "startPoint":
		{
			Point point = new Point(MiddlePoint.X, MiddlePoint.Y);
			Point point2 = StartPoint;
			Point point3 = EndPoint;
			Point containerPosition = ContainerPosition;
			Point point4 = StartPoint + new Vector(containerPosition.X, containerPosition.Y);
			Point point5 = EndPoint + new Vector(containerPosition.X, containerPosition.Y);
			Point point6 = new Point((point2.X + point3.X) / 2.0, (point2.Y + point3.Y) / 2.0);
			double length = new Vector(point6.X - point.X, point6.Y - point.Y).Length;
			if (length == 0.0)
			{
				break;
			}
			Point point7 = position;
			Vector vector3 = new Vector(point7.X - point5.X, point7.Y - point5.Y);
			double num = (length * length + Math.Pow(vector3.Length / 2.0, 2.0)) / (2.0 * length);
			double num2 = 10000.0 * ControlToImageRadio;
			if (num >= num2 || num - (curRadius - curMinRadius) >= num2)
			{
				break;
			}
			Point point8 = new Point((CircleCenterPoint.X + 0.0) * ControlToImageRadio, (CircleCenterPoint.Y + 0.0) * ControlToImageRadio);
			Vector vector5;
			if (vector3.Length == 0.0)
			{
				Vector vector4 = new Vector(point4.X - point8.X, point4.Y - point8.Y);
				vector5 = -vector4;
				vector3 = new Vector(vector5.Y, 0.0 - vector5.X);
				if (Vector.CrossProduct(vector3, vector5) < 0.0)
				{
					vector3 = -vector3;
				}
			}
			else
			{
				vector5 = new Vector(vector3.Y, 0.0 - vector3.X);
			}
			if (Vector.CrossProduct(vector5, vector3) > 0.0)
			{
				vector5 = -vector5;
			}
			vector5.Normalize();
			Vector vector6 = vector5 * length;
			Point point9 = new Point((point7.X + point5.X) / 2.0, (point7.Y + point5.Y) / 2.0) + vector6;
			point8 = point9 + -vector5 * num;
			double num3 = num / ControlToImageRadio;
			double num4 = num3 - (curRadius - curMinRadius);
			double minRadius = ((num4 > 0.0) ? num4 : 0.0);
			double startAngle = Math.Atan2(point7.Y - point8.Y, point7.X - point8.X) * 180.0 / Math.PI;
			double endAngle = Math.Atan2(point5.Y - point8.Y, point5.X - point8.X) * 180.0 / Math.PI;
			IsFresh = false;
			CircleCenterPoint = GetImagePoint(point8);
			CircleRadius = num3;
			MinRadius = minRadius;
			StartAngle = startAngle;
			EndAngle = endAngle;
			IsFresh = true;
			SetNewShape();
			break;
		}
		}
	}

	private void SetNewShape()
	{
		ChangeContainerPosition();
		ChangeContainerSize();
		SetIsLargeArc();
		CalPoints();
	}

	private double GetY(Point a, Point b, double x)
	{
		Point point = new Point((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
		double num = (b.Y - a.Y) / (b.X - a.X);
		return (0.0 - 1.0 / num) * (x - (a.X + b.X) / 2.0) + (a.Y + b.Y) / 2.0;
	}

	public static double AngleBetween(Vector vector1, Vector vector2)
	{
		double y = vector1.X * vector2.Y - vector2.X * vector1.Y;
		double x = vector1.X * vector2.X + vector1.Y * vector2.Y;
		return Math.Atan2(y, x) * (180.0 / Math.PI);
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
				SetArc();
				SetArrow();
			}
		}
		else
		{
			ChangeContainerSize();
			ChangeContainerPosition();
			OnPropertyChanged("InnerCircleSize");
			Init();
		}
	}

	private void SetArc()
	{
		if (arcPoint != null)
		{
			arcPoint.Width = 6.0 * StrokeThickness;
			arcPoint.Height = 6.0 * StrokeThickness;
			Canvas.SetLeft(arcPoint, ContainerWidth);
			Canvas.SetTop(arcPoint, 0.5 * ContainerHeight - arcPoint.Height / 2.0);
			arcPoint.RenderTransform = new RotateTransform(AngleForUI, (0.0 - ContainerWidth) / 2.0, arcPoint.Height / 2.0);
		}
	}

	private void SetArrow()
	{
		if (startArrow != null && endArrow != null && arcArrow != null)
		{
			Point point = new Point(ContainerWidth / 2.0 + InnerCircleSize / 2.0 + 0.375 * ContainerWidth - 0.75 * InnerCircleSize / 2.0, ContainerHeight / 2.0);
			startArrow.Width = ArrowWidth;
			startArrow.Height = 2.0 * startArrow.Width;
			Canvas.SetLeft(startArrow, point.X);
			Canvas.SetTop(startArrow, point.Y - startArrow.Height / 2.0);
			startArrow.RenderTransform = new RotateTransform(StartAngle, ContainerWidth / 2.0 - point.X, startArrow.Height / 2.0);
			endArrow.Width = ArrowWidth;
			endArrow.Height = 2.0 * endArrow.Width;
			Canvas.SetLeft(endArrow, point.X);
			Canvas.SetTop(endArrow, point.Y - endArrow.Height / 2.0);
			endArrow.RenderTransform = new RotateTransform(EndAngle, ContainerWidth / 2.0 - point.X, endArrow.Height / 2.0);
			arcArrow.Width = 2.0 * ArrowWidth;
			arcArrow.Height = 0.5 * arcArrow.Width;
			Canvas.SetLeft(arcArrow, ContainerWidth - arcArrow.Width / 2.0);
			Canvas.SetTop(arcArrow, ContainerHeight * 0.5);
			double angle = AngleForUI + (AngleForUI - StartAngle) / 2.0;
			arcArrow.RenderTransform = new RotateTransform(angle, arcArrow.Width / 2.0 - ContainerWidth / 2.0, 0.0);
		}
	}

	private void ChangeContainerSize()
	{
		if (IsFresh)
		{
			ContainerWidth = CircleRadius * ControlToImageRadio * 2.0;
			ContainerHeight = ContainerWidth;
		}
	}

	private void ChangeContainerPosition()
	{
		if (IsFresh)
		{
			double x = (CircleCenterPoint.X - CircleRadius + 0.0) * ControlToImageRadio;
			double y = (CircleCenterPoint.Y - CircleRadius + 0.0) * ControlToImageRadio;
			ContainerPosition = new Point(x, y);
		}
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
