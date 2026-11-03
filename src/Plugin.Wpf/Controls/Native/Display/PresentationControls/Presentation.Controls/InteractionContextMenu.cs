using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public class InteractionContextMenu : ContextMenu
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

	static InteractionContextMenu()
	{
		OpenCommandProperty = DependencyProperty.Register("OpenCommand", typeof(ICommand), typeof(InteractionContextMenu), new PropertyMetadata((object)null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(InteractionContextMenu), new FrameworkPropertyMetadata(typeof(InteractionContextMenu)));
	}

	public InteractionContextMenu()
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
