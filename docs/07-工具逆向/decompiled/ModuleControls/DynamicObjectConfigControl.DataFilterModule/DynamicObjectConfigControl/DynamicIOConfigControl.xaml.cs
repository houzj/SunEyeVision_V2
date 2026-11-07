using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DynamicObjectConfigControl;

public partial class DynamicIOConfigControl : Control, IComponentConnector, IStyleConnector
{
	public DynamicIOConfigControl()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}
}
