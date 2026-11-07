using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.BaseData;

namespace DynamicObjectConfigControl;

public partial class GlobalParamConfigPanal : UserControl, IComponentConnector, IStyleConnector
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

	public GlobalParamConfigPanal()
	{
		InitializeComponent();
	}

	private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
	{
		(sender as FrameworkElement).Focus();
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(e.Delta / 2);
			double num2 = 0.0;
			num2 = ((e.Delta <= 0) ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + num) : Math.Max(0.0, scrollViewer.VerticalOffset - num));
			if (num2 != scrollViewer.VerticalOffset)
			{
				scrollViewer.ScrollToVerticalOffset(num2);
				scrollViewer.Focus();
			}
			e.Handled = true;
		}
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
