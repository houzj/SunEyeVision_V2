using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class HikContextMenu : ContextMenu
{
	public static readonly DependencyProperty OpenCommandProperty;

	public ICommand OpenCommand
	{
		get
		{
			return (ICommand)GetValue(OpenCommandProperty);
		}
		set
		{
			SetValue(OpenCommandProperty, value);
		}
	}

	static HikContextMenu()
	{
		OpenCommandProperty = DependencyProperty.Register("OpenCommand", typeof(ICommand), typeof(HikContextMenu), new PropertyMetadata((object)null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HikContextMenu), new FrameworkPropertyMetadata(typeof(HikContextMenu)));
	}

	public HikContextMenu()
	{
		Opened += (object s, RoutedEventArgs e) =>
		{
			if (OpenCommand != null)
			{
				OpenCommand.Execute(this);
			}
		};
	}
}
