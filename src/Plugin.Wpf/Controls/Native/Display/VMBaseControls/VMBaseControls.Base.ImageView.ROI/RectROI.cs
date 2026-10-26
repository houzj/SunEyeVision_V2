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
using System.Windows.Shapes;
using Apps.UIHelper.Extern;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IRectROI))]
public class RectROI : ROIControlBase
{
	protected static RectROISubstitute ROISubstitute;

	protected static Func<Rect, double, ROIItemsPage, object> GetDataFunc;

	public static readonly DependencyProperty ContainerWidthProperty;

	public static readonly DependencyProperty ContainerHeightProperty;

	public static readonly DependencyProperty ContainerAngleProperty;

	protected Grid move;

	private Area rect1;

	private Area rect2;

	private Area rect3;

	private Area rect4;

	private Area left;

	private Area top;

	private Area right;

	private Area bottom;

	private Line line;

	private RotateArea topLeftRotate;

	private RotateArea topRightRotate;

	private RotateArea bottomLeftRotate;

	private RotateArea bottomRightRotate;

	private System.Windows.Point clickPoint;

	private ImageViewItemsPage parent;

	private RectangleGeometry eRect;

	private static Bitmap image;

	private RotateTransform rotateTransform = new RotateTransform();

	private double angleWidth = 9.0;

	private double angleHeight = 13.0;

	private double arrowWidth = 6.0;

	public static readonly DependencyProperty IsRotatableProperty;

	public static readonly DependencyProperty RectWidthProperty;

	public static readonly DependencyProperty RectHeightProperty;

	public static readonly DependencyProperty RectCenterPointProperty;

	public static readonly DependencyProperty RectAngleProperty;

	public static readonly DependencyProperty IsFullScreenProperty;

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

	public double AngleWidth => angleWidth * StrokeThickness;

	public double AngleHeight => angleHeight * StrokeThickness;

	public Thickness AngelMargin => new Thickness((0.0 - AngleWidth) / 2.0, 0.0 - AngleHeight, (0.0 - AngleWidth) / 2.0, 0.0);

	public Thickness BottomArrowMargin => new Thickness(0.0 - ArrowWidth, 0.0 - ArrowWidth, 0.0, 0.0 - ArrowWidth);

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

	public double RectWidth
	{
		get
		{
			return (double)GetValue(RectWidthProperty);
		}
		set
		{
			SetValue(RectWidthProperty, value);
		}
	}

	public double RectHeight
	{
		get
		{
			return (double)GetValue(RectHeightProperty);
		}
		set
		{
			SetValue(RectHeightProperty, value);
		}
	}

	public System.Windows.Point RectCenterPoint
	{
		get
		{
			return (System.Windows.Point)GetValue(RectCenterPointProperty);
		}
		set
		{
			SetValue(RectCenterPointProperty, value);
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

	public bool IsFullScreen
	{
		get
		{
			return (bool)GetValue(IsFullScreenProperty);
		}
		set
		{
			SetValue(IsFullScreenProperty, value);
		}
	}

	public Thickness MoveRotateMargin => new Thickness(4.0 * dwidth * NegativeStrokeThickness);

	public System.Windows.Point StartPoint
	{
		get
		{
			System.Windows.Point point = new System.Windows.Point(ContainerWidth / 2.0, ContainerHeight / 2.0);
			return new System.Windows.Point(point.X - 1.5 * ContainerWidth, point.Y);
		}
	}

	public System.Windows.Point EndPoint
	{
		get
		{
			System.Windows.Point point = new System.Windows.Point(ContainerWidth / 2.0, ContainerHeight / 2.0);
			return new System.Windows.Point(point.X + 1.5 * ContainerWidth, point.Y);
		}
	}

	public bool IsSeqRect { get; set; }

	static RectROI()
	{
		ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(RectROI), new PropertyMetadata(0.0));
		ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(RectROI), new PropertyMetadata(0.0));
		ContainerAngleProperty = DependencyProperty.Register("ContainerAngle", typeof(double), typeof(RectROI), new PropertyMetadata(0.0));
		IsRotatableProperty = DependencyProperty.Register("IsRotatable", typeof(bool), typeof(RectROI), new PropertyMetadata(true));
		RectWidthProperty = DependencyProperty.Register("RectWidth", typeof(double), typeof(RectROI), new PropertyMetadata(0.0, OnRectWidthChanged));
		RectHeightProperty = DependencyProperty.Register("RectHeight", typeof(double), typeof(RectROI), new PropertyMetadata(0.0, OnRectHeightChanged));
		RectCenterPointProperty = DependencyProperty.Register("RectCenterPoint", typeof(System.Windows.Point), typeof(RectROI), new PropertyMetadata(default(System.Windows.Point), OnRectCenterPointChanged));
		RectAngleProperty = DependencyProperty.Register("RectAngle", typeof(double), typeof(RectROI), new PropertyMetadata(0.0, OnRectAngleChanged));
		IsFullScreenProperty = DependencyProperty.Register("IsFullScreen", typeof(bool), typeof(RectROI), new PropertyMetadata(false));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(RectROI), new FrameworkPropertyMetadata(typeof(RectROI)));
	}

	public static void Draw(FrameworkElement parent, Type roiDataType)
	{
		ROIControlBase.BaseBeforeDraw(parent, roiDataType);
		ROISubstitute = new RectROISubstitute();
		ROISubstitute.SubstituteRect = new Rect(ROIControlBase.mouseOffset.X, ROIControlBase.mouseOffset.Y, 0.0, 0.0);
		ROIControlBase.BaseAfterDraw(ROISubstitute, parent);
		parent.MouseMove += Parent_MouseMove;
		parent.MouseLeftButtonUp += Parent_MouseLeftButtonUp;
		GetDataFunc = RectCetData;
	}

	protected static void Parent_MouseMove(object sender, MouseEventArgs e)
	{
		if (ROISubstitute != null)
		{
			if (e.LeftButton == MouseButtonState.Pressed && !ROIControlBase.IsMoved)
			{
				ROIControlBase.IsMoved = true;
			}
			System.Windows.Point position = e.GetPosition(sender as IInputElement);
			ROISubstitute.SubstituteRect = new Rect(ROIControlBase.mouseOffset, position - ROIControlBase.mouseOffset);
		}
	}

	private static object RectCetData(Rect rect, double radio, ROIItemsPage parent)
	{
		IRectROI rectROI = Activator.CreateInstance(ROIControlBase.drawRoiType) as IRectROI;
		int num = 1;
		int num2 = 1;
		if (rectROI is INormalizedROI)
		{
			num = parent.ImageWidth;
			num2 = parent.ImageHeight;
		}
		rectROI.CenterPoint = new System.Windows.Point(((rect.X + rect.Width / 2.0) / radio - 0.0) / (double)num, ((rect.Y + rect.Height / 2.0) / radio - 0.0) / (double)num2);
		rectROI.Width = rect.Width / radio / (double)num;
		rectROI.Height = rect.Height / radio / (double)num2;
		return rectROI;
	}

	protected static void Parent_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		ROIItemsPage rOIItemsPage = sender as ROIItemsPage;
		if (ROISubstitute != null)
		{
			if (!ROIControlBase.IsMoved && (ROISubstitute.SubstituteRect.Width > 0.0 || ROISubstitute.SubstituteRect.Height > 0.0))
			{
				ROIControlBase.IsMoved = true;
			}
			if (ROIControlBase.IsMoved)
			{
				Rect substituteRect = ROISubstitute.SubstituteRect;
				double controlToImageRadio = ROISubstitute.ControlToImageRadio;
				object data = GetDataFunc(substituteRect, controlToImageRadio, rOIItemsPage);
				rOIItemsPage.MouseMove -= Parent_MouseMove;
				rOIItemsPage.MouseLeftButtonUp -= Parent_MouseLeftButtonUp;
				ROIControlBase.CompleteDraw(data, rOIItemsPage);
				ROIControlBase.BaseFinishDraw(ROISubstitute, rOIItemsPage);
			}
		}
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (!IsFullScreen)
		{
			move = Template.FindName("move", this) as Grid;
			move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			rect1 = Template.FindName("rect1", this) as Area;
			rect1.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			rect2 = Template.FindName("rect2", this) as Area;
			rect2.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			rect3 = Template.FindName("rect3", this) as Area;
			rect3.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			rect4 = Template.FindName("rect4", this) as Area;
			rect4.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			left = Template.FindName("left", this) as Area;
			left.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			top = Template.FindName("top", this) as Area;
			top.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			right = Template.FindName("right", this) as Area;
			right.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			bottom = Template.FindName("bottom", this) as Area;
			bottom.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			topLeftRotate = Template.FindName("topLeftRotate", this) as RotateArea;
			topLeftRotate.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			topRightRotate = Template.FindName("topRightRotate", this) as RotateArea;
			topRightRotate.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			bottomLeftRotate = Template.FindName("bottomLeftRotate", this) as RotateArea;
			bottomLeftRotate.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			bottomRightRotate = Template.FindName("bottomRightRotate", this) as RotateArea;
			bottomRightRotate.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		}
		line = Template.FindName("line", this) as Line;
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

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		string name = e.Property.Name;
		if (!(name == "ControlToImageRadio"))
		{
			if (name == "StrokeThickness")
			{
				OnPropertyChanged("MoveRotateMargin");
				OnPropertyChanged("AngleWidth");
				OnPropertyChanged("AngleHeight");
				OnPropertyChanged("AngelMargin");
				OnPropertyChanged("ArrowWidth");
				OnPropertyChanged("BottomArrowMargin");
				double num = StrokeThickness * 5.0;
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

	protected void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		clickPoint = e.GetPosition(parent);
		this.GetParent<ListViewItem>().RaiseEvent(e);
		frameworkElement.CaptureMouse();
		ImageViewBox imageViewBox = this.GetParent<ImageViewBox>();
		if (imageViewBox != null)
		{
			imageViewBox.IsDragROI = true;
			frameworkElement.MouseMove += Move_MouseMove;
			frameworkElement.MouseLeftButtonUp += Move_MouseLeftButtonUp;
			if (frameworkElement.Name == "move")
			{
				frameworkElement.MouseWheel += userControl_MouseWheel;
			}
			e.Handled = true;
		}
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
		rotateTransform.Angle = RectAngle;
		System.Windows.Point point = rotateTransform.Inverse.Transform(new System.Windows.Point(vector.X, vector.Y));
		Vector newOffSet = new Vector(point.X, point.Y);
		double rectWidth = RectWidth;
		double rectHeight = RectHeight;
		switch (frameworkElement.Name)
		{
		case "left":
		{
			rectWidth -= newOffSet.X;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: true, isTop: true);
			System.Windows.Point point4 = rotateTransform.Transform(new System.Windows.Point(newOffSet.X, 0.0));
			RectCenterPoint += new Vector(point4.X, point4.Y) / 2.0;
			break;
		}
		case "top":
		{
			rectHeight -= newOffSet.Y;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: false, isTop: true);
			System.Windows.Point point3 = rotateTransform.Transform(new System.Windows.Point(0.0, newOffSet.Y));
			RectCenterPoint += new Vector(point3.X, point3.Y) / 2.0;
			break;
		}
		case "right":
		{
			rectWidth += newOffSet.X;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: false, isTop: true);
			System.Windows.Point point2 = rotateTransform.Transform(new System.Windows.Point(newOffSet.X, 0.0));
			RectCenterPoint += new Vector(point2.X, point2.Y) / 2.0;
			break;
		}
		case "bottom":
		{
			rectHeight += newOffSet.Y;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: false, isTop: false);
			System.Windows.Point point5 = rotateTransform.Transform(new System.Windows.Point(0.0, newOffSet.Y));
			RectCenterPoint += new Vector(point5.X, point5.Y) / 2.0;
			break;
		}
		case "move":
			RectCenterPoint += vector;
			clickPoint = position;
			break;
		case "rect1":
		{
			rectWidth -= newOffSet.X;
			rectHeight -= newOffSet.Y;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: true, isTop: true);
			System.Windows.Point point9 = rotateTransform.Transform(new System.Windows.Point(newOffSet.X, newOffSet.Y));
			RectCenterPoint += new Vector(point9.X, point9.Y) / 2.0;
			break;
		}
		case "rect2":
		{
			rectWidth += newOffSet.X;
			rectHeight -= newOffSet.Y;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: false, isTop: true);
			System.Windows.Point point8 = rotateTransform.Transform(new System.Windows.Point(newOffSet.X, newOffSet.Y));
			RectCenterPoint += new Vector(point8.X, point8.Y) / 2.0;
			break;
		}
		case "rect3":
		{
			rectWidth += newOffSet.X;
			rectHeight += newOffSet.Y;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: false, isTop: false);
			System.Windows.Point point7 = rotateTransform.Transform(new System.Windows.Point(newOffSet.X, newOffSet.Y));
			RectCenterPoint += new Vector(point7.X, point7.Y) / 2.0;
			break;
		}
		case "rect4":
		{
			rectWidth -= newOffSet.X;
			rectHeight += newOffSet.Y;
			AdjustRect(position, rectWidth, rectHeight, ref newOffSet, isLeft: true, isTop: false);
			System.Windows.Point point6 = rotateTransform.Transform(new System.Windows.Point(newOffSet.X, newOffSet.Y));
			RectCenterPoint += new Vector(point6.X, point6.Y) / 2.0;
			break;
		}
		case "bottomRightRotate":
		case "bottomLeftRotate":
		case "topRightRotate":
		case "topLeftRotate":
		{
			Vector vector2 = new Vector(position.X / ControlToImageRadio - RectCenterPoint.X, position.Y / ControlToImageRadio - RectCenterPoint.Y);
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
			break;
		}
		default:
			ExternMouseMove(frameworkElement.Name, position, vector, ref clickPoint);
			break;
		}
	}

	protected virtual void ExternMouseMove(string icName, System.Windows.Point curPoint, Vector offSet, ref System.Windows.Point clickPoint)
	{
	}

	private void AdjustRect(System.Windows.Point curPoint, double tempRectWidth, double tempRectHeight, ref Vector newOffSet, bool isLeft, bool isTop)
	{
		bool flag = false;
		if (tempRectWidth < 0.0)
		{
			newOffSet.X = (double)(isLeft ? 1 : (-1)) * RectWidth;
			RectWidth = 0.0;
			flag = true;
		}
		else
		{
			RectWidth = tempRectWidth;
		}
		if (tempRectHeight < 0.0)
		{
			newOffSet.Y = (double)(isTop ? 1 : (-1)) * RectHeight;
			RectHeight = 0.0;
			flag = true;
		}
		else
		{
			RectHeight = tempRectHeight;
		}
		if (flag)
		{
			System.Windows.Point point = rotateTransform.Transform(new System.Windows.Point(newOffSet.X, newOffSet.Y));
			Vector vector = new Vector(point.X, point.Y) * ControlToImageRadio;
			clickPoint += vector;
		}
		else
		{
			clickPoint = curPoint;
		}
	}

	private void Move_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		FrameworkElement frameworkElement = sender as FrameworkElement;
		frameworkElement.MouseMove -= Move_MouseMove;
		frameworkElement.MouseLeftButtonUp -= Move_MouseLeftButtonUp;
		frameworkElement.ReleaseMouseCapture();
		if (frameworkElement.Name == "move")
		{
			frameworkElement.MouseWheel -= userControl_MouseWheel;
		}
		if (_isMove)
		{
			NotifyROIParamChanged();
			_isMove = false;
		}
		AfterMouseUp();
	}

	private void ChangeContainerWidth()
	{
		ContainerWidth = RectWidth * ControlToImageRadio;
	}

	private void ChangeContainerHeight()
	{
		ContainerHeight = RectHeight * ControlToImageRadio;
	}

	private void ChangeContainerPosition()
	{
		double x = (RectCenterPoint.X - RectWidth / 2.0 + 0.0) * ControlToImageRadio;
		double y = (RectCenterPoint.Y - RectHeight / 2.0 + 0.0) * ControlToImageRadio;
		ContainerPosition = new System.Windows.Point(x, y);
		SetCenterLinePosition();
	}

	private void SetCenterLinePosition()
	{
		OnPropertyChanged("StartPoint");
		OnPropertyChanged("EndPoint");
	}

	private void userControl_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		double deltastep = Math.Round((double)e.Delta / 120.0, MidpointRounding.AwayFromZero) * 0.5;
		AddAngle(deltastep);
		e.Handled = true;
	}

	private void AddAngle(double deltastep)
	{
		double num = RectAngle + deltastep;
		if (num < -180.0)
		{
			RectAngle = 360.0 + num;
		}
		else if (num > 180.0)
		{
			RectAngle = num - 360.0;
		}
		else
		{
			RectAngle = num;
		}
	}

	private static void OnRectWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		RectROI rectROI = d as RectROI;
		rectROI.ChangeContainerWidth();
		rectROI.ChangeContainerPosition();
	}

	private static void OnRectHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		RectROI rectROI = d as RectROI;
		rectROI.ChangeContainerHeight();
		rectROI.ChangeContainerPosition();
	}

	private static void OnRectCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		RectROI rectROI = d as RectROI;
		rectROI.ChangeContainerPosition();
	}

	private static void OnRectAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		RectROI rectROI = d as RectROI;
		double containerAngle = (double)e.NewValue;
		rectROI.ContainerAngle = containerAngle;
	}

	protected override bool Move(ROIMoveDirection rOIMoveDirection)
	{
		switch (rOIMoveDirection)
		{
		case ROIMoveDirection.Left:
			RectCenterPoint = new System.Windows.Point(RectCenterPoint.X - 1.0, RectCenterPoint.Y);
			break;
		case ROIMoveDirection.Right:
			RectCenterPoint = new System.Windows.Point(RectCenterPoint.X + 1.0, RectCenterPoint.Y);
			break;
		case ROIMoveDirection.Up:
			RectCenterPoint = new System.Windows.Point(RectCenterPoint.X, RectCenterPoint.Y - 1.0);
			break;
		case ROIMoveDirection.Down:
			RectCenterPoint = new System.Windows.Point(RectCenterPoint.X, RectCenterPoint.Y + 1.0);
			break;
		case ROIMoveDirection.LeftRotate:
			AddAngle(-1.0);
			break;
		case ROIMoveDirection.RightRotate:
			AddAngle(1.0);
			break;
		default:
			return false;
		}
		return true;
	}
}
