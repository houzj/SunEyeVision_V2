using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Log;

namespace VisionMasterMain.AppMainWindow;

public partial class TitleBar : Control, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty IsButtonClickEnabledProperty = DependencyProperty.Register("IsButtonClickEnabled", typeof(bool), typeof(TitleBar), new PropertyMetadata(true));

	public bool IsButtonClickEnabled
	{
		get
		{
			return (bool)GetValue(IsButtonClickEnabledProperty);
		}
		set
		{
			SetValue(IsButtonClickEnabledProperty, value);
		}
	}

	public TitleBar()
	{
		InitializeComponent();
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableWidth, scrollViewer.HorizontalOffset + num) : Math.Max(0.0, scrollViewer.HorizontalOffset - num));
			if (num2 != scrollViewer.HorizontalOffset)
			{
				scrollViewer.ScrollToHorizontalOffset(num2);
				e.Handled = true;
			}
		}
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		LogHelper.Info("用户点击关闭窗口按钮");
	}

	private void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
	{
		MainWindow.SeMenuFocus(sender as UIElement);
	}
}
