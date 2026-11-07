using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.XmlParser.ParamTab;

namespace IMVSColorSegmentModuControl;

public partial class UserScrollView : UserControl, IComponentConnector, IStyleConnector
{
	public UserScrollView()
	{
		InitializeComponent();
	}

	private void AddSegmentRange_Click(object sender, RoutedEventArgs e)
	{
		sv.ScrollToRightEnd();
	}

	private void OnMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer)
		{
			if (e.Delta > 0)
			{
				(sender as ScrollViewer).LineLeft();
				(sender as ScrollViewer).LineLeft();
			}
			else
			{
				(sender as ScrollViewer).LineRight();
				(sender as ScrollViewer).LineRight();
			}
		}
		e.Handled = true;
	}

	private void OnMouse_Enter(object sender, MouseEventArgs e)
	{
		(((FrameworkElement)sender).DataContext as RangeInfo).CanDelete = true;
	}

	private void OnMouse_Leave(object sender, MouseEventArgs e)
	{
		if (((FrameworkElement)sender).DataContext is RangeInfo rangeInfo)
		{
			rangeInfo.CanDelete = false;
		}
	}
}
