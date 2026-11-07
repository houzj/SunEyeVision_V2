using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DataFilterModuleControl;

public partial class DataFilterParam : Control, IComponentConnector, IStyleConnector
{
	public DataFilterParam()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}
}
