using System.Windows;
using System.Windows.Controls;

namespace VM.Controls;

public class HikMenuItem : MenuItem
{
	static HikMenuItem()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HikMenuItem), new FrameworkPropertyMetadata(typeof(HikMenuItem)));
	}
}
