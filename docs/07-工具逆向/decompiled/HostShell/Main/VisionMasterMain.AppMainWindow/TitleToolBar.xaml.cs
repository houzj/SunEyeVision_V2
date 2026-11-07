using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace VisionMasterMain.AppMainWindow;

public partial class TitleToolBar : UserControl, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IList), typeof(TitleToolBar), new PropertyMetadata(null));

	public IList ItemsSource
	{
		get
		{
			return (IList)GetValue(ItemsSourceProperty);
		}
		set
		{
			SetValue(ItemsSourceProperty, value);
		}
	}

	public TitleToolBar()
	{
		InitializeComponent();
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		MainWindow.SeMenuFocus(sender as UIElement);
	}
}
