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

[ROIDisplayForInterfaceType(typeof(ILineCaliperROI))]
public class LineCaliperROI : ROIControlBase
{
	public const double MinCaliperWidth = 1.0;

	public const double MinCaliperHeight = 3.0;

	public const double DefaultCaliperWidth = 5.0;

	private static LineCaliperROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty StartPointProperty;

	public static readonly DependencyProperty EndPointProperty;

	public static readonly DependencyProperty CaliperHeightProperty;

	public static readonly DependencyProperty CaliperWidthProperty;

	public static readonly DependencyProperty CaliperNumProperty;

	public static readonly DependencyProperty CaliperRotateAngleProperty;

	private Canvas move;

	private Arrow line;

	private Area left;

	private Area top;

	private Area right;

	private Area bottom;

	private Area caliperRect;

	private Ellipse startPoint;

	private Ellipse endPoint;

	private DrawingCanvas drawCanvas;

	private Point clickPoint;

	private Point curStartPoint;

	private Point curEndPoint;

	private RotateTransform caliperRotate = new RotateTransform();

	private ImageViewItemsPage parent;

	private Brush brush = Brushes.Transparent;

	private Pen pen = new Pen();

	private Pen pen2 = new Pen
	{
		Thickness = 0.0
	};

	private double CaliperMaxWidth = 0.0;

	private Point RectCenterPoint;

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

	public double RectWidth => CaliperWidth * ControlToImageRadio;

	public double RectHeight => CaliperHeight * ControlToImageRadio;

	public Point RectTopLeft => new Point(RectCenterPoint.X - RectWidth / 2.0, RectCenterPoint.Y - RectHeight / 2.0);

	public Point Start => GetControlPoint(StartPoint);

	public Point End => GetControlPoint(EndPoint);

	static LineCaliperROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(LineCaliperROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(LineCaliperROI), new PropertyMetadata(0.0));
		StartPointProperty = DependencyProperty.Register("StartPoint", typeof(Point), typeof(LineCaliperROI), new PropertyMetadata(new Point(double.NaN, double.NaN), OnStartPointChanged));
		EndPointProperty = DependencyProperty.Register("EndPoint", typeof(Point), typeof(LineCaliperROI), new PropertyMetadata(new Point(double.NaN, double.NaN), OnEndPointChanged));
		CaliperHeightProperty = DependencyProperty.Register("CaliperHeight", typeof(double), typeof(LineCaliperROI), new PropertyMetadata(double.NaN, OnCaliperHeightChanged));
		CaliperWidthProperty = DependencyProperty.Register("CaliperWidth", typeof(double), typeof(LineCaliperROI), new PropertyMetadata(double.NaN, OnCaliperWidthChanged));
		CaliperNumProperty = DependencyProperty.Register("CaliperNum", typeof(uint), typeof(LineCaliperROI), new PropertyMetadata(OnCaliperNumChanged));
		CaliperRotateAngleProperty = DependencyProperty.Register("CaliperRotateAngle", typeof(double), typeof(LineCaliperROI));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(LineCaliperROI), new FrameworkPropertyMetadata(typeof(LineCaliperROI)));
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new LineCaliperROISubstitute();
		ROISubstitute.StartPoint = ROIControlBase.mouseOffset;
		ROISubstitute.EndPoint = ROIControlBase.mouseOffset;
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
			ROISubstitute.EndPoint = position;
		}
	}

	private static void Parent_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (ROISubstitute == null)
		{
			return;
		}
		ROIItemsPage rOIItemsPage = sender as ROIItemsPage;
		if (!ROIControlBase.IsMoved && ROISubstitute.StartPoint != ROISubstitute.EndPoint)
		{
			ROIControlBase.IsMoved = true;
		}
		if (ROIControlBase.IsMoved)
		{
			Point point = ROISubstitute.StartPoint;
			Point point2 = ROISubstitute.EndPoint;
			double controlToImageRadio = ROISubstitute.ControlToImageRadio;
			ILineCaliperROI lineCaliperROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ILineCaliperROI;
			int num = 1;
			int num2 = 1;
			if (lineCaliperROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			lineCaliperROI.StartPoint = new Point((point.X / controlToImageRadio - 0.0) / (double)num, (point.Y / controlToImageRadio - 0.0) / (double)num2);
			lineCaliperROI.EndPoint = new Point((point2.X / controlToImageRadio - 0.0) / (double)num, (point2.Y / controlToImageRadio - 0.0) / (double)num2);
			lineCaliperROI.CaliperNum = 6u;
			lineCaliperROI.CaliperWidth = 5.0 / (double)num;
			lineCaliperROI.CaliperHeight = 2.0 * (GetDistance(point.X, point.Y, point2.X, point2.Y) / ((double)((lineCaliperROI.CaliperNum + 1) * 2) * controlToImageRadio)) / (double)num2;
			lineCaliperROI.CaliperRotateAngle = GetRotate(point, point2);
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseDown -= Parent_MouseDown;
			ROIControlBase.CompleteDraw(lineCaliperROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static double GetRotate(Point StartPoint, Point EndPoint)
	{
		Vector vector = new Vector(EndPoint.X - StartPoint.X, EndPoint.Y - StartPoint.Y);
		Vector vector2 = new Vector(0.0, -10.0);
		double num = Math.Abs(Vector.AngleBetween(vector, vector2));
		if (EndPoint.X >= StartPoint.X && EndPoint.Y >= StartPoint.Y)
		{
			return num - 90.0;
		}
		if (EndPoint.X >= StartPoint.X && EndPoint.Y < StartPoint.Y)
		{
			return num + 270.0;
		}
		return 270.0 - num;
	}

	private static void OnStartPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LineCaliperROI).OnPropertyChanged("Start");
		(d as LineCaliperROI).DrawCaliper();
	}

	private static void OnEndPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LineCaliperROI).OnPropertyChanged("End");
		(d as LineCaliperROI).DrawCaliper();
	}

	private static void OnCaliperHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LineCaliperROI).DrawCaliper();
	}

	private static void OnCaliperWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LineCaliperROI).DrawCaliper();
	}

	private static void OnCaliperNumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LineCaliperROI).DrawCaliper();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Canvas;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		line = Template.FindName("line", this) as Arrow;
		caliperRect = Template.FindName("caliperRect", this) as Area;
		caliperRect.RenderTransform = caliperRotate;
		caliperRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		startPoint = Template.FindName("startPoint", this) as Ellipse;
		startPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		endPoint = Template.FindName("endPoint", this) as Ellipse;
		endPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
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
		DrawCaliper();
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		curStartPoint = StartPoint;
		curEndPoint = EndPoint;
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
			switch (frameworkElement.Name)
			{
			case "move":
			{
				double num = (position.Y - clickPoint.Y) / ControlToImageRadio;
				double num2 = (position.X - clickPoint.X) / ControlToImageRadio;
				double x = curStartPoint.X + num2;
				double y = curStartPoint.Y + num;
				double x2 = curEndPoint.X + num2;
				double y2 = curEndPoint.Y + num;
				StartPoint = new Point(x, y);
				EndPoint = new Point(x2, y2);
				break;
			}
			case "startPoint":
				StartPoint = GetImagePoint(position);
				break;
			case "endPoint":
				EndPoint = GetImagePoint(position);
				break;
			case "top":
			case "bottom":
				SetCaliperSize(position, width: false, height: true);
				break;
			case "left":
			case "right":
				SetCaliperSize(position, width: true, height: false);
				break;
			case "caliperRect":
				SetCaliperSize(position, width: true, height: true);
				break;
			}
		}
	}

	private void SetCaliperSize(Point curPoint, bool width, bool height)
	{
		Vector vector = new Vector(End.X - Start.X, End.Y - Start.Y);
		double value = Vector.AngleBetween(vector2: new Vector(curPoint.X - RectCenterPoint.X, curPoint.Y - RectCenterPoint.Y), vector1: vector);
		double num = Math.Abs(value) * Math.PI / 180.0;
		double distance = GetDistance(curPoint.X, curPoint.Y, RectCenterPoint.X, RectCenterPoint.Y);
		if (width)
		{
			double num2 = 2.0 * distance * Math.Abs(Math.Cos(num)) / ControlToImageRadio;
			if (num2 < 1.0)
			{
				num2 = 1.0;
			}
			CaliperWidth = num2;
		}
		if (height)
		{
			double num3 = 2.0 * distance * Math.Abs(Math.Sin(num)) / ControlToImageRadio;
			if (num3 < 3.0)
			{
				num3 = 3.0;
			}
			CaliperHeight = num3;
		}
	}

	private static double GetDistance(double A_x, double A_y, double B_x, double B_y)
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
				ChangeArrow();
			}
		}
		else
		{
			OnPropertyChanged("Start");
			OnPropertyChanged("End");
			DrawCaliper();
		}
	}

	private void ChangeArrow()
	{
		if (line != null)
		{
			line.HeadHeight = 2.0 * HotStrokeThickness;
			line.HeadWidth = 3.0 * line.HeadHeight;
		}
	}

	private void DrawCaliper()
	{
		if (double.IsNaN(StartPoint.X) || double.IsNaN(EndPoint.X) || double.IsNaN(CaliperWidth) || double.IsNaN(CaliperHeight) || drawCanvas == null)
		{
			return;
		}
		pen.Thickness = StrokeThickness;
		if (pen.Brush == null)
		{
			pen.Brush = (Brush)GetResource("ROIBaseColor");
		}
		Point start = Start;
		Point end = End;
		double num = end.Y - start.Y;
		double num2 = end.X - start.X;
		double num3 = num2 / (double)(CaliperNum + 1);
		double num4 = num / (double)(CaliperNum + 1);
		double num5 = 2.0 * Math.Sqrt(num3 * num3 + num4 * num4) / ControlToImageRadio;
		CaliperMaxWidth = ((num5 > 500.0) ? 500.0 : num5);
		if (CaliperWidth > CaliperMaxWidth)
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				CaliperWidth = CaliperMaxWidth;
			}));
			return;
		}
		drawCanvas.ClearVisuals();
		CaliperRotateAngle = GetRotate(start, end);
		double num6 = CaliperWidth * ControlToImageRadio;
		double num7 = CaliperHeight * ControlToImageRadio;
		for (int num8 = 1; num8 < CaliperNum + 1; num8++)
		{
			double num9 = start.X + num3 * (double)num8;
			double num10 = start.Y + num4 * (double)num8;
			double num11 = num10 - num7 / 2.0;
			double num12 = num9 - num6 / 2.0;
			RotateTransform rotateTransform = new RotateTransform();
			rotateTransform.CenterX = num12 + num6 / 2.0;
			rotateTransform.CenterY = num11 + num7 / 2.0;
			rotateTransform.Angle = CaliperRotateAngle;
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.DrawRectangle(brush, pen, new Rect(new Point(num12, num11), new Size(num6, num7)));
			}
			drawingVisual.Transform = rotateTransform;
			if ((double)num8 == Math.Ceiling((double)CaliperNum / 2.0))
			{
				caliperRect.SetValue(Canvas.TopProperty, num11);
				caliperRect.SetValue(Canvas.LeftProperty, num12 + num6);
				caliperRotate.CenterX = (caliperRect.ActualWidth - num6) / 2.0;
				caliperRotate.CenterY = (caliperRect.ActualHeight + num7) / 2.0;
				caliperRotate.Angle = CaliperRotateAngle;
				RectCenterPoint = new Point(num9, num10);
			}
			else
			{
				drawCanvas.AddVisual(drawingVisual);
			}
		}
		double distance = GetDistance(start.X, start.Y, end.X, end.Y);
		double num13 = (start.X + end.X) / 2.0;
		double num14 = (start.Y + end.Y) / 2.0;
		double y = num14 - num7 / 2.0;
		double x = num13 - distance / 2.0;
		RotateTransform rotateTransform2 = new RotateTransform();
		rotateTransform2.CenterX = num13;
		rotateTransform2.CenterY = num14;
		rotateTransform2.Angle = CaliperRotateAngle;
		DrawingVisual drawingVisual2 = new DrawingVisual();
		using (DrawingContext drawingContext2 = drawingVisual2.RenderOpen())
		{
			drawingContext2.DrawRectangle(brush, pen2, new Rect(new Point(x, y), new Size(distance, num7)));
		}
		drawingVisual2.Transform = rotateTransform2;
		drawCanvas.AddVisual(drawingVisual2);
		OnPropertyChanged("RectWidth");
		OnPropertyChanged("RectHeight");
		OnPropertyChanged("RectTopLeft");
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
