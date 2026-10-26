using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public class PathsItemControl : Control
{
	public delegate void PathCountChange(object itemInfo, IList newItem, IList oldItem);

	public static readonly DependencyProperty ItemInfoProperty = DependencyProperty.Register("ItemInfo", typeof(object), typeof(PathsItemControl), new PropertyMetadata(null));

	public static readonly DependencyProperty PathsProperty = DependencyProperty.Register("Paths", typeof(IList), typeof(PathsItemControl), new PropertyMetadata(null, OnPathsChanged));

	private INotifyCollectionChanged obsPaths;

	public object ItemInfo
	{
		get
		{
			return GetValue(ItemInfoProperty);
		}
		set
		{
			SetValue(ItemInfoProperty, value);
		}
	}

	public IList Paths
	{
		get
		{
			return (IList)GetValue(PathsProperty);
		}
		set
		{
			SetValue(PathsProperty, value);
		}
	}

	public event PathCountChange OnPathCountChange;

	private static void OnPathsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as PathsItemControl).DoPathsChange();
	}

	private void DoPathsChange()
	{
		obsPaths = Paths as INotifyCollectionChanged;
		OnPathCountChange(ItemInfo, Paths, null);
		if (obsPaths != null)
		{
			CollectionChangedEventManager.AddHandler(obsPaths, obsPaths_CollectionChanged);
		}
	}

	private void obsPaths_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (OnPathCountChange != null)
		{
			OnPathCountChange(ItemInfo, e.NewItems, e.OldItems);
		}
	}
}
