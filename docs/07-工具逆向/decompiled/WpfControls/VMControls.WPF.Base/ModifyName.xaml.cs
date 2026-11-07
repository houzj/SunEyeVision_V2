using System.Windows;
using System.Windows.Markup;
using VM.Controls;
using VMControls.WPF.DataForUI;

namespace VMControls.WPF.Base;

/// <summary>
/// ModifyName.xaml 的交互逻辑
/// </summary>
/// <summary>
/// ModifyName
/// </summary>
public partial class ModifyName : BaseWindow, IComponentConnector
{
	public ModifyName()
	{
		InitializeComponent();
	}

	public ModifyName(VmModuleBaseDecorator decorator)
	{
		InitializeComponent();
		if (decorator is VmModuleDecorator dataContext)
		{
			DataContext = dataContext;
		}
	}

	private void SearchClick(object sender, RoutedEventArgs e)
	{
		DataContext = null;
		Close();
	}
}
