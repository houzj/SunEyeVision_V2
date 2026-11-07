using System.Windows;
using System.Windows.Controls;

namespace VM.Controls;

public class HikScrollViewer : ScrollViewer
{
	static HikScrollViewer()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HikScrollViewer), new FrameworkPropertyMetadata(typeof(HikScrollViewer)));
	}
}
