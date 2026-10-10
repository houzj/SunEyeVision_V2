using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationHeartbeatsView : UserControl
{
    public CommunicationHeartbeatsView() => InitializeComponent();
    private void Enabled_Changed(object sender, RoutedPropertyChangedEventArgs<bool> e)
    {
        if (sender is not ToggleSwitch toggle || toggle.DataContext is not CommunicationHeartbeatViewModel row) return;
        if (e.NewValue)
        {
            try
            {
                if (row.DeviceId.Length == 0 || row.IntervalMs < 1) throw new FormatException("请选择目标并设置有效时间间隔。");
                if (!row.TypeResolved) throw new FormatException("节点类型未确认，请连接设备并刷新数据点。");
                CommunicationSendEventFormatter.ParseLiteral(row.ContentA, row.DataType);
                if (row.IsAlternate) CommunicationSendEventFormatter.ParseLiteral(row.ContentB, row.DataType);
                row.Enabled = true;
            }
            catch (Exception ex)
            {
                row.Enabled = false;
                row.Status = "配置无效：" + ex.Message;
                toggle.SetCurrentValue(ToggleSwitch.IsCheckedProperty, false);
            }
        }
        else row.Enabled = false;
        BindingOperations.SetBinding(toggle, ToggleSwitch.IsCheckedProperty,
            new Binding(nameof(CommunicationHeartbeatViewModel.Enabled)) { Source = row, Mode = BindingMode.OneWay });
    }
    private void Targets_Opened(object? sender, EventArgs e)
    {
        (DataContext as CommunicationManagerViewModel)?.RefreshHeartbeatTargets();
        if (sender is not Popup popup) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!popup.IsOpen || FindVisual<TreeView>(popup.Child) is not { DataContext: CommunicationHeartbeatViewModel row } tree)
                return;
            tree.UpdateLayout();
            foreach (var group in row.Targets)
            {
                if (group.DeviceId != row.DeviceId) continue;
                if (tree.ItemContainerGenerator.ContainerFromItem(group) is not TreeViewItem parent) continue;
                if (group.Selectable && group.TargetId == row.TargetId)
                { FocusTarget(parent); break; }
                var leaf = System.Linq.Enumerable.FirstOrDefault(group.Children, n => n.TargetId == row.TargetId);
                if (leaf == null) continue;
                parent.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
                parent.UpdateLayout();
                if (parent.ItemContainerGenerator.ContainerFromItem(leaf) is TreeViewItem child)
                    FocusTarget(child);
                break;
            }
        }));
    }
    private static void FocusTarget(TreeViewItem item)
    {
        item.SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
        item.BringIntoView();
        item.Focus(); // Navigation is not a configuration commit.
    }
    private static T? FindVisual<T>(DependencyObject? node) where T : DependencyObject
    {
        if (node is T found) return found;
        if (node is not Visual) return null;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindVisual<T>(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }
    private static TreeViewItem? FindTargetItem(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is ScrollBar) return null;
            if (node is TreeViewItem item) return item;
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }
    private void Target_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeView tree || FindTargetItem(e.OriginalSource as DependencyObject) is not { } item) return;
        // Own both the row and arrow once; suppress default ToggleButton and double-click handling.
        e.Handled = true;
        if (e.ClickCount > 1) return;
        FocusTarget(item);
        if (item.HasItems)
            item.SetCurrentValue(TreeViewItem.IsExpandedProperty, !item.IsExpanded);
        else CommitTarget(tree, item.Header as HeartbeatTargetNode);
    }
    private void Target_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FindTargetItem(e.OriginalSource as DependencyObject) != null) e.Handled = true;
    }
    private void Target_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TreeView { DataContext: CommunicationHeartbeatViewModel row } tree) return;
        if (e.Key == Key.Escape)
        {
            row.IsTargetOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && tree.SelectedItem is HeartbeatTargetNode target)
        {
            e.Handled = true;
            if (target.Selectable) CommitTarget(tree, target);
            else if (FindTargetItem(Keyboard.FocusedElement as DependencyObject) is { } item)
                item.SetCurrentValue(TreeViewItem.IsExpandedProperty, !item.IsExpanded);
        }
        // Native tree navigation handles arrows without modifying the bound sending target.
    }
    private void CommitTarget(TreeView tree, HeartbeatTargetNode? target)
    {
        if (tree.DataContext is CommunicationHeartbeatViewModel row && target is { Selectable: true } &&
            DataContext is CommunicationManagerViewModel manager)
            manager.SelectHeartbeatTarget(row, target);
    }
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommunicationHeartbeatViewModel row } &&
            DataContext is CommunicationManagerViewModel manager &&
            ThemeMessageDialog.Show("删除此心跳配置并停止发送？", "心跳管理",
                MessageBoxButton.YesNo, ThemeMessageDialog.MessageIcon.Question, Window.GetWindow(this)) == MessageBoxResult.Yes)
            manager.RemoveHeartbeat(row);
    }
}
