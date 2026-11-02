using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

public class NativeContextMenu : ContextMenu
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

	static NativeContextMenu()
	{
		OpenCommandProperty = DependencyProperty.Register("OpenCommand", typeof(ICommand), typeof(NativeContextMenu), new PropertyMetadata((object)null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeContextMenu), new FrameworkPropertyMetadata(typeof(NativeContextMenu)));
	}

	public NativeContextMenu()
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
