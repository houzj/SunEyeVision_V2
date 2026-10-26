using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Apps.UIHelper.Extern;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(ILineROI))]
public class LineROI : ROIControlBase
{
	private static LineROISubstitute ROISubstitute;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty StartPointProperty;

	public static readonly DependencyProperty EndPointProperty;

	private Canvas move;

	private Arrow line;

	private Ellipse startPoint;

	private Ellipse endPoint;

	private Point clickPoint;

	private Point curStartPoint;

	private Point curEndPoint;

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

	public Point Start => GetControlPoint(StartPoint);

	public Point End => GetControlPoint(EndPoint);

	static LineROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(LineROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(LineROI), new PropertyMetadata(0.0));
		StartPointProperty = DependencyProperty.Register("StartPoint", typeof(Point), typeof(LineROI), new PropertyMetadata(new Point(0.0, 0.0), OnStartPointChanged));
		EndPointProperty = DependencyProperty.Register("EndPoint", typeof(Point), typeof(LineROI), new PropertyMetadata(new Point(0.0, 0.0), OnEndPointChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(LineROI), new FrameworkPropertyMetadata(typeof(LineROI)));
	}

	~LineROI()
	{
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new LineROISubstitute();
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
			ILineROI lineROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ILineROI;
			int num = 1;
			int num2 = 1;
			if (lineROI is INormalizedROI)
			{
				num = rOIItemsPage.ImageWidth;
				num2 = rOIItemsPage.ImageHeight;
			}
			lineROI.StartPoint = new Point((point.X / controlToImageRadio - 0.0) / (double)num, (point.Y / controlToImageRadio - 0.0) / (double)num2);
			lineROI.EndPoint = new Point((point2.X / controlToImageRadio - 0.0) / (double)num, (point2.Y / controlToImageRadio - 0.0) / (double)num2);
			rOIItemsPage.MouseMove -= Parent_MouseMove;
			rOIItemsPage.MouseDown -= Parent_MouseDown;
			ROIControlBase.CompleteDraw(lineROI, rOIItemsPage);
			ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
		}
	}

	private static void OnStartPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LineROI).OnPropertyChanged("Start");
	}

	private static void OnEndPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LineROI).OnPropertyChanged("End");
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Canvas;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		line = Template.FindName("line", this) as Arrow;
		startPoint = Template.FindName("startPoint", this) as Ellipse;
		startPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		endPoint = Template.FindName("endPoint", this) as Ellipse;
		endPoint.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
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
			}
		}
		else
		{
			OnPropertyChanged("Start");
			OnPropertyChanged("End");
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
