using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Linq;
using SunEyeVision.Core.Versioning;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.Dialogs;

public partial class SolutionConfigurationDialog : Window
{
    private readonly SolutionConfigurationDialogViewModel _viewModel;
    // WindowChrome carries dispatcher/lifetime state: never share it across windows.
    public static System.Windows.Shell.WindowChrome DialogChrome => new()
    {
        CaptionHeight = 64, ResizeBorderThickness = new Thickness(6),
        GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false
    };
    public Solution? LaunchResult => _viewModel.LaunchResult;
    public bool IsLaunched => _viewModel.IsLaunched;
    public SolutionConfigurationDialog(SolutionManager manager, string? preselectSolutionId = null,
        Func<Task<bool>>? beforeTransition = null, Action? synchronizeCurrent = null)
    {
        InitializeComponent();
        _viewModel = new SolutionConfigurationDialogViewModel(manager, preselectSolutionId, beforeTransition, synchronizeCurrent);
        DataContext = _viewModel; _viewModel.SetOwnerWindow(this);
        VersionTextBlock.Text = "v" + AppVersionInfo.Product + (AppVersionInfo.Channel == "stable" ? "" : "_" + AppVersionInfo.Channel);
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    private bool _navigatingRecent;
    private bool _closed;
    private void Cards_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CardList.SelectedItem;
        if (selected == null) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (!_closed && ReferenceEquals(CardList.SelectedItem, selected))
            { CardList.UpdateLayout(); CardList.ScrollIntoView(selected); }
        }));
    }
    private static ListBoxItem? ItemAt(ItemsControl list, DependencyObject? source) =>
        source == null ? null : ItemsControl.ContainerFromElement(list, source) as ListBoxItem;
    private static bool FromButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase || source is TextBoxBase || source is ComboBox) return true;
            source = source is Visual || source is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
    private void Solution_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _viewModel.IsBusy || FromButton(e.OriginalSource as DependencyObject)) return;
        var item = ItemAt(CardList, e.OriginalSource as DependencyObject);
        if (item == null) return;
        item.IsSelected = true;
        if (_viewModel.LaunchCommand.CanExecute(null)) _viewModel.LaunchCommand.Execute(null);
    }
    private void Cards_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsBusy) { e.Handled = true; return; }
        var item = ItemAt(CardList, e.OriginalSource as DependencyObject);
        if (item != null) item.IsSelected = true;
    }
    private void Cards_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_viewModel.IsBusy || ItemAt(CardList, e.OriginalSource as DependencyObject) is not ListBoxItem item)
        { e.Handled = true; return; }
        item.IsSelected = true;
    }
    private void ShowSelectedMenu()
    {
        if (_viewModel.IsBusy || CardList.SelectedItem == null) return;
        CardList.ScrollIntoView(CardList.SelectedItem); CardList.UpdateLayout();
        if (CardList.ItemContainerGenerator.ContainerFromItem(CardList.SelectedItem) is ListBoxItem item && item.ContextMenu != null)
        {
            item.ContextMenu.PlacementTarget = item;
            item.ContextMenu.Placement = PlacementMode.Bottom;
            item.ContextMenu.IsOpen = true;
        }
    }
    private void Cards_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.IsBusy || FromButton(e.OriginalSource as DependencyObject)) return;
        if (e.Key == Key.Apps || e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)
        { ShowSelectedMenu(); e.Handled = true; return; }
        if (e.Key == Key.Enter)
        {
            if (_viewModel.LaunchCommand.CanExecute(null)) _viewModel.LaunchCommand.Execute(null);
            e.Handled = true; return;
        }
        var columns = FindPanel(CardList)?.ColumnCount ?? 1;
        var delta = e.Key switch { Key.Left => -1, Key.Right => 1, Key.Up => -columns, Key.Down => columns, _ => 0 };
        if (CardList.Items.Count == 0 || delta == 0 && e.Key != Key.Home && e.Key != Key.End) return;
        var index = e.Key == Key.Home ? 0 : e.Key == Key.End ? CardList.Items.Count - 1 :
            CardList.SelectedIndex < 0 ? 0 : Math.Clamp(CardList.SelectedIndex + delta, 0, CardList.Items.Count - 1);
        CardList.SelectedIndex = index; CardList.ScrollIntoView(CardList.SelectedItem); CardList.UpdateLayout();
        (CardList.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)?.Focus();
        e.Handled = true;
    }
    private static SolutionCardPanel? FindPanel(DependencyObject parent)
    {
        if (parent is SolutionCardPanel panel) return panel;
        for (var child = 0; child < VisualTreeHelper.GetChildrenCount(parent); child++)
        {
            var found = FindPanel(VisualTreeHelper.GetChild(parent, child));
            if (found != null) return found;
        }
        return null;
    }
    private void Recent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigatingRecent || RecentList.SelectedItem is not SolutionMetadata metadata) return;
        NavigateRecent(metadata);
    }
    private void NavigateRecent(SolutionMetadata metadata)
    {
        _navigatingRecent = true;
        try
        {
            if (!_viewModel.SelectRecent(metadata)) return;
            RecentList.SelectedItem = RecentList.Items.OfType<SolutionMetadata>().FirstOrDefault(m => m.Id == metadata.Id);
            CardList.UpdateLayout(); CardList.ScrollIntoView(_viewModel.SelectedMetadata);
        }
        finally { _navigatingRecent = false; }
    }
    private void Recent_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || ItemAt(RecentList, e.OriginalSource as DependencyObject)?.DataContext is not SolutionMetadata metadata) return;
        NavigateRecent(metadata);
        if (_viewModel.LaunchCommand.CanExecute(null)) _viewModel.LaunchCommand.Execute(null);
    }
    private void Recent_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.IsBusy || RecentList.SelectedItem is not SolutionMetadata metadata) return;
        if (e.Key == Key.Enter)
        { NavigateRecent(metadata); if (_viewModel.LaunchCommand.CanExecute(null)) _viewModel.LaunchCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Apps || e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)
        { NavigateRecent(metadata); ShowSelectedMenu(); e.Handled = true; }
    }
    private void Missing_Locate(object sender, RoutedEventArgs e)
    {
        if (ItemAt(CardList, e.OriginalSource as DependencyObject) is not ListBoxItem item) return;
        item.IsSelected = true;
        if (_viewModel.LocateCommand.CanExecute(null)) _viewModel.LocateCommand.Execute(null);
        e.Handled = true;
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button; button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.IsBusy && !_viewModel.IsLaunched) { e.Cancel = true; return; }
        try { _viewModel.SaveSettings(); }
        catch (Exception ex)
        {
            ThemeMessageDialog.Show("配置保存失败，窗口保持打开：" + ex.Message, "保存配置", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            e.Cancel = true; return;
        }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e) { _closed = true; _viewModel.Dispose(); base.OnClosed(e); }
}
