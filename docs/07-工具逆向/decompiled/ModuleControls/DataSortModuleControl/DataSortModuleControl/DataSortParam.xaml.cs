using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DataSortModuleControl;

public partial class DataSortParam : Control, IComponentConnector, IStyleConnector
{
	public DataSortParam()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}
}
