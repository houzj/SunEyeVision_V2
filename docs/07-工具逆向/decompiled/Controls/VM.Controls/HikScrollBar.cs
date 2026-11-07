using System.Windows;
using System.Windows.Controls.Primitives;

namespace VM.Controls;

public class HikScrollBar : ScrollBar
{
	static HikScrollBar()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HikScrollBar), new FrameworkPropertyMetadata(typeof(HikScrollBar)));
	}
}
