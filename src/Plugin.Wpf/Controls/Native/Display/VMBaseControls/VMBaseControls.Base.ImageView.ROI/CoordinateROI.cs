using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Apps.UIHelper.Extern;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(ICoordinateROI))]
public class CoordinateROI : ROIControlBase
{
	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty ContainerAngleProperty;

	public static readonly DependencyProperty CenterPointProperty;

	public static readonly DependencyProperty RectAngleProperty;

	public static readonly DependencyProperty IsRotatableProperty;

	private Grid move;

	private System.Windows.Point clickPoint;

	private ImageViewItemsPage parent;

	private RotateArea topLeftRotate;

	private RotateArea topRightRotate;

	private RotateArea bottomLeftRotate;

	private RotateArea bottomRightRotate;

	private RectangleGeometry eRect;

	private double RightTopCursorAngle = double.NaN;

	private Cursor _RightTopCursor;

	private static Bitmap image;

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

	public System.Windows.Point CenterPoint
	{
		get
		{
			return (System.Windows.Point)GetValue(CenterPointProperty);
		}
		set
		{
			SetValue(CenterPointProperty, value);
		}
	}

	public double RectAngle
	{
		get
		{
			return (double)GetValue(RectAngleProperty);
		}
		set
		{
			SetValue(RectAngleProperty, value);
		}
	}

	public bool IsRotatable
	{
		get
		{
			return (bool)GetValue(IsRotatableProperty);
		}
		set
		{
			SetValue(IsRotatableProperty, value);
		}
	}

	public Thickness MoveRotateMargin => new Thickness(4.0 * dwidth * NegativeStrokeThickness);

	public System.Windows.Point Center => GetControlPoint(CenterPoint);

	public Cursor RightTopCursor
	{
		get
		{
			if (RightTopCursorAngle != RectAngle && (double.IsNaN(RightTopCursorAngle) || Math.Abs(RightTopCursorAngle - RectAngle) > 1.0))
			{
				RightTopCursorAngle = RectAngle;
				_RightTopCursor?.Dispose();
				Bitmap rotateImage = ROIControlFrame.GetRotateImage(GetBiArrowImage(), (int)RectAngle - 45);
				_RightTopCursor = BitmapCursor.CreateBmpCursor(rotateImage);
			}
			return _RightTopCursor;
		}
	}

	public Thickness TopArrowMargin => new Thickness(0.0 - ArrowWidth, 0.0 - ArrowWidth, 0.0, 0.0 - ArrowWidth);

	static CoordinateROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(CoordinateROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(CoordinateROI), new PropertyMetadata(0.0));
		ContainerAngleProperty = DependencyProperty.Register("ContainerAngle", typeof(double), typeof(CoordinateROI), new PropertyMetadata(0.0));
		CenterPointProperty = DependencyProperty.Register("CenterPoint", typeof(System.Windows.Point), typeof(CoordinateROI), new PropertyMetadata(new System.Windows.Point(0.0, 0.0), OnCenterPointChanged));
		RectAngleProperty = DependencyProperty.Register("RectAngle", typeof(double), typeof(CoordinateROI), new PropertyMetadata(0.0, OnRectAngleChanged));
		IsRotatableProperty = DependencyProperty.Register("IsRotatable", typeof(bool), typeof(CoordinateROI), new PropertyMetadata(true));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CoordinateROI), new FrameworkPropertyMetadata(typeof(CoordinateROI)));
	}

	public CoordinateROI()
	{
		Loaded += PointROI_Loaded;
	}

	private void PointROI_Loaded(object sender, RoutedEventArgs e)
	{
		ChangeContainerSize();
	}

	~CoordinateROI()
	{
	}

	public static void Draw(FrameworkElement sender, Type roiDataType)
	{
		ROIItemsPage rOIItemsPage = sender as ROIItemsPage;
		ROIControlBase.BaseBeforeDraw(rOIItemsPage, roiDataType);
		double controlToImageRadio = rOIItemsPage.ControlToImageRadio;
		ICoordinateROI coordinateROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as ICoordinateROI;
		int num = 1;
		int num2 = 1;
		if (coordinateROI is INormalizedROI)
		{
			num = rOIItemsPage.ImageWidth;
			num2 = rOIItemsPage.ImageHeight;
		}
		coordinateROI.OrgPoint = new System.Windows.Point((ROIControlBase.mouseOffset.X / controlToImageRadio - 0.0) / (double)num, (ROIControlBase.mouseOffset.Y / controlToImageRadio - 0.0) / (double)num2);
		rOIItemsPage.CompleteDrawSubstitute(coordinateROI);
	}

	private static void OnCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
	}

	private static void OnRectAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CoordinateROI coordinateROI = d as CoordinateROI;
		double containerAngle = (double)e.NewValue;
		coordinateROI.ContainerAngle = containerAngle;
		coordinateROI.OnPropertyChanged("RightTopCursor");
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		topRightRotate = Template.FindName("topRightRotate", this) as RotateArea;
		topRightRotate.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		eRect = Template.FindName("eRect", this) as RectangleGeometry;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	public static Bitmap GetBiArrowImage()
	{
		if (image == null)
		{
			Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Resources.鼠标双向箭头.png");
			image = (Bitmap)System.Drawing.Image.FromStream(manifestResourceStream);
			manifestResourceStream.Dispose();
		}
		return image;
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
		System.Windows.Point position = e.GetPosition(parent);
		Vector vector = (position - clickPoint) / ControlToImageRadio;
		string name = frameworkElement.Name;
		if (!(name == "move"))
		{
			if (name == "topRightRotate")
			{
				Vector vector2 = new Vector(position.X / ControlToImageRadio - CenterPoint.X, position.Y / ControlToImageRadio - CenterPoint.Y);
				Vector vector3 = vector2 + vector;
				double num = Vector.AngleBetween(vector2, vector3);
				double num2 = RectAngle + num;
				if (num2 > 180.0)
				{
					num2 -= 360.0;
				}
				else if (num2 < -180.0)
				{
					num2 += 360.0;
				}
				RectAngle = num2;
				clickPoint = position;
			}
		}
		else
		{
			CenterPoint = GetImagePoint(position);
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
		{
			ChangeContainerSize();
			OnPropertyChanged("TopArrowMargin");
			double num = StrokeThickness * 5.0;
			if (eRect != null)
			{
				eRect.Rect = new Rect(num, 0.0, num, num);
			}
			break;
		}
		}
	}

	private void ChangeContainerSize()
	{
		ContainerWidth = HotStrokeThickness * 80.0;
		ContainerHeight = ContainerWidth;
		ChangeContainerPosition();
	}

	private void ChangeContainerPosition()
	{
		double x = Center.X - ContainerWidth / 2.0;
		double y = Center.Y - ContainerHeight / 2.0;
		ContainerPosition = new System.Windows.Point(x, y);
	}

	protected override bool Move(ROIMoveDirection rOIMoveDirection)
	{
		switch (rOIMoveDirection)
		{
		case ROIMoveDirection.Left:
			CenterPoint = new System.Windows.Point(CenterPoint.X - 1.0, CenterPoint.Y);
			break;
		case ROIMoveDirection.Right:
			CenterPoint = new System.Windows.Point(CenterPoint.X + 1.0, CenterPoint.Y);
			break;
		case ROIMoveDirection.Up:
			CenterPoint = new System.Windows.Point(CenterPoint.X, CenterPoint.Y - 1.0);
			break;
		case ROIMoveDirection.Down:
			CenterPoint = new System.Windows.Point(CenterPoint.X, CenterPoint.Y + 1.0);
			break;
		default:
			return false;
		}
		return true;
	}
}
