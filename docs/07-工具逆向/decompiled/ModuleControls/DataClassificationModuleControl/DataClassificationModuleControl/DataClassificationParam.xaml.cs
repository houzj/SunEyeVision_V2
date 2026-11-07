using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DataClassificationModuleControl;

public partial class DataClassificationParam : Control, IComponentConnector, IStyleConnector
{
	public DataClassificationParam()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}
}
