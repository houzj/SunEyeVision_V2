using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DataAnalysisModuleControl;

public partial class ProtocolOut : UserControl, IComponentConnector, IStyleConnector
{
	public ProtocolOut()
	{
		InitializeComponent();
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
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

	private void TxtName_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space && sender != null && sender is TextBox)
		{
			e.Handled = true;
		}
	}
}
