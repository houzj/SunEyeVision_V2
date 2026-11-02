using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

public class NativeTabControl : TabControl
{
	public static readonly DependencyProperty ExpanderDirectionProperty;

	public ExpandDirection ExpanderDirection
	{
		get
		{
			return (ExpandDirection)GetValue(ExpanderDirectionProperty);
		}
		set
		{
			SetValue(ExpanderDirectionProperty, value);
		}
	}

	static NativeTabControl()
	{
		ExpanderDirectionProperty = DependencyProperty.Register("ExpanderDirection", typeof(ExpandDirection), typeof(NativeTabControl), new PropertyMetadata(ExpandDirection.LeftToRight));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeTabControl), new FrameworkPropertyMetadata(typeof(NativeTabControl)));
	}
}
