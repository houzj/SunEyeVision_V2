using System.Windows;
using System.Windows.Controls;

namespace VM.Controls;

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
