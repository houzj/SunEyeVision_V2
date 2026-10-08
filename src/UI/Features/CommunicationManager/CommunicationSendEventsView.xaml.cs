using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationSendEventsView : UserControl
{
    private bool _scrollPending;
    public CommunicationSendEventsView() => InitializeComponent();
    private CommunicationManagerViewModel? Manager => DataContext as CommunicationManagerViewModel;
    private void AddSendEvent_Click(object sender, RoutedEventArgs e)
    {
        if (Manager == null) return;
        var dialog = new CommunicationSendEventDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result != null) Manager.AddSendEvent(dialog.Result);
    }
    private void InputConfig_Click(object sender, RoutedEventArgs e)
    {
        if (Manager?.SelectedSendEvent is { } item) item.IsAssemblyConfigTab = false;
    }
    private void AssemblyConfig_Click(object sender, RoutedEventArgs e)
    {
        if (Manager?.SelectedSendEvent is { } item) item.IsAssemblyConfigTab = true;
    }
    private void Targets_DropDownOpened(object sender, EventArgs e) => Manager?.RefreshSendTargets();
    private void RemoveParameter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommunicationSendParameterViewModel item })
            Manager?.SelectedSendEvent?.Parameters.Remove(item);
    }
    private void RemoveAssembly_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommunicationAssemblyOutputViewModel item })
            Manager?.SelectedSendEvent?.AssemblyOutputs.Remove(item);
    }
    private void AssemblyBinding_Selected(object sender, RoutedEventArgs e) => SyncBinding(sender);
    private void AssemblyBinding_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => SyncBinding(sender);
    private static void SyncBinding(object sender)
    {
        if (sender is ParamBinding { DataContext: CommunicationAssemblyOutputViewModel output } control)
            output.ApplyBindingSelection(control.BindingSource, control.BindingDisplayText);
    }
    private void SendItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item) { item.IsSelected = true; item.Focus(); }
    }
    private static CommunicationSendEventViewModel? ContextEvent(object sender) =>
        sender is MenuItem menuItem && ItemsControl.ItemsControlFromItemContainer(menuItem) is ContextMenu
            { PlacementTarget: FrameworkElement target } ? target.DataContext as CommunicationSendEventViewModel : null;
    private void RenameSendEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ContextEvent(sender) is not { IsBusy: false } item) return;
        var dialog = new CommunicationRenameDialog(item.DisplayName, "发送事件") { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) item.Name = dialog.DeviceName;
    }
    private void DeleteSendEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ContextEvent(sender) is not { IsBusy: false } item) return;
        if (MessageBox.Show(Window.GetWindow(this), $"删除发送事件“{item.DisplayName}”？", "删除发送事件",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) Manager?.RemoveSendEvent(item);
    }
    private void History_TextChanged(object sender, TextChangedEventArgs e) { if (sender is TextBox box) ScrollHistory(box); }
    private void History_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) { if (e.NewValue is true && sender is TextBox box) ScrollHistory(box); }
    private void ScrollHistory(TextBox box)
    {
        if (_scrollPending || !box.IsVisible) return;
        _scrollPending = true;
        box.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        { _scrollPending = false; if (box.IsVisible) { box.UpdateLayout(); box.ScrollToEnd(); } }));
    }
}
