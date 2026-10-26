using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public partial class CrossControl : Control, INotifyPropertyChanged, IComponentConnector
{
	private const int DEFAULT_WIDTH = 50;

	public static readonly DependencyProperty Line1Point1XProperty = DependencyProperty.Register("Line1Point1X", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty Line1Point1YProperty = DependencyProperty.Register("Line1Point1Y", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty Line1Point2XProperty = DependencyProperty.Register("Line1Point2X", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty Line1Point2YProperty = DependencyProperty.Register("Line1Point2Y", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty Line2Point1XProperty = DependencyProperty.Register("Line2Point1X", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty Line2Point1YProperty = DependencyProperty.Register("Line2Point1Y", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty Line2Point2XProperty = DependencyProperty.Register("Line2Point2X", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty Line2Point2YProperty = DependencyProperty.Register("Line2Point2Y", typeof(double), typeof(CrossControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty CenterPointProperty = DependencyProperty.Register("CenterPoint", typeof(Point), typeof(CrossControl), new PropertyMetadata(default(Point), PositionChanged));

	public static readonly DependencyProperty ControlToImageRadioProperty = DependencyProperty.Register("ControlToImageRadio", typeof(double), typeof(CrossControl), new PropertyMetadata(1.0, PositionChanged));

	public static readonly DependencyProperty ImageSourceWidthProperty = DependencyProperty.Register("ImageSourceWidth", typeof(int), typeof(CrossControl), new PropertyMetadata(0, PositionChanged));

	public static readonly DependencyProperty ImageSourceHeightProperty = DependencyProperty.Register("ImageSourceHeight", typeof(int), typeof(CrossControl), new PropertyMetadata(0, PositionChanged));

	public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register("IsLocked", typeof(bool), typeof(CrossControl), new PropertyMetadata(false, OnIsCheckChanged));

	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register("Stroke", typeof(Brush), typeof(CrossControl), new PropertyMetadata((object)null));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register("StrokeThickness", typeof(double), typeof(CrossControl), new PropertyMetadata(1.0, OnThicknessScaleChanged));

	public static readonly DependencyProperty ThicknessScaleProperty = DependencyProperty.Register("ThicknessScale", typeof(double), typeof(CrossControl), new PropertyMetadata(1.0, OnThicknessScaleChanged));

	private Thickness moveMargin;

	private ImageViewItemsPage parent;

	private Grid move;

	private Point clickPoint;

	public double Line1Point1X
	{
		get
		{
			return (double)GetValue(Line1Point1XProperty);
		}
		set
		{
			SetValue(Line1Point1XProperty, value);
		}
	}

	public double Line1Point1Y
	{
		get
		{
			return (double)GetValue(Line1Point1YProperty);
		}
		set
		{
			SetValue(Line1Point1YProperty, value);
		}
	}

	public double Line1Point2X
	{
		get
		{
			return (double)GetValue(Line1Point2XProperty);
		}
		set
		{
			SetValue(Line1Point2XProperty, value);
		}
	}

	public double Line1Point2Y
	{
		get
		{
			return (double)GetValue(Line1Point2YProperty);
		}
		set
		{
			SetValue(Line1Point2YProperty, value);
		}
	}

	public double Line2Point1X
	{
		get
		{
			return (double)GetValue(Line2Point1XProperty);
		}
		set
		{
			SetValue(Line2Point1XProperty, value);
		}
	}

	public double Line2Point1Y
	{
		get
		{
			return (double)GetValue(Line2Point1YProperty);
		}
		set
		{
			SetValue(Line2Point1YProperty, value);
		}
	}

	public double Line2Point2X
	{
		get
		{
			return (double)GetValue(Line2Point2XProperty);
		}
		set
		{
			SetValue(Line2Point2XProperty, value);
		}
	}

	public double Line2Point2Y
	{
		get
		{
			return (double)GetValue(Line2Point2YProperty);
		}
		set
		{
			SetValue(Line2Point2YProperty, value);
		}
	}

	public Point CenterPoint
	{
		get
		{
			return (Point)GetValue(CenterPointProperty);
		}
		set
		{
			SetValue(CenterPointProperty, value);
		}
	}

	public double ControlToImageRadio
	{
		get
		{
			return (double)GetValue(ControlToImageRadioProperty);
		}
		set
		{
			SetValue(ControlToImageRadioProperty, value);
		}
	}

	public int ImageSourceWidth
	{
		get
		{
			return (int)GetValue(ImageSourceWidthProperty);
		}
		set
		{
			SetValue(ImageSourceWidthProperty, value);
		}
	}

	public int ImageSourceHeight
	{
		get
		{
			return (int)GetValue(ImageSourceHeightProperty);
		}
		set
		{
			SetValue(ImageSourceHeightProperty, value);
		}
	}

	public bool IsLocked
	{
		get
		{
			return (bool)GetValue(IsLockedProperty);
		}
		set
		{
			SetValue(IsLockedProperty, value);
		}
	}

	public Brush Stroke
	{
		get
		{
			return (Brush)GetValue(StrokeProperty);
		}
		set
		{
			SetValue(StrokeProperty, value);
		}
	}

	public double StrokeThickness
	{
		get
		{
			return (double)GetValue(StrokeThicknessProperty);
		}
		set
		{
			SetValue(StrokeThicknessProperty, value);
		}
	}

	public double ThicknessScale
	{
		get
		{
			return (double)GetValue(ThicknessScaleProperty);
		}
		set
		{
			SetValue(ThicknessScaleProperty, value);
		}
	}

	public double StrokeThicknessR
	{
		get
		{
			if (ThicknessScale == 0.0)
			{
				return StrokeThickness;
			}
			return StrokeThickness / ThicknessScale;
		}
	}

	public Thickness MoveMargin
	{
		get
		{
			return moveMargin;
		}
		set
		{
			if (value != moveMargin)
			{
				moveMargin = value;
				OnPropertyChanged("MoveMargin");
			}
		}
	}

	public double StrokeThicknessMove
	{
		get
		{
			if (ThicknessScale == 0.0)
			{
				return StrokeThickness;
			}
			return StrokeThickness * 3.0 / ThicknessScale;
		}
	}

	public double MoveWidth
	{
		get
		{
			if (ThicknessScale == 0.0)
			{
				return 50.0;
			}
			return 50.0 / ThicknessScale;
		}
	}

	public Visibility LockVisibility => IsLocked ? Visibility.Collapsed : Visibility.Visible;

	public Visibility UnLockVisibility => (!IsLocked) ? Visibility.Collapsed : Visibility.Visible;

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private static void PositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CrossControl crossControl = d as CrossControl;
		crossControl.ChangedPosition();
	}

	private static void OnIsCheckChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CrossControl crossControl = d as CrossControl;
		crossControl.OnPropertyChanged("LockVisibility");
		crossControl.OnPropertyChanged("UnLockVisibility");
	}

	private static void OnThicknessScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CrossControl crossControl = d as CrossControl;
		crossControl.ChangeThickness();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		move = Template.FindName("move", this) as Grid;
		move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		parent = this.GetParent<ImageViewItemsPage>();
	}

	public CrossControl()
	{
		InitializeComponent();
	}

	private void ChangedPosition()
	{
		if (ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			Line1Point1X = CenterPoint.X * ControlToImageRadio;
			Line1Point1Y = 0.0;
			Line1Point2X = CenterPoint.X * ControlToImageRadio;
			Line1Point2Y = (double)ImageSourceHeight * ControlToImageRadio;
			Line2Point1X = 0.0;
			Line2Point1Y = CenterPoint.Y * ControlToImageRadio;
			Line2Point2X = (double)ImageSourceWidth * ControlToImageRadio;
			Line2Point2Y = CenterPoint.Y * ControlToImageRadio;
			ChangeThickness();
		}
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (!IsLocked)
		{
			FrameworkElement frameworkElement = sender as FrameworkElement;
			clickPoint = e.GetPosition(parent);
			frameworkElement.CaptureMouse();
			frameworkElement.MouseMove += Move_MouseMove;
			frameworkElement.MouseLeftButtonUp += Move_MouseLeftButtonUp;
			e.Handled = true;
		}
	}

	private void Move_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!IsLocked)
		{
			FrameworkElement frameworkElement = sender as FrameworkElement;
			frameworkElement.MouseMove -= Move_MouseMove;
			frameworkElement.MouseLeftButtonUp -= Move_MouseLeftButtonUp;
			frameworkElement.ReleaseMouseCapture();
		}
	}

	private void Move_MouseMove(object sender, MouseEventArgs e)
	{
		if (!IsLocked)
		{
			FrameworkElement frameworkElement = sender as FrameworkElement;
			Point position = e.GetPosition(parent);
			Vector vector = (position - clickPoint) / ControlToImageRadio;
			CenterPoint += vector;
			clickPoint = position;
		}
	}

	private void ChangeThickness()
	{
		MoveMargin = new Thickness(CenterPoint.X * ControlToImageRadio - MoveWidth / 2.0, CenterPoint.Y * ControlToImageRadio - MoveWidth / 2.0, 0.0, 0.0);
		OnPropertyChanged("StrokeThicknessR");
		OnPropertyChanged("StrokeThicknessMove");
		OnPropertyChanged("MoveWidth");
	}

	private void Control_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		CrossControl crossControl = sender as CrossControl;
		crossControl.ChangedPosition();
	}
}
