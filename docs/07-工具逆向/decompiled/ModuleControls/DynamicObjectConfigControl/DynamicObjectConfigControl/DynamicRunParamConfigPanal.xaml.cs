using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.BaseData;

namespace DynamicObjectConfigControl;

public partial class DynamicRunParamConfigPanal : Control, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public DialogHandler EditModulesMethod
	{
		get
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Expected Obj, but got Unknown
			return EditRelationModules;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public DynamicRunParamConfigPanal()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}

	private bool? EditRelationModules(object dataSource)
	{
		DynamicRunParamRelationModulesConfigWin dynamicRunParamRelationModulesConfigWin = new DynamicRunParamRelationModulesConfigWin
		{
			Owner = Window.GetWindow(this)
		};
		dynamicRunParamRelationModulesConfigWin.SetBinding(DynamicRunParamRelationModulesConfigWin.CheckClosingMethodProperty, new Binding("CheckClosing")
		{
			Source = DataContext
		});
		dynamicRunParamRelationModulesConfigWin.DataContext = dataSource;
		return dynamicRunParamRelationModulesConfigWin.ShowDialog();
	}
}
