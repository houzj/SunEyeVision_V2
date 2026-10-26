using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class ExpanderButton : ToggleButton, INotifyPropertyChanged
{
	public static readonly DependencyProperty DirectionProperty;

	public static readonly DependencyProperty AngleProperty;

	public ExpandDirection Direction
	{
		get
		{
			return (ExpandDirection)GetValue(DirectionProperty);
		}
		set
		{
			SetValue(DirectionProperty, value);
		}
	}

	public double Angle
	{
		get
		{
			return (double)GetValue(AngleProperty);
		}
		set
		{
			SetValue(AngleProperty, value);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	static ExpanderButton()
	{
		DirectionProperty = DependencyProperty.Register("Direction", typeof(ExpandDirection), typeof(ExpanderButton), new PropertyMetadata(ExpandDirection.LeftToRight, (DependencyObject s, DependencyPropertyChangedEventArgs e) =>
		{
			if (s is ExpanderButton expanderButton)
			{
				expanderButton.OnAngleChanged();
			}
		}));
		AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(ExpanderButton), new PropertyMetadata(0.0));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ExpanderButton), new FrameworkPropertyMetadata(typeof(ExpanderButton)));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		OnAngleChanged();
	}

	protected override void OnClick()
	{
		base.OnClick();
		OnAngleChanged();
	}

	protected void OnAngleChanged()
	{
		if (Visibility != Visibility.Collapsed)
		{
			switch (Direction)
			{
			case ExpandDirection.DownToUp:
				Angle = ((IsChecked.HasValue && IsChecked.Value) ? 180 : 0);
				break;
			case ExpandDirection.LeftToRight:
				Angle = ((IsChecked.HasValue && IsChecked.Value) ? (-90) : 90);
				break;
			case ExpandDirection.RightToLeft:
				Angle = ((IsChecked.HasValue && IsChecked.Value) ? 90 : (-90));
				break;
			case ExpandDirection.UpToDown:
				Angle = ((IsChecked.HasValue && IsChecked.Value) ? 0.0 : 180.0);
				break;
			}
		}
	}
}
