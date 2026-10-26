using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class HikTabControl : TabControl
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

	static HikTabControl()
	{
		ExpanderDirectionProperty = DependencyProperty.Register("ExpanderDirection", typeof(ExpandDirection), typeof(HikTabControl), new PropertyMetadata(ExpandDirection.LeftToRight));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HikTabControl), new FrameworkPropertyMetadata(typeof(HikTabControl)));
	}
}
