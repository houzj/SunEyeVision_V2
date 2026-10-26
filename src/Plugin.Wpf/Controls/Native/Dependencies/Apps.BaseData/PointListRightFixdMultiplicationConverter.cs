using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;

public class PointListRightFixdMultiplicationConverter : IMultiValueConverter
{
	private IList<Point> left;

	private ObservableCollection<Point> newList;

	private double fixedWidth;

	private double fixedHeight;

	private bool isChanging_newList;

	private bool isChanging_left;

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue || !(values[0] is IList<Point>) || values[1] == DependencyProperty.UnsetValue || values[2] == DependencyProperty.UnsetValue)
		{
			return 0;
		}
		IList<Point> list = values[0] as IList<Point>;
		fixedWidth = System.Convert.ToDouble(values[1]);
		if (fixedWidth == 0.0)
		{
			return double.NaN;
		}
		fixedHeight = System.Convert.ToDouble(values[2]);
		if (fixedHeight == 0.0)
		{
			return double.NaN;
		}
		if (left == null && left != list)
		{
			newList = new ObservableCollection<Point>();
			left = list;
			for (int i = 0; i < left.Count; i++)
			{
				newList.Add(new Point(left[i].X * fixedWidth, left[i].Y * fixedHeight));
			}
			newList.CollectionChanged += NewList_CollectionChanged;
			CollectionChangedEventManager.AddHandler(left as INotifyCollectionChanged, leftCollectionChanged);
		}
		else
		{
			for (int j = 0; j < left.Count; j++)
			{
				newList[j] = new Point(left[j].X * fixedWidth, left[j].Y * fixedHeight);
			}
		}
		return newList;
	}

	private void leftCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		NotifyCollectionChangedAction action = e.Action;
		if (action == NotifyCollectionChangedAction.Replace)
		{
			isChanging_left = true;
			if (!isChanging_newList)
			{
				Point point = (Point)e.NewItems[0];
				newList[e.OldStartingIndex] = new Point(point.X * fixedWidth, point.Y * fixedHeight);
				isChanging_newList = false;
			}
			isChanging_left = false;
		}
	}

	private void NewList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		NotifyCollectionChangedAction action = e.Action;
		if (action == NotifyCollectionChangedAction.Replace)
		{
			isChanging_newList = true;
			if (!isChanging_left)
			{
				Point point = (Point)e.NewItems[0];
				left[e.OldStartingIndex] = new Point(point.X / fixedWidth, point.Y / fixedHeight);
				isChanging_newList = false;
			}
		}
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new Exception();
	}
}
