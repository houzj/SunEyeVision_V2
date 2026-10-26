using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Apps.UIHelper.Extern;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IOverSectorROI))]
public class OverSectorROI : ROIControlBase
{
	private static SectorROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty CircleRadiusProperty;

	public static readonly DependencyProperty MinRadiusProperty;

	public static readonly DependencyProperty StartAngleProperty;

	public static readonly DependencyProperty AngleRangeProperty;

	public static readonly DependencyProperty MiddleEndAngleProperty;

	public static readonly DependencyProperty MiddleStartAngleProperty;

	public static readonly DependencyProperty MinMiddleAngleProperty;

	public static readonly DependencyProperty CircleCenterPointProperty;

	private Grid move;

	private Area middleEndPoint;

	private Area minMiddlePoint;

	private Area middlePoint;

	private Area middleStartPoint;

	private Path arcPoint;

	private Path arcArrow;

	private Path startArrow;

	private Path endArrow;

	private Ellipse startPoint;

	private Ellipse endPoint;

	private Point clickPoint;

	private ImageViewItemsPage parent;

	private PathFigure pFigure;

	private LineSegment lSegment1;

	private ArcSegment aSegment1;

	private ArcSegment aSegment10;

	private ArcSegment aSegment11;

	private ArcSegment aSegment12;

	private LineSegment lSegment2;

	private ArcSegment aSegment2;

	private ArcSegment aSegment20;

	private ArcSegment aSegment21;

	private ArcSegment aSegment22;

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

	public double AngleRange
	{
		get
		{
			return (double)GetValue(AngleRangeProperty);
		}
		set
		{
			SetValue(AngleRangeProperty, value);
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

	static OverSectorROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(OverSectorROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(OverSectorROI), new PropertyMetadata(0.0));
		CircleRadiusProperty = DependencyProperty.Register("CircleRadius", typeof(double), typeof(OverSectorROI), new PropertyMetadata(double.NaN, OnCircleRadiusChanged));
		MinRadiusProperty = DependencyProperty.Register("MinRadius", typeof(double), typeof(OverSectorROI), new PropertyMetadata(0.0, OnMinRadiusChanged));
		StartAngleProperty = DependencyProperty.Register("StartAngle", typeof(double), typeof(OverSectorROI), new PropertyMetadata(0.0, OnStartAngleChanged));
		AngleRangeProperty = DependencyProperty.Register("AngleRange", typeof(double), typeof(OverSectorROI), new PropertyMetadata(double.NaN, OnAngleRangeChanged));
		MiddleEndAngleProperty = DependencyProperty.Register("MiddleEndAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		MiddleStartAngleProperty = DependencyProperty.Register("MiddleStartAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		MinMiddleAngleProperty = DependencyProperty.Register("MinMiddleAngle", typeof(double), typeof(SectorROI), new PropertyMetadata(0.0));
		CircleCenterPointProperty = DependencyProperty.Register("CircleCenterPoint", typeof(Point), typeof(OverSectorROI), new PropertyMetadata(new Point(double.NaN, double.NaN), OnCircleCenterPointChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(OverSectorROI), new FrameworkPropertyMetadata(typeof(OverSectorROI)));
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
			Vector vector = new Vector(1.0, 0.0);
			Vector vector2 = new Vector(position.X - ROIControlBase.mouseOffset.X, position.Y - ROIControlBase.mouseOffset.Y);
			ROISubstitute.StartAngle = Vector.AngleBetween(vector, new Vector(vector2.X, vector2.Y)) - 45.0;
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
			double startAngle = ROISubstitute.StartAngle;
			double controlToImageRadio = ROISubstitute.ControlToImageRadio;
			IOverSectorROI overSectorROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as IOverSectorROI;
			int num = 1;
			int num2 = 1;
			if (overSectorROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			overSectorROI.CenterPoint = new Point((centerPoint.X / controlToImageRadio - 0.0) / (double)num, (centerPoint.Y / controlToImageRadio - 0.0) / (double)num2);
			overSectorROI.Radius = radius / controlToImageRadio / (double)num;
			overSectorROI.StartAngle = startAngle;
			overSectorROI.AngleRange = 90.0;
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
			ROIControlBase.CompleteDraw(overSectorROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static void OnCircleRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		OverSectorROI overSectorROI = d as OverSectorROI;
		overSectorROI.ChangeContainerSize();
		overSectorROI.ChangeContainerPosition();
		overSectorROI.CalPoints();
	}

	private Point GetStartPoint(bool needCheck, double angleOffset = 0.0)
	{
		if (needCheck && (double.IsNaN(CircleCenterPoint.X) || double.IsNaN(CircleCenterPoint.Y) || double.IsNaN(StartAngle) || double.IsNaN(CircleRadius)))
		{
			return new Point(double.NaN, double.NaN);
		}
		RotateTransform rotateTransform = new RotateTransform();
		rotateTransform.Angle = StartAngle + angleOffset;
		rotateTransform.CenterX = CircleRadius;
		rotateTransform.CenterY = CircleRadius;
		Point point = new Point(CircleRadius * 2.0, CircleRadius);
		Point point2 = rotateTransform.Transform(point);
		return new Point(point2.X * ControlToImageRadio, point2.Y * ControlToImageRadio);
	}

	private Point GetEndPoint(bool needCheck)
	{
		if (needCheck && double.IsNaN(AngleRange))
		{
			return new Point(double.NaN, double.NaN);
		}
		return GetStartPoint(needCheck, AngleRange);
	}

	private Point GetMinEndPoint(bool needCheck, Point endPoint)
	{
		if (needCheck && (double.IsNaN(endPoint.X) || double.IsNaN(endPoint.Y) || double.IsNaN(CircleRadius) || double.IsNaN(MinRadius)))
		{
			return new Point(double.NaN, double.NaN);
		}
		Point point = new Point(endPoint.X / ControlToImageRadio, endPoint.Y / ControlToImageRadio);
		Point point2 = new Point(CircleRadius, CircleRadius);
		Vector vector = point - point2;
		Vector vector2 = vector * MinRadius / CircleRadius;
		Point point3 = point2 + vector2;
		return new Point(point3.X * ControlToImageRadio, point3.Y * ControlToImageRadio);
	}

	private Size GetMinSize()
	{
		if (double.IsNaN(MinRadius))
		{
			return new Size(double.NaN, double.NaN);
		}
		return new Size(MinRadius * ControlToImageRadio, MinRadius * ControlToImageRadio);
	}

	private Size GetMaxSize()
	{
		if (double.IsNaN(CircleRadius))
		{
			return new Size(double.NaN, double.NaN);
		}
		return new Size(CircleRadius * ControlToImageRadio, CircleRadius * ControlToImageRadio);
	}

	private Point GetMinStartPoint(bool needCheck, Point startPoint)
	{
		return GetMinEndPoint(needCheck, startPoint);
	}

	private void ChangeMinEndPoint()
	{
		if (lSegment1 != null)
		{
			lSegment1.Point = GetMinEndPoint(needCheck: false, pFigure.StartPoint);
		}
	}

	private void ChangeMiddleEndPoint()
	{
		if (middleEndPoint != null)
		{
			middleEndPoint.SetValue(Canvas.LeftProperty, (lSegment1?.Point.X + pFigure?.StartPoint.X) / 2.0);
			middleEndPoint.SetValue(Canvas.TopProperty, (lSegment1?.Point.Y + pFigure?.StartPoint.Y) / 2.0);
		}
	}

	private void ChangeMiddleStartPoint()
	{
		if (middleStartPoint != null)
		{
			middleStartPoint.SetValue(Canvas.LeftProperty, (lSegment2?.Point.X + aSegment1?.Point.X) / 2.0);
			middleStartPoint.SetValue(Canvas.TopProperty, (lSegment2?.Point.Y + aSegment1?.Point.Y) / 2.0);
		}
	}

	private void ChangeMiddlePoint()
	{
		if (middlePoint != null)
		{
			RotateTransform rotateTransform = new RotateTransform();
			Point point = new Point(ContainerWidth / 2.0, ContainerHeight / 2.0);
			rotateTransform.CenterX = point.X;
			rotateTransform.CenterY = point.Y;
			rotateTransform.Angle = AngleRange / 2.0;
			Point point2 = rotateTransform.Transform(lSegment2.Point);
			middlePoint.SetValue(Canvas.LeftProperty, point2.X);
			middlePoint.SetValue(Canvas.TopProperty, point2.Y);
			ChangeRadiusCursor();
		}
	}

	private void ChangMinMiddlePoint()
	{
		if (minMiddlePoint != null)
		{
			RotateTransform rotateTransform = new RotateTransform();
			Point point = new Point(ContainerWidth / 2.0, ContainerHeight / 2.0);
			rotateTransform.CenterX = point.X;
			rotateTransform.CenterY = point.Y;
			rotateTransform.Angle = AngleRange / 2.0;
			Point point2 = rotateTransform.Inverse.Transform(lSegment1.Point);
			minMiddlePoint.SetValue(Canvas.LeftProperty, point2.X);
			minMiddlePoint.SetValue(Canvas.TopProperty, point2.Y);
		}
	}

	private void ChangArcPoint()
	{
		if (arcPoint != null)
		{
			arcPoint.Width = 6.0 * StrokeThickness;
			arcPoint.Height = 6.0 * StrokeThickness;
			Canvas.SetLeft(arcPoint, ContainerWidth);
			Canvas.SetTop(arcPoint, 0.5 * ContainerHeight - arcPoint.Height / 2.0);
			arcPoint.RenderTransform = new RotateTransform((2.0 * StartAngle + AngleRange) / 2.0, (0.0 - ContainerWidth) / 2.0, arcPoint.Height / 2.0);
		}
	}

	private void ChangeStartPoint()
	{
		if (startPoint != null)
		{
			Canvas.SetLeft(startPoint, lSegment2.Point.X);
			Canvas.SetTop(startPoint, lSegment2.Point.Y);
		}
	}

	private void ChangeEndPoint()
	{
		if (endPoint != null)
		{
			Canvas.SetLeft(endPoint, pFigure.StartPoint.X);
			Canvas.SetTop(endPoint, pFigure.StartPoint.Y);
		}
	}

	private void CalPoints()
	{
		if (double.IsNaN(CircleCenterPoint.X) || double.IsNaN(CircleCenterPoint.Y) || double.IsNaN(StartAngle) || double.IsNaN(AngleRange) || double.IsNaN(CircleRadius) || double.IsNaN(MinRadius))
		{
			return;
		}
		if (pFigure != null)
		{
			pFigure.StartPoint = GetEndPoint(needCheck: false);
			ChangeMinEndPoint();
		}
		Size minSize = GetMinSize();
		Point point = GetStartPoint(needCheck: false);
		Point minStartPoint = GetMinStartPoint(needCheck: false, point);
		Point point2 = new Point(ContainerWidth / 2.0, ContainerHeight / 2.0);
		if (aSegment1 != null)
		{
			aSegment1.Size = minSize;
			aSegment1.Point = minStartPoint;
		}
		if (aSegment10 != null)
		{
			aSegment10.Size = minSize;
			if (AngleRange < 180.0)
			{
				aSegment10.Point = lSegment1.Point;
			}
			else
			{
				Vector vector = minStartPoint - point2;
				aSegment10.Point = -vector + point2;
			}
		}
		if (aSegment11 != null)
		{
			aSegment11.Size = minSize;
			if (AngleRange < 360.0)
			{
				aSegment11.Point = lSegment1.Point;
			}
			else
			{
				aSegment11.Point = minStartPoint;
			}
		}
		if (aSegment12 != null)
		{
			aSegment12.Size = minSize;
			if (AngleRange < 540.0)
			{
				aSegment12.Point = lSegment1.Point;
			}
			else
			{
				Vector vector2 = minStartPoint - point2;
				aSegment12.Point = -vector2 + point2;
			}
		}
		if (lSegment2 != null)
		{
			lSegment2.Point = point;
		}
		Size maxSize = GetMaxSize();
		if (aSegment2 != null)
		{
			aSegment2.Size = maxSize;
			if (AngleRange < 180.0)
			{
				aSegment2.Point = (pFigure?.StartPoint).Value;
			}
			else
			{
				Vector vector3 = point - point2;
				aSegment2.Point = -vector3 + point2;
			}
		}
		if (aSegment20 != null)
		{
			aSegment20.Size = maxSize;
			if (AngleRange < 360.0)
			{
				aSegment20.Point = (pFigure?.StartPoint).Value;
			}
			else
			{
				aSegment20.Point = point;
			}
		}
		if (aSegment21 != null)
		{
			aSegment21.Size = maxSize;
			if (AngleRange < 540.0)
			{
				aSegment21.Point = (pFigure?.StartPoint).Value;
			}
			else
			{
				Vector vector4 = point - point2;
				aSegment21.Point = -vector4 + point2;
			}
		}
		if (aSegment22 != null)
		{
			aSegment22.Size = maxSize;
			aSegment22.Point = (pFigure?.StartPoint).Value;
		}
		ChangeMiddleStartPoint();
		ChangeMiddleEndPoint();
		ChangeMiddlePoint();
		ChangMinMiddlePoint();
		ChangArcPoint();
		ChangeStartPoint();
		ChangeEndPoint();
		SetArrow();
	}

	private static void OnMinRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		OverSectorROI overSectorROI = d as OverSectorROI;
		overSectorROI.CalPoints();
	}

	private static void OnStartAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		OverSectorROI overSectorROI = d as OverSectorROI;
		overSectorROI.CalPoints();
		overSectorROI.ChangeStartAngleCursor();
		overSectorROI.ChangeEndAngleCursor();
	}

	private static void OnAngleRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		OverSectorROI overSectorROI = d as OverSectorROI;
		overSectorROI.CalPoints();
		overSectorROI.ChangeEndAngleCursor();
	}

	private void ChangeEndAngleCursor()
	{
		if (middleEndPoint != null && pFigure != null && !double.IsNaN(ContainerWidth) && !double.IsNaN(ContainerHeight))
		{
			Vector vector = new Vector(pFigure.StartPoint.X - ContainerWidth / 2.0, pFigure.StartPoint.Y - ContainerHeight / 2.0);
			Vector vector2 = new Vector(1.0, 0.0);
			MiddleEndAngle = Vector.AngleBetween(vector2, vector);
			middleEndPoint.RenderTransformOrigin = new Point(0.5, 0.5);
			middleEndPoint.RenderTransform = new RotateTransform(MiddleEndAngle);
			ChangeRadiusCursor();
		}
	}

	private void ChangeStartAngleCursor()
	{
		if (middleStartPoint != null && lSegment2 != null && !double.IsNaN(ContainerWidth) && !double.IsNaN(ContainerHeight))
		{
			Vector vector = new Vector(lSegment2.Point.X - ContainerWidth / 2.0, lSegment2.Point.Y - ContainerHeight / 2.0);
			Vector vector2 = new Vector(1.0, 0.0);
			MiddleStartAngle = Vector.AngleBetween(vector2, vector);
			middleStartPoint.RenderTransformOrigin = new Point(0.5, 0.5);
			middleStartPoint.RenderTransform = new RotateTransform(MiddleStartAngle);
			ChangeRadiusCursor();
		}
	}

	private void ChangeRadiusCursor()
	{
		if (middlePoint != null && !double.IsNaN(ContainerWidth) && !double.IsNaN(ContainerHeight) && ContainerWidth != 0.0 && ContainerHeight != 0.0)
		{
			Point point = new Point(Canvas.GetLeft(middlePoint), Canvas.GetTop(middlePoint));
			Vector vector = new Vector(point.X - ContainerWidth / 2.0, point.Y - ContainerHeight / 2.0);
			Vector vector2 = new Vector(1.0, 0.0);
			MinMiddleAngle = Vector.AngleBetween(vector2, vector);
			minMiddlePoint.RenderTransformOrigin = new Point(0.5, 0.5);
			minMiddlePoint.RenderTransform = new RotateTransform(MinMiddleAngle);
			middlePoint.RenderTransformOrigin = new Point(0.5, 0.5);
			middlePoint.RenderTransform = new RotateTransform(MinMiddleAngle);
		}
	}

	private static void OnCircleCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		OverSectorROI overSectorROI = d as OverSectorROI;
		overSectorROI.ChangeContainerPosition();
		overSectorROI.CalPoints();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		pFigure = Template.FindName("pFigure", this) as PathFigure;
		lSegment1 = Template.FindName("lSegment1", this) as LineSegment;
		aSegment1 = Template.FindName("aSegment1", this) as ArcSegment;
		aSegment10 = Template.FindName("aSegment10", this) as ArcSegment;
		aSegment11 = Template.FindName("aSegment11", this) as ArcSegment;
		aSegment12 = Template.FindName("aSegment12", this) as ArcSegment;
		lSegment2 = Template.FindName("lSegment2", this) as LineSegment;
		aSegment2 = Template.FindName("aSegment2", this) as ArcSegment;
		aSegment20 = Template.FindName("aSegment20", this) as ArcSegment;
		aSegment21 = Template.FindName("aSegment21", this) as ArcSegment;
		aSegment22 = Template.FindName("aSegment22", this) as ArcSegment;
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
		Point point = new Point(ContainerPosition.X + ContainerWidth / 2.0, ContainerPosition.Y + ContainerHeight / 2.0);
		Point point2 = new Point(ContainerWidth / 2.0, ContainerHeight / 2.0);
		switch (frameworkElement.Name)
		{
		case "move":
			CircleCenterPoint += vector;
			clickPoint = position;
			break;
		case "minMiddlePoint":
		{
			Vector vector14 = clickPoint - point;
			vector14.Normalize();
			double num16 = vector * vector14;
			double minRadius = Math.Abs(MinRadius + num16);
			MinRadius = minRadius;
			clickPoint = position;
			break;
		}
		case "middlePoint":
		{
			Vector vector17 = clickPoint - point;
			vector17.Normalize();
			double num19 = vector * vector17;
			double circleRadius = Math.Abs(CircleRadius + num19);
			CircleRadius = circleRadius;
			clickPoint = position;
			break;
		}
		case "middleStartPoint":
		{
			Vector vector15 = clickPoint - point;
			Vector vector16 = position - point;
			double num17 = Vector.AngleBetween(vector15, vector16);
			double num18 = AngleRange - num17;
			if (num18 > 720.0)
			{
				AngleRange = 720.0;
				StartAngle += num17;
			}
			else if (num18 < 0.0)
			{
				AngleRange = 0.0;
				StartAngle += num17;
			}
			else
			{
				AngleRange -= num17;
				StartAngle += num17;
			}
			clickPoint = position;
			break;
		}
		case "middleEndPoint":
		{
			Vector vector18 = clickPoint - point;
			Vector vector19 = position - point;
			double num20 = Vector.AngleBetween(vector18, vector19);
			double num21 = AngleRange + num20;
			if (num21 > 720.0)
			{
				AngleRange = 720.0;
				StartAngle += num20;
			}
			else if (num21 < 0.0)
			{
				AngleRange = 0.0;
				StartAngle += num20;
			}
			else
			{
				AngleRange += num20;
			}
			clickPoint = position;
			break;
		}
		case "arcPoint":
		{
			Point point9 = pFigure.StartPoint;
			Point point10 = lSegment2.Point;
			Point point11 = new Point((point9.X + point10.X) / 2.0, (point9.Y + point10.Y) / 2.0);
			Point point12 = new Point(Canvas.GetLeft(middlePoint), Canvas.GetTop(middlePoint));
			Vector vector8 = point12 - point2;
			vector8.Normalize();
			double num9 = (position - clickPoint) * vector8;
			double length2 = (point12 - point11).Length;
			double num10 = length2 + num9;
			double num11 = num10;
			if (num10 == 0.0)
			{
				return;
			}
			if (num10 < 0.0)
			{
				num11 = 0.0 - num10;
				point12 = -(point12 - point11) + point11;
				point9 = lSegment2.Point;
				point10 = pFigure.StartPoint;
				if (StartAngle > 180.0)
				{
					StartAngle -= 180.0;
				}
				else
				{
					StartAngle += 180.0;
				}
				Vector vector9 = (-(point2 - point11) + point11 - point2) / ControlToImageRadio;
				CircleCenterPoint += vector9;
				point2 = -(point2 - point11) + point11;
			}
			double num12 = (point9 - point10).Length / 2.0;
			double num13 = (num11 * num11 + num12 * num12) / (2.0 * num11);
			Vector vector10 = point2 - point12;
			vector10.Normalize();
			Vector vector11 = (0.0 - num11 + num13 + length2 - CircleRadius * ControlToImageRadio) * vector10;
			Point circleCenterPoint2 = CircleCenterPoint;
			double num14 = CircleRadius - MinRadius;
			CircleRadius = num13 / ControlToImageRadio;
			CircleCenterPoint = circleCenterPoint2 + vector11 / ControlToImageRadio;
			double val2 = CircleRadius - num14;
			MinRadius = Math.Max(val2, 0.0);
			Vector vector12 = point2 - point10;
			Vector vector13 = vector12 + vector11;
			double num15 = Vector.AngleBetween(vector12, vector13);
			StartAngle += num15;
			AngleRange -= 2.0 * num15;
			clickPoint = position;
			break;
		}
		case "endPoint":
		case "startPoint":
		{
			Point point3 = pFigure.StartPoint;
			Point point4 = lSegment2.Point;
			bool flag = frameworkElement.Name == "endPoint";
			if (flag)
			{
				point4 = pFigure.StartPoint;
				point3 = lSegment2.Point;
			}
			Point point5 = new Point((point4.X + point3.X) / 2.0, (point4.Y + point3.Y) / 2.0);
			Point point6 = new Point(Canvas.GetLeft(middlePoint), Canvas.GetTop(middlePoint));
			double length = (point6 - point5).Length;
			Point point7 = point4 + (position - clickPoint);
			double num = (point3 - point7).Length / 2.0;
			double num2 = (length * length + num * num) / (2.0 * length);
			RotateTransform rotateTransform = new RotateTransform();
			rotateTransform.Angle = -90.0;
			Vector vector2 = (point7 - point3) * rotateTransform.Value * ((!flag) ? 1 : (-1));
			vector2.Normalize();
			Point point8 = new Point((point3.X + point7.X) / 2.0, (point3.Y + point7.Y) / 2.0);
			Vector vector3 = point8 + -vector2 * length + vector2 * num2 - point2;
			Point circleCenterPoint = CircleCenterPoint;
			double num3 = CircleRadius - MinRadius;
			CircleRadius = num2 / ControlToImageRadio;
			CircleCenterPoint = circleCenterPoint + vector3 / ControlToImageRadio;
			double val = CircleRadius - num3;
			MinRadius = Math.Max(val, 0.0);
			double num4 = StartAngle + AngleRange;
			Vector vector4 = point2 - point4;
			Vector vector5 = point2 + vector3 - point7;
			double num5 = Vector.AngleBetween(vector4, vector5);
			Vector vector6 = point2 - point3;
			Vector vector7 = point2 + vector3 - point3;
			double num6 = Vector.AngleBetween(vector6, vector7);
			if (flag)
			{
				double num7 = num5;
				num5 = num6;
				num6 = num7;
			}
			StartAngle += num5;
			double num8 = num4 + num6;
			AngleRange = num8 - StartAngle;
			clickPoint = position;
			break;
		}
		}
		if (StartAngle > 180.0)
		{
			StartAngle = -360.0 + StartAngle;
		}
		else if (StartAngle < -180.0)
		{
			StartAngle = 360.0 + StartAngle;
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
		case "ControlToImageRadio":
			ChangeContainerSize();
			ChangeContainerPosition();
			Init();
			break;
		case "StrokeThickness":
			ChangArcPoint();
			SetArrow();
			break;
		case "ContainerWidth":
		case "ContainerHeight":
			ChangeEndAngleCursor();
			ChangeStartAngleCursor();
			ChangeRadiusCursor();
			break;
		}
	}

	private void SetArrow()
	{
		if (startArrow != null && endArrow != null && arcArrow != null)
		{
			Point point = new Point((0.75 * (CircleRadius - MinRadius) + MinRadius + CircleRadius) * ControlToImageRadio, ContainerHeight / 2.0);
			startArrow.Width = ArrowWidth;
			startArrow.Height = 2.0 * startArrow.Width;
			Canvas.SetLeft(startArrow, point.X);
			Canvas.SetTop(startArrow, point.Y - startArrow.Height / 2.0);
			startArrow.RenderTransform = new RotateTransform(StartAngle, ContainerWidth / 2.0 - point.X, startArrow.Height / 2.0);
			endArrow.Width = ArrowWidth;
			endArrow.Height = 2.0 * endArrow.Width;
			Canvas.SetLeft(endArrow, point.X);
			Canvas.SetTop(endArrow, point.Y - endArrow.Height / 2.0);
			endArrow.RenderTransform = new RotateTransform(StartAngle + AngleRange, ContainerWidth / 2.0 - point.X, endArrow.Height / 2.0);
			arcArrow.Width = 2.0 * ArrowWidth;
			arcArrow.Height = 0.5 * arcArrow.Width;
			Canvas.SetLeft(arcArrow, ContainerWidth - arcArrow.Width / 2.0);
			Canvas.SetTop(arcArrow, ContainerHeight * 0.5);
			double angle = StartAngle + AngleRange * 3.0 / 4.0;
			arcArrow.RenderTransform = new RotateTransform(angle, arcArrow.Width / 2.0 - ContainerWidth / 2.0, 0.0);
		}
	}

	private void ChangeContainerSize()
	{
		ContainerWidth = CircleRadius * ControlToImageRadio * 2.0;
		ContainerHeight = ContainerWidth;
	}

	private void ChangeContainerPosition()
	{
		if (!double.IsNaN(CircleCenterPoint.X) && !double.IsNaN(CircleCenterPoint.Y) && !double.IsNaN(CircleRadius))
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
