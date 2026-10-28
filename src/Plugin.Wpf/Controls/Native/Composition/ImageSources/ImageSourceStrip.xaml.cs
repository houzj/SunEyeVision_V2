using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources;

/// <summary>Recovered local-image composition. Owns only view state and cancellable requests, never execution or configuration.</summary>
public partial class ImageSourceStrip : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(nameof(Session),
        typeof(IImageSourceStripSession), typeof(ImageSourceStrip), new PropertyMetadata(null, SessionChanged));
    public IImageSourceStripSession? Session { get => (IImageSourceStripSession?)GetValue(SessionProperty); set => SetValue(SessionProperty, value); }
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded),
        typeof(bool), typeof(ImageSourceStrip), new PropertyMetadata(true, ExpandedChanged));
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    private IImageSourceStripSession? _attached;
    private CancellationTokenSource? _lifetime;
    private bool _refreshing;
    private string? _requestError;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<ImageSourceStripItem> Items => Session?.Items ?? Array.Empty<ImageSourceStripItem>();
    public int DisplayIndex => Session?.DisplayIndex ?? -1;
    public int DisplayNumber => DisplayIndex < 0 ? 0 : DisplayIndex + 1;
    public int ImageCount => Items.Count;
    public Visibility FirstVisibility => IsExpanded && Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public bool IsReadOnly => Session?.IsReadOnly ?? true;
    public bool IsSelectMode => Session?.IsSelectMode ?? false;
    public int ModeIndex => IsSelectMode ? 1 : 0;
    public bool AutoSwitchEnabled => Session?.AutoSwitchEnabled ?? false;
    public bool IsImporting => Session?.IsImporting ?? false;
    public bool CanInteract => Session is not null && !IsReadOnly && !IsImporting;
    public bool IsFirstConfigured => Session is not null && Session.ConfiguredStartIndex == FirstEligibleIndex();
    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public string? Status => _requestError ?? Session?.Status;
    public ImageSourceStrip() { InitializeComponent(); }
    private static void SessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var view = (ImageSourceStrip)d; view.Detach(); if (view.IsLoaded) view.Attach(); view.Refresh(); }
    private static void ExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var view = (ImageSourceStrip)d; view.Height = (bool)e.NewValue ? 125 : 40; view.Refresh(); }
    private void OnLoaded(object sender, RoutedEventArgs e) { Attach(); Refresh(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();
    private void Attach()
    {
        if (_lifetime is not null) return;
        _lifetime = new CancellationTokenSource(); _attached = Session;
        if (_attached is not null) _attached.PropertyChanged += SessionPropertyChanged;
    }
    private void Detach()
    {
        if (_attached is not null) _attached.PropertyChanged -= SessionPropertyChanged;
        _attached = null;
        var old = _lifetime; _lifetime = null; old?.Cancel(); old?.Dispose();
        if (Pop1 is not null) Pop1.IsOpen = false;
        if (Pop2 is not null) Pop2.IsOpen = false;
        _requestError = null;
    }
    private void SessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _attached)) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => { if (ReferenceEquals(sender, _attached)) Refresh(); })); return; }
        Refresh();
    }
    private void Refresh()
    {
        if (!IsInitialized) return;
        _refreshing = true;
        try
        {
            foreach (var name in new[] { nameof(Items), nameof(DisplayIndex), nameof(DisplayNumber), nameof(ImageCount), nameof(IsReadOnly),
                nameof(IsSelectMode), nameof(ModeIndex), nameof(AutoSwitchEnabled), nameof(IsImporting), nameof(CanInteract), nameof(IsFirstConfigured), nameof(ExpandedVisibility), nameof(FirstVisibility), nameof(Status) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            // One-way restoration is never an execution request.
            lst.SelectedIndex = DisplayIndex;
            Mode.SelectedIndex = ModeIndex;
            AutoSwitch.IsChecked = AutoSwitchEnabled;
            if (DisplayIndex >= 0 && DisplayIndex < Items.Count) lst.ScrollIntoView(Items[DisplayIndex]);
            lst.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            RestoreRunChecks(lst);
        }
        finally { _refreshing = false; }
    }
    private static void RestoreRunChecks(DependencyObject parent)
    {
        if (parent is CheckBox check) check.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) RestoreRunChecks(VisualTreeHelper.GetChild(parent, i));
    }
    private int FirstEligibleIndex()
    { for (int i = 0; i < Items.Count; i++) if (!IsSelectMode || Items[i].IsForRun) return i; return -1; }
    private ImageSourceStripItem? Item(object sender) => (sender as FrameworkElement)?.DataContext as ImageSourceStripItem;
    private async Task Request(ImageSourceStripRequestKind kind, string? id = null, bool? flag = null,
        IEnumerable<string>? itemIds = null, IEnumerable<string>? paths = null)
    {
        var session = Session; var lifetime = _lifetime;
        if (session is null || lifetime is null || lifetime.IsCancellationRequested || _refreshing) return;
        if ((session.IsReadOnly || session.IsImporting) && kind != ImageSourceStripRequestKind.CancelImport) return;
        var request = new ImageSourceStripRequest(kind, session.Version, id, flag, itemIds, paths);
        try { _requestError = null; await session.RequestAsync(request, lifetime.Token); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        { if (ReferenceEquals(session, Session) && ReferenceEquals(lifetime, _lifetime)) _requestError = ex.Message; }
        finally
        { if (ReferenceEquals(session, Session) && ReferenceEquals(lifetime, _lifetime)) Refresh(); }
    }
    private async void Grid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Item(sender) is not { } item || !CanInteract) return;
        e.Handled = true;
        lst.Focus();
        if (IsSelectMode && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            var selected = Items.Where(x => x.IsForRun).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            if (!selected.Remove(item.Id)) selected.Add(item.Id);
            await Request(ImageSourceStripRequestKind.SetRunSelection, itemIds: Items.Where(x => selected.Contains(x.Id)).Select(x => x.Id));
        }
        else if (IsSelectMode && (Keyboard.Modifiers & ModifierKeys.Shift) != 0 && DisplayIndex >= 0)
        {
            int end = Items.ToList().FindIndex(x => x.Id == item.Id);
            if (end < 0 || end == DisplayIndex) return;
            int first = Math.Min(DisplayIndex, end), last = Math.Max(DisplayIndex, end);
            await Request(ImageSourceStripRequestKind.SetRunSelection, itemIds: Items.Skip(first).Take(last - first + 1).Select(x => x.Id));
        }
        else await Request(ImageSourceStripRequestKind.Activate, item.Id);
    }
    private async void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Item(sender) is not { } item) return;
        bool selected = (sender as CheckBox)?.IsChecked == true;
        await Request(ImageSourceStripRequestKind.SetRunSelection, itemIds: Items.Where(x => x.Id == item.Id ? selected : x.IsForRun).Select(x => x.Id));
    }
    private async void FirstImage_Click(object sender, RoutedEventArgs e)
    {
        int first = FirstEligibleIndex(); if (first < 0) return;
        await Request(ImageSourceStripRequestKind.SelectStart, Items[first].Id);
        FindScrollViewer(lst)?.ScrollToLeftEnd();
    }
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    { e.Handled = true; if (Item(sender) is { } item) await Request(ImageSourceStripRequestKind.Remove, item.Id); }
    private async void BtnImageClear_Click(object sender, RoutedEventArgs e) => await Request(ImageSourceStripRequestKind.Clear);
    private void BtnImageAdd_Click(object sender, RoutedEventArgs e) { Pop2.IsOpen = false; Pop1.IsOpen = !Pop1.IsOpen; }
    private void BtnImageAddFolder_Click(object sender, RoutedEventArgs e) { Pop1.IsOpen = false; Pop2.IsOpen = !Pop2.IsOpen; }
    private async void LocalImage_Click(object sender, RoutedEventArgs e) { Pop1.IsOpen = false; await Request(ImageSourceStripRequestKind.ImportFiles); }
    private async void LocalImageFolder_Click(object sender, RoutedEventArgs e) { Pop2.IsOpen = false; await Request(ImageSourceStripRequestKind.ImportDirectory); }
    // These original entries are deliberately unavailable until formal source contracts exist.
    private void LinuxImage_Click(object sender, RoutedEventArgs e) { }
    private void LinuxImageFolder_Click(object sender, RoutedEventArgs e) { }
    private void BtnImageAddRpFolder_Click(object sender, RoutedEventArgs e) { }
    private async void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menu && ItemsControl.ItemsControlFromItemContainer(menu) is ContextMenu context
            && context.PlacementTarget is FrameworkElement target && target.DataContext is ImageSourceStripItem item)
            await Request(ImageSourceStripRequestKind.OpenDirectory, item.Id);
    }
    private async void AutoSwitchClick(object sender, RoutedEventArgs e)
    { if (!_refreshing) await Request(ImageSourceStripRequestKind.SetAutoSwitch, flag: AutoSwitch.IsChecked == true); }
    private async void ModeChanged(object sender, SelectionChangedEventArgs e)
    { if (!_refreshing && IsLoaded && Mode.SelectedIndex is 0 or 1) await Request(ImageSourceStripRequestKind.SetMode, flag: Mode.SelectedIndex == 1); }
    private async void CancelImportClick(object sender, RoutedEventArgs e) => await Request(ImageSourceStripRequestKind.CancelImport);
    private void Grid_MouseEnter(object sender, MouseEventArgs e)
    { if (sender is Grid grid && grid.FindName("BtnDelete") is Button button) button.Visibility = CanInteract ? Visibility.Visible : Visibility.Collapsed; }
    private void Grid_MouseLeave(object sender, MouseEventArgs e)
    { if (sender is Grid grid && grid.FindName("BtnDelete") is Button button) button.Visibility = Visibility.Collapsed; }
    private void ListDragOver(object sender, DragEventArgs e)
    { e.Effects = CanInteract && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void ListDrop(object sender, DragEventArgs e)
    { e.Handled = true; if (CanInteract && e.Data.GetData(DataFormats.FileDrop) is string[] paths) await Request(ImageSourceStripRequestKind.ImportFiles, paths: paths); }
    private async void ListKeyDown(object sender, KeyEventArgs e)
    {
        if (!CanInteract || Items.Count == 0) return;
        if (e.Key == Key.Escape) { Pop1.IsOpen = Pop2.IsOpen = false; e.Handled = true; return; }
        int index = Math.Clamp(DisplayIndex, 0, Items.Count - 1);
        switch (e.Key)
        {
            case Key.Left: index = Math.Max(0, index - 1); break;
            case Key.Right: index = Math.Min(Items.Count - 1, index + 1); break;
            case Key.Home: index = 0; break;
            case Key.End: index = Items.Count - 1; break;
            case Key.Enter: break;
            case Key.Delete: e.Handled = true; await Request(ImageSourceStripRequestKind.Remove, Items[index].Id); return;
            default: return;
        }
        e.Handled = true;
        await Request(ImageSourceStripRequestKind.Activate, Items[index].Id);
    }
    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(parent, i)) is { } found) return found;
        return null;
    }
}

public sealed class ImageCountVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
