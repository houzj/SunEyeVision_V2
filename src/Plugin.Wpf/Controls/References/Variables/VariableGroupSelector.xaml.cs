using SunEyeVision.Plugin.Wpf.Interop.Windows;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

public partial class VariableGroupSelector : ComboBox, IComponentConnector
{
	public delegate void TreeNodeSingleClick(object sender, ObjectEventArgs e);

	public static readonly DependencyProperty ToggleButtonStyleProperty;

	public static readonly DependencyProperty ExternSelectedItemTemplateProperty;

	public static readonly DependencyProperty ExternSelectedItemProperty;

	public Style ToggleButtonStyle
	{
		get
		{
			return (Style)GetValue(ToggleButtonStyleProperty);
		}
		set
		{
			SetValue(ToggleButtonStyleProperty, value);
		}
	}

	public DataTemplate ExternSelectedItemTemplate
	{
		get
		{
			return (DataTemplate)GetValue(ExternSelectedItemTemplateProperty);
		}
		set
		{
			SetValue(ExternSelectedItemTemplateProperty, value);
		}
	}

	public object ExternSelectedItem
	{
		get
		{
			return GetValue(ExternSelectedItemProperty);
		}
		set
		{
			SetValue(ExternSelectedItemProperty, value);
		}
	}

	public event TreeNodeSingleClick OnTreeNodeSingleClick;

	private static void OnExternSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		VariableGroupSelector treeViewCombobox = d as VariableGroupSelector;
		treeViewCombobox.RaiseEvent(new SelectionChangedEventArgs(Selector.SelectedEvent, new List<object> { e.OldValue }, new List<object> { e.NewValue }));
	}

	static VariableGroupSelector()
	{
		ToggleButtonStyleProperty = DependencyProperty.Register("ToggleButtonStyle", typeof(Style), typeof(VariableGroupSelector), new PropertyMetadata(null));
		ExternSelectedItemTemplateProperty = DependencyProperty.Register("ExternSelectedItemTemplate", typeof(DataTemplate), typeof(VariableGroupSelector), new PropertyMetadata(null));
		ExternSelectedItemProperty = DependencyProperty.Register("ExternSelectedItem", typeof(object), typeof(VariableGroupSelector), new PropertyMetadata(null, OnExternSelectedItemChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(VariableGroupSelector), new FrameworkPropertyMetadata(typeof(VariableGroupSelector)));
	}

	public VariableGroupSelector()
	{
		InitializeComponent();
	}

	internal void NotifyComboBoxItemMouseDown(TreeView treeView, TreeViewItem treeViewItem)
	{
		IsDropDownOpen = false;
		ExternSelectedItem = treeViewItem.DataContext;
	}

	internal void SingleClickCommand(TreeView treeView, TreeViewItem treeViewItem, object sender)
	{
		if (OnTreeNodeSingleClick != null)
		{
			object dataContext = treeViewItem.DataContext;
			ObjectEventArgs e = new ObjectEventArgs
			{
				Param = dataContext
			};
			OnTreeNodeSingleClick(sender, e);
			IsDropDownOpen = false;
			ExternSelectedItem = treeViewItem.DataContext;
		}
	}
}
