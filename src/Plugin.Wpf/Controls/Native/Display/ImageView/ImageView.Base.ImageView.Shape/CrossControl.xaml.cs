using SunEyeVision.Plugin.Wpf.Interop.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VisualElements;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.Shape;

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
	private FrameworkElement dragHandle;
	private ImageViewItemsPage dragParent;
	private object dragContext;
	private long gestureVersion;
	private bool replacingTemplate;
	private ControlTemplate appliedTemplate;
	private System.Func<bool> interactionCurrent;

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
		if (e.Property != CenterPointProperty) crossControl.CancelGesture();
		crossControl.ChangedPosition();
	}

	private static void OnIsCheckChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CrossControl crossControl = d as CrossControl;
		if (crossControl.IsLocked) crossControl.CancelGesture();
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
		replacingTemplate = true;
		try
		{
			CancelGesture();
			if (move != null) move.MouseLeftButtonDown -= Move_MouseLeftButtonDown;
			base.OnApplyTemplate();
			appliedTemplate = Template;
			move = GetTemplateChild("move") as Grid;
			if (move != null) move.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			parent = this.GetParent<ImageViewItemsPage>();
		}
		finally { replacingTemplate = false; }
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		if (e.Property == TemplateProperty) CancelGesture();
		base.OnPropertyChanged(e);
	}

	public CrossControl()
	{
		InitializeComponent();
		Unloaded += (_, _) => CancelGesture();
		DataContextChanged += (_, _) =>
		{
			CancelGesture();
			interactionCurrent = DataContext is SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display.CrosshairSettings ? () => false : null;
		};
		IsEnabledChanged += (_, _) => { if (!IsEnabled) CancelGesture(); };
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
		if (replacingTemplate) return;
		if (!CheckInteraction()) return;
		var expected = gestureVersion + 1;
		CancelGesture();
		if (gestureVersion != expected) return;
		parent = this.GetParent<ImageViewItemsPage>();
		if (IsLocked || !IsEnabled || !IsVisible || !IsLoaded || !IsHitTestVisible || parent == null || sender != move
			|| !ReferenceEquals(appliedTemplate, Template)
			|| move.TemplatedParent != this
			|| !double.IsFinite(ControlToImageRadio) || ControlToImageRadio <= 0
			|| ImageSourceWidth <= 0 || ImageSourceHeight <= 0) return;
		var handle = (FrameworkElement)sender;
		var version = gestureVersion;
		dragHandle = handle; dragParent = parent; dragContext = DataContext;
		handle.LostMouseCapture += Move_LostMouseCapture;
		try
		{
			if (!handle.CaptureMouse() || version != gestureVersion || dragHandle != handle
				|| !handle.IsMouseCaptured) { if (version == gestureVersion) CancelGesture(); return; }
			// Capture can synchronously route pointer events. Start deltas only after
			// the capture has been accepted and its coordinate context is stable.
			clickPoint = e.GetPosition(parent);
			handle.MouseMove += Move_MouseMove;
			handle.MouseLeftButtonUp += Move_MouseLeftButtonUp;
			e.Handled = true;
		}
		catch (System.Exception failure)
		{
			try { if (version == gestureVersion) CancelGesture(); }
			catch (System.Exception cleanup) { throw new System.AggregateException(failure, cleanup); }
			throw;
		}
	}

	private void Move_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (sender == dragHandle) CancelGesture();
	}

	private void Move_LostMouseCapture(object sender, MouseEventArgs e)
	{ if (sender == dragHandle) CancelGesture(); }

	private void CancelGesture()
	{
		++gestureVersion;
		var handle = dragHandle;
		dragHandle = null; dragParent = null; dragContext = null;
		if (handle == null) return;
		handle.MouseMove -= Move_MouseMove;
		handle.MouseLeftButtonUp -= Move_MouseLeftButtonUp;
		handle.LostMouseCapture -= Move_LostMouseCapture;
		if (handle.IsMouseCaptured) handle.ReleaseMouseCapture();
	}
	internal void RetireInteraction() { interactionCurrent = () => false; CancelGesture(); }
	internal void BindInteraction(System.Func<bool> current) => interactionCurrent = current;
	private bool CheckInteraction()
	{
		var version = gestureVersion; var proof = interactionCurrent;
		try
		{
			var valid = proof?.Invoke() != false;
			if (version != gestureVersion || !ReferenceEquals(proof, interactionCurrent)) return false;
			if (!valid) CancelGesture();
			return valid;
		}
		catch (System.Exception failure)
		{
			try { if (version == gestureVersion) CancelGesture(); }
			catch (System.Exception cleanup) { throw new System.AggregateException(failure, cleanup); }
			throw;
		}
	}

	private void Move_MouseMove(object sender, MouseEventArgs e)
	{
		if (sender != dragHandle) return;
		var handle = dragHandle; var startVersion = gestureVersion;
		if (!CheckInteraction() || handle == null || !ReferenceEquals(handle, dragHandle) || startVersion != gestureVersion) return;
		if (IsLocked || !IsEnabled || !IsVisible || !dragHandle.IsMouseCaptured
			|| !ReferenceEquals(dragContext, DataContext) || dragParent != this.GetParent<ImageViewItemsPage>())
		{ CancelGesture(); return; }
		var version = gestureVersion;
		Point position = e.GetPosition(dragParent);
		Vector vector = (position - clickPoint) / ControlToImageRadio;
		var next = CenterPoint + vector;
		if (!double.IsFinite(next.X) || !double.IsFinite(next.Y)) { CancelGesture(); return; }
		try
		{
			// User interaction must retain the model's TwoWay binding.
			var binding = GetBindingExpression(CenterPointProperty);
			SetCurrentValue(CenterPointProperty, next);
			if (version == gestureVersion && ReferenceEquals(binding, GetBindingExpression(CenterPointProperty))) binding?.UpdateSource();
			if (version == gestureVersion) clickPoint = position;
		}
		catch { if (version == gestureVersion) CancelGesture(); throw; }
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
		if (!crossControl.IsVisible) crossControl.CancelGesture();
		crossControl.ChangedPosition();
	}
}
