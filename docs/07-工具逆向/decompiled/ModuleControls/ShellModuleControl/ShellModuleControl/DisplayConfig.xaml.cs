using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace ShellModuleControl;

public partial class DisplayConfig : UserControl, IComponentConnector
{
	public DisplayConfig()
	{
		InitializeComponent();
	}

	private void listDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is ItemsControl current)
		{
			DependencyObject parent = LogicalTreeHelper.GetParent(LogicalTreeHelper.GetParent(current));
			if (parent is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollToEnd();
			}
		}
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
}
