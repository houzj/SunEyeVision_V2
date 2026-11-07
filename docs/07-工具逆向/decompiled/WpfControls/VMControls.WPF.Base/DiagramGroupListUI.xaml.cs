using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace VMControls.WPF.Base;

/// <summary>
/// DiagramGroupListUI.xaml 的交互逻辑
/// </summary>
/// <summary>
/// DiagramGroupListUI
/// </summary>
public partial class DiagramGroupListUI : ComboBox, IComponentConnector, IStyleConnector
{
	public DiagramGroupListUI()
	{
		InitializeComponent();
	}

	private void Gd_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		IsDropDownOpen = false;
	}
}
