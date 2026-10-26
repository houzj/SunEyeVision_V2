using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public static class DataGridExtension
{
	public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached("Columns", typeof(ObservableCollection<DataGridColumn>), typeof(DataGridExtension), new UIPropertyMetadata(new ObservableCollection<DataGridColumn>(), OnDataGridColumnsPropertyChanged));

	public static ObservableCollection<DataGridColumn> GetColumns(DependencyObject obj)
	{
		return (ObservableCollection<DataGridColumn>)obj.GetValue(ColumnsProperty);
	}

	public static void SetColumns(DependencyObject obj, ObservableCollection<DataGridColumn> value)
	{
		obj.SetValue(ColumnsProperty, value);
	}

	private static void OnDataGridColumnsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d.GetType() == typeof(DataGrid)))
		{
			return;
		}
		DataGrid myGrid = d as DataGrid;
		ObservableCollection<DataGridColumn> observableCollection = (ObservableCollection<DataGridColumn>)e.NewValue;
		if (observableCollection == null)
		{
			return;
		}
		myGrid.Columns.Clear();
		if (observableCollection != null && observableCollection.Count > 0)
		{
			foreach (DataGridColumn item in observableCollection)
			{
				myGrid.Columns.Add(item);
			}
		}
		observableCollection.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs args) =>
		{
			if (args.OldItems != null)
			{
				foreach (DataGridColumn item2 in args.OldItems.Cast<DataGridColumn>())
				{
					myGrid.Columns.Remove(item2);
				}
			}
			if (args.NewItems != null)
			{
				foreach (DataGridColumn item3 in args.NewItems.Cast<DataGridColumn>())
				{
					myGrid.Columns.Add(item3);
				}
			}
		};
	}
}
