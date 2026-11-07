using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VM.Controls;

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
