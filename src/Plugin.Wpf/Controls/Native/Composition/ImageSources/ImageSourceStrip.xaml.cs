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
    private long _keyboardSelectionGeneration;
    private bool _keyboardSelectionPending;
    private long _refreshGeneration;
    private string? _requestError;
    private bool _viewportQueued;
    private string[] _lastViewport = Array.Empty<string>();
    private string? _lastDisplayedId;
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
    public bool IsFirstConfigured => Session is not null && Session.ConfiguredStartIndex == FirstResetIndex();
    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public string? Status => _requestError ?? Session?.Status;
    private sealed record RunModeItem(string DisplayName, bool Value);
    public ImageSourceStrip() { InitializeComponent(); Mode.ItemsSource = new[] { new RunModeItem("运行全部", false), new RunModeItem("运行选中", true) }; }
    private static void SessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ImageSourceStrip)d;
        try { view.Detach(); }
        finally
        {
            // A release callback may already have installed a newer session.
            if (ReferenceEquals(view.Session, e.NewValue))
            { if (view.IsLoaded) view.Attach(); view.Refresh(); }
        }
    }
    private static void ExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var view = (ImageSourceStrip)d; view.Height = (bool)e.NewValue ? 125 : 40; view.Refresh(); }
    private void OnLoaded(object sender, RoutedEventArgs e) { Attach(); Refresh(); QueueViewport(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();
    private void Attach()
    {
        if (_lifetime is not null) return;
        _lifetime = new CancellationTokenSource(); _attached = Session;
        if (_attached is not null) _attached.PropertyChanged += SessionPropertyChanged;
        lst.LayoutUpdated += ViewportLayoutUpdated;
        lst.ItemContainerGenerator.StatusChanged += ViewportLayoutUpdated;
        lst.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ViewportScrolled));
        QueueViewport();
    }
    private void Detach()
    {
        ++_refreshGeneration; ++_keyboardSelectionGeneration; _keyboardSelectionPending = false;
        var attached = _attached; var lifetime = _lifetime;
        _attached = null; _lifetime = null;
        _lastViewport = Array.Empty<string>();
        _lastDisplayedId = null;
        _requestError = null;
        var errors = new List<Exception>();
        void Clean(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
        if (attached is not null) Clean(() => attached.PropertyChanged -= SessionPropertyChanged);
        Clean(() => lst.LayoutUpdated -= ViewportLayoutUpdated);
        Clean(() => lst.ItemContainerGenerator.StatusChanged -= ViewportLayoutUpdated);
        Clean(() => lst.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ViewportScrolled)));
        if (lifetime is not null) { Clean(lifetime.Cancel); Clean(lifetime.Dispose); }
        // Release the captured viewport only after old handlers are gone; a callback
        // may attach a new session and must not be followed by old cleanup.
        if (attached is IImageSourceStripViewportSession viewport && !ReferenceEquals(_attached, attached))
            Clean(() => viewport.UpdateVisibleItems(Array.Empty<string>()));
        if (errors.Count != 0) throw new AggregateException("图像源视图解绑时部分通知失败。", errors);
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
        var generation=++_refreshGeneration;var session=Session;var lifetime=_lifetime;
        bool Current()=>generation==_refreshGeneration&&ReferenceEquals(session,Session)&&ReferenceEquals(lifetime,_lifetime);
        var wasRefreshing=_refreshing;_refreshing = true;
        try
        {
            foreach (var name in new[] { nameof(Items), nameof(DisplayIndex), nameof(DisplayNumber), nameof(ImageCount), nameof(IsReadOnly),
                nameof(IsSelectMode), nameof(ModeIndex), nameof(AutoSwitchEnabled), nameof(IsImporting), nameof(CanInteract), nameof(IsFirstConfigured), nameof(ExpandedVisibility), nameof(FirstVisibility), nameof(Status) })
            {
                if(!Current())return;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
            if(!Current())return;
            // One-way restoration is never an execution request.
            lst.SelectedIndex = DisplayIndex;
            if(!Current())return;
            Mode.SelectedIndex = ModeIndex;
            if(!Current())return;
            AutoSwitch.IsChecked = AutoSwitchEnabled;
            if(!Current())return;
            string? displayedId = DisplayIndex >= 0 && DisplayIndex < Items.Count ? Items[DisplayIndex].Id : null;
            if (displayedId is not null && displayedId != _lastDisplayedId) lst.ScrollIntoView(Items[DisplayIndex]);
            if(!Current())return;
            _lastDisplayedId = displayedId;
            lst.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            if(!Current())return;
            RestoreRunChecks(lst,Current);
            if(!Current())return;
            QueueViewport();
        }
        finally { _refreshing = wasRefreshing; }
    }
    private static void RestoreRunChecks(DependencyObject parent,Func<bool> current)
    {
        if(!current())return;
        if (parent is CheckBox check) check.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
        for (int i = 0; current() && i < VisualTreeHelper.GetChildrenCount(parent); i++) RestoreRunChecks(VisualTreeHelper.GetChild(parent, i),current);
    }
    private void ViewportLayoutUpdated(object? sender, EventArgs e) => QueueViewport();
    private void ViewportScrolled(object sender, ScrollChangedEventArgs e) => QueueViewport();
    private void QueueViewport()
    {
        if (!IsLoaded || _lifetime is null || _viewportQueued) return;
        _viewportQueued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            _viewportQueued = false;
            if (!IsLoaded || _lifetime is null || Session is not IImageSourceStripViewportSession viewport) return;
            var ids = new List<string>();
            if (IsExpanded && lst.IsVisible && lst.ActualWidth > 0 && lst.ActualHeight > 0)
            {
                var bounds = new Rect(0, 0, lst.ActualWidth, lst.ActualHeight);
                for (int i = 0; i < lst.Items.Count && ids.Count < 128; i++)
                    if (lst.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container && container.IsVisible
                        && container.ActualWidth > 0 && container.ActualHeight > 0)
                    {
                        var itemBounds = container.TransformToAncestor(lst).TransformBounds(new Rect(container.RenderSize));
                        if (bounds.IntersectsWith(itemBounds) && lst.Items[i] is ImageSourceStripItem item) ids.Add(item.Id);
                    }
            }
            var snapshot = ids.ToArray();
            if (_lastViewport.SequenceEqual(snapshot, StringComparer.Ordinal)) return;
            _lastViewport = snapshot;
            viewport.UpdateVisibleItems(Array.AsReadOnly(snapshot));
        }));
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
        else if ((lst.SelectedItem as ImageSourceStripItem)?.Id != item.Id) await Request(ImageSourceStripRequestKind.Activate, item.Id);
    }
    private async void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Item(sender) is not { } item) return;
        bool selected = (sender as CheckBox)?.IsChecked == true;
        await Request(ImageSourceStripRequestKind.SetRunSelection, itemIds: Items.Where(x => x.Id == item.Id ? selected : x.IsForRun).Select(x => x.Id));
    }
    private async void FirstImage_Click(object sender, RoutedEventArgs e)=>await SelectFirstAsync();
    private int FirstResetIndex()
    {
        if(!AutoSwitchEnabled)return FirstEligibleIndex();
        for(var index=Items.Count-1;index>=0;index--)
            if(!IsSelectMode||Items[index].IsForRun)return index;
        return -1;
    }
    private async Task SelectFirstAsync()
    {
        int first = FirstResetIndex(); if (first < 0) return;
        var session = Session; var lifetime = _lifetime; var id = Items[first].Id;
        await Request(ImageSourceStripRequestKind.SelectStart, id);
        if (session is null || lifetime is null || lifetime.IsCancellationRequested
            || !ReferenceEquals(session, Session) || !ReferenceEquals(lifetime, _lifetime)) return;
        var configured = session.ConfiguredStartIndex;
        if (configured >= 0 && configured < session.Items.Count && session.Items[configured].Id == id)
            FindScrollViewer(lst)?.ScrollToLeftEnd();
    }
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    { e.Handled = true; if (Item(sender) is { } item) await Request(ImageSourceStripRequestKind.Remove, item.Id); }
    private async void BtnImageClear_Click(object sender, RoutedEventArgs e) => await Request(ImageSourceStripRequestKind.Clear);
    private async void BtnImageAdd_Click(object sender, RoutedEventArgs e) => await Request(ImageSourceStripRequestKind.ImportFiles);
    private async void BtnImageAddFolder_Click(object sender, RoutedEventArgs e) => await Request(ImageSourceStripRequestKind.ImportDirectory);
    private void BtnImageAddRpFolder_Click(object sender, RoutedEventArgs e) { }
    private async void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DisplayIndex >= 0 && DisplayIndex < Items.Count)
            await Request(ImageSourceStripRequestKind.OpenDirectory, Items[DisplayIndex].Id);
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
    private void ListKeyDown(object sender, KeyEventArgs e)
    {
        if (!CanInteract || Items.Count == 0 || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)) return;
        // Let the original ListBox perform navigation. Only its resulting user selection may activate.
        var generation = ++_keyboardSelectionGeneration;
        _keyboardSelectionPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        { if (generation == _keyboardSelectionGeneration) _keyboardSelectionPending = false; }));
    }
    private async void ListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, lst) || !_keyboardSelectionPending || _refreshing || !CanInteract) return;
        _keyboardSelectionPending = false;
        if (lst.SelectedItem is ImageSourceStripItem item) await Request(ImageSourceStripRequestKind.Activate, item.Id);
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
