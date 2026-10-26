using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System;
using System.Collections;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public partial class ArrowTreeViewControl : UserControl, IComponentConnector
{
	private struct treeViewItemInfo(object d, ItemsControl p)
	{
		public object data = d;

		public ItemsControl parent = p;
	}

	public static readonly DependencyProperty ScrollViewerStyleProperty = DependencyProperty.Register("ScrollViewerStyle", typeof(Style), typeof(ArrowTreeViewControl), new PropertyMetadata(null));

	private Grid ViewGrid;

	private Hashtable hashTable = new Hashtable();

	private Hashtable mapHashTable = new Hashtable();

	private int needGenerateCount;

	private int alreadyGenerateCount;

	private AutoResetEvent waitGenerator = new AutoResetEvent(initialState: false);

	private AutoResetEvent are = new AutoResetEvent(initialState: false);

	private bool needFlash;

	public Style ScrollViewerStyle
	{
		get
		{
			return (Style)GetValue(ScrollViewerStyleProperty);
		}
		set
		{
			SetValue(ScrollViewerStyleProperty, value);
		}
	}

	public object SelectedItem => treeView.SelectedItem;

	public TreeView InnerTree => treeView;

	public ArrowTreeViewControl()
	{
		InitializeComponent();
	}

	private TreeViewItem GetContainerFromNode(object node)
	{
		return hashTable[node] as TreeViewItem;
	}

	private ArrowLineH GetArrowContainerFromNode(object node)
	{
		return mapHashTable[node] as ArrowLineH;
	}

	private void treeView_Loaded(object sender, RoutedEventArgs e)
	{
		ViewGrid = TheGrid;
		treeView.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
	}

	private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
	{
		if (treeView.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
		{
			return;
		}
		SubContainersGenerate(treeView);
		Dispatcher.BeginInvoke((Action)(() =>
		{
			foreach (ArrowLineH value in mapHashTable.Values)
			{
				if (FreshLine(value) < 0)
				{
					break;
				}
			}
		}));
	}

	private void IniteLineData(ArrowLineH line, object mapNode)
	{
		if (mapNode != null)
		{
			line.BindingDataContext(mapNode);
			line.Tag = mapNode;
			if (!mapHashTable.Contains(mapNode))
			{
				mapHashTable.Add(mapNode, line);
			}
		}
	}

	private ArrowLineH DrawLine()
	{
		ArrowLineH arrowLineH = new ArrowLineH();
		ViewGrid.Children.Add(arrowLineH);
		arrowLineH.Focus();
		return arrowLineH;
	}

	private int FreshLine(ArrowLineH line)
	{
		Point point = default;
		Point point2 = default;
		if (line == null)
		{
			return -1;
		}
		TreeViewItem shownItem = GetShownItem(GetContainerByItem(line.StartObj));
		TreeViewItem shownItem2 = GetShownItem(GetContainerByItem(line.EndObj));
		if (shownItem == null || shownItem2 == null)
		{
			return -1;
		}
		double width = shownItem.DesiredSize.Width;
		double height = shownItem.DesiredSize.Height;
		try
		{
			string text = shownItem2.ToString();
			if (text.IndexOf("Disconnect") >= 0)
			{
				return -1;
			}
			point = shownItem.TransformToVisual(ViewGrid).Transform(new Point(0.0, 0.0));
		}
		catch
		{
			return -1;
		}
		double width2 = shownItem2.DesiredSize.Width;
		double height2 = shownItem2.DesiredSize.Height;
		try
		{
			string text2 = shownItem2.ToString();
			if (text2.IndexOf("Disconnect") >= 0)
			{
				return -1;
			}
			point2 = shownItem2.TransformToVisual(ViewGrid).Transform(new Point(0.0, 0.0));
		}
		catch
		{
			return -1;
		}
		double num = 0.0;
		if (point.Y > point2.Y)
		{
			double num2 = Math.Abs(FindTreeViewMaxWidth(treeView, shownItem2, shownItem, 0.0));
			num = point2.X + num2;
		}
		else
		{
			double num2 = Math.Abs(FindTreeViewMaxWidth(treeView, shownItem, shownItem2, 0.0));
			num = point.X + num2;
		}
		line.StartPoint = new Point(point.X + width, point.Y + height / 2.0);
		line.EndPoint = new Point(point2.X + width2, point2.Y + height2 / 2.0);
		line.MidStartPoint = new Point(num + 38.0, point.Y + height / 2.0);
		line.MidEndPoint = new Point(num + 38.0, point2.Y + height2 / 2.0);
		line.BaseX = line.MidStartPoint.X;
		line.LineSpan = Math.Abs(point2.Y - point.Y);
		return 0;
	}

	private void SubContainersGenerate(ItemsControl parentContainer)
	{
		foreach (object item in (IEnumerable)parentContainer.Items)
		{
			TreeViewItem currentContainer = parentContainer.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
			if (currentContainer != null)
			{
				currentContainer.Tag = new treeViewItemInfo(item, parentContainer);
			}
			if (!hashTable.Contains(item))
			{
				hashTable.Add(item, currentContainer);
			}
			if (currentContainer == null || currentContainer.Items.Count <= 0)
			{
				continue;
			}
			currentContainer.IsExpanded = true;
			if (currentContainer.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
			{
				EventHandler eh = null;
				eh = delegate
				{
					if (currentContainer.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
					{
						SubContainersGenerate(currentContainer);
						alreadyGenerateCount++;
						currentContainer.ItemContainerGenerator.StatusChanged -= eh;
					}
				};
				currentContainer.ItemContainerGenerator.StatusChanged += eh;
				needGenerateCount++;
			}
			else
			{
				SubContainersGenerate(currentContainer);
			}
		}
	}

	private void treeView_LayoutUpdated(object sender, EventArgs e)
	{
		if (needFlash)
		{
			are.Set();
			needFlash = false;
		}
	}

	private TreeViewItem GetShownItem(TreeViewItem treeViewItem)
	{
		if (treeViewItem != null)
		{
			for (TreeViewItem treeViewItem2 = ((treeViewItemInfo)treeViewItem.Tag).parent as TreeViewItem; treeViewItem2 != null; treeViewItem2 = ((treeViewItemInfo)treeViewItem2.Tag).parent as TreeViewItem)
			{
				if (!treeViewItem2.IsExpanded)
				{
					treeViewItem = treeViewItem2;
				}
			}
		}
		return treeViewItem;
	}

	private TreeViewItem GetContainerByItem(object data)
	{
		TreeViewItem result = null;
		if (data != null)
		{
			result = hashTable[data] as TreeViewItem;
		}
		return result;
	}

	private void RemoveLine(object data)
	{
		ArrowLineH arrowLineH = ((!(data is ArrowLineH)) ? GetArrowContainerFromNode(data) : (data as ArrowLineH));
		if (arrowLineH != null)
		{
			ViewGrid.Children.Remove(arrowLineH);
			mapHashTable.Remove(arrowLineH.Tag);
		}
	}

	private double FindTreeViewMaxWidth(ItemsControl container, TreeViewItem startItem, TreeViewItem endItem, double maxWidth)
	{
		if (container == null || endItem == null || startItem == null)
		{
			return maxWidth;
		}
		int count = container.Items.Count;
		for (int i = 0; i < count; i++)
		{
			TreeViewItem treeViewItem = (TreeViewItem)container.ItemContainerGenerator.ContainerFromIndex(i);
			if (treeViewItem == null)
			{
				continue;
			}
			if (treeViewItem == startItem)
			{
				maxWidth = treeViewItem.DesiredSize.Width;
			}
			if (treeViewItem == endItem)
			{
				if (maxWidth < treeViewItem.DesiredSize.Width)
				{
					maxWidth = treeViewItem.DesiredSize.Width;
				}
				maxWidth = 0.0 - maxWidth;
				break;
			}
			if (maxWidth < treeViewItem.DesiredSize.Width && maxWidth != 0.0)
			{
				maxWidth = treeViewItem.DesiredSize.Width;
			}
			double num = FindTreeViewMaxWidth(treeViewItem, startItem, endItem, maxWidth);
			if (num < 0.0)
			{
				maxWidth = ((!(maxWidth < 0.0 - num)) ? (0.0 - maxWidth) : num);
				break;
			}
			if (maxWidth < num)
			{
				maxWidth = num;
			}
		}
		return maxWidth;
	}

	private void TheTreeView_GotFocus(object sender, RoutedEventArgs e)
	{
		if (!(e.OriginalSource is TreeViewItem treeViewItem))
		{
			return;
		}
		foreach (object child in ViewGrid.Children)
		{
			if (child is ArrowLineH && (child as ArrowLineH).StartObj == ((treeViewItemInfo)treeViewItem.Tag).data)
			{
				(child as ArrowLineH).Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF67C2A"));
				(child as ArrowLineH).SetValue(Panel.ZIndexProperty, 99);
			}
		}
	}

	private void TheTreeView_LostFocus(object sender, RoutedEventArgs e)
	{
		if (!(e.OriginalSource is TreeViewItem treeViewItem))
		{
			return;
		}
		foreach (object child in ViewGrid.Children)
		{
			if (child is ArrowLine && (child as ArrowLineH).StartObj == ((treeViewItemInfo)treeViewItem.Tag).data)
			{
				(child as ArrowLineH).Stroke = Brushes.Gray;
				(child as ArrowLineH).SetValue(Panel.ZIndexProperty, 0);
			}
		}
	}

	private void treeView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		hashTable.Clear();
		mapHashTable.Clear();
	}

	private void PathsItemControl_OnPathCountChange(object itemInfo, IList newItem, IList oldItem)
	{
		if (newItem != null)
		{
			foreach (object item in newItem)
			{
				ArrowLineH line = DrawLine();
				IniteLineData(line, item);
			}
		}
		if (oldItem == null)
		{
			return;
		}
		foreach (object item2 in oldItem)
		{
			RemoveLine(item2);
		}
	}
}
