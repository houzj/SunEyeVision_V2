using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace IMVSImgStitchCalibModuControl;

public partial class ImageSourceListView : UserControl, IComponentConnector
{
	public ImageSourceListView()
	{
		InitializeComponent();
	}

	public void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
				e.Handled = true;
			}
		}
	}
}
