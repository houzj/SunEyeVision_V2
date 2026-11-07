using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using VM.Controls.Core.Utilities;

namespace GraphicsSetModuleControl;

public partial class DynamicDisplayObjectConfigForGraphicsSetControl : Control, IComponentConnector, IStyleConnector
{
	private ScrollViewer listInputScrollViewer;

	public DynamicDisplayObjectConfigForGraphicsSetControl()
	{
		InitializeComponent();
		Loaded += DynamicDisplayObjectConfigForGraphicsSetControl_Loaded;
	}

	private void DynamicDisplayObjectConfigForGraphicsSetControl_Loaded(object sender, RoutedEventArgs e)
	{
		listInputScrollViewer = this.FindVisualChild<ScrollViewer>("listInputScrollViewer");
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		listInputScrollViewer?.ScrollToEnd();
	}
}
