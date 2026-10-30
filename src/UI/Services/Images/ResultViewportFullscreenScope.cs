using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SunEyeVision.UI.Services.Images;

/// <summary>Moves the one actual native view, never its image, adapter or viewport owner.</summary>
internal sealed class ResultViewportFullscreenScope : IDisposable
{
    private static readonly ConditionalWeakTable<Window, HostState> Hosts = new();
    private static readonly ConditionalWeakTable<FrameworkElement, ViewState> Views = new();
    private sealed class ViewState { internal ResultViewportFullscreenScope? Active; }
    private sealed class HostState
    {
        internal ResultViewportFullscreenScope? Active;
        internal long Generation;
        internal bool Closing, Closed;
        internal HostState(Window owner)
        {
            owner.Closing += (_, _) =>
            {
                Closing = true;
                Active?.CloseFromEvent();
                owner.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (!Closed) Closing = false; }));
            };
            owner.Closed += (_, _) => { Closing = Closed = true; Active?.CloseFromEvent(); };
        }
    }
    private readonly HostState _host;
    private readonly ViewState _viewState;
    private readonly ParentSlot _slot;
    private readonly List<(FrameworkElement View, ParentSlot Slot)> _navigation = new();
    private readonly Grid? _surface;
    private readonly long _generation;
    private bool _detached, _installed, _disposed, _closed;
    internal FrameworkElement View { get; }
    internal Window Window { get; }
    internal Exception? CloseError { get; private set; }
    internal bool IsCurrent => !_disposed && !_host.Closing && !_host.Closed && _generation == _host.Generation
        && ReferenceEquals(_host.Active, this) && ReferenceEquals(_viewState.Active, this)
        && (ReferenceEquals(Window.Content, View) || _surface is not null
            && ReferenceEquals(Window.Content, _surface) && _surface.Children.Contains(View));

    private ResultViewportFullscreenScope(FrameworkElement view, Window owner, HostState host,
        ViewState viewState, ParentSlot slot, long generation, IReadOnlyList<FrameworkElement> navigation)
    {
        View = view; _host = host; _viewState = viewState; _slot = slot; _generation = generation;
        Window = new Window
        {
            Owner = owner, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            WindowState = WindowState.Maximized, Background = Brushes.Black, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Topmost = true
        };
        if (navigation.Count > 0)
        {
            _surface = new Grid { DataContext = view.DataContext };
            var resources = new List<ResourceDictionary>();
            for (DependencyObject? current = view; current is not null; current = LogicalTreeHelper.GetParent(current))
                if (current is FrameworkElement element && (element.Resources.Count > 0 || element.Resources.MergedDictionaries.Count > 0)) resources.Add(element.Resources);
            foreach (var resource in resources.AsEnumerable().Reverse()) _surface.Resources.MergedDictionaries.Add(resource);
            foreach (var item in navigation) _navigation.Add((item, ParentSlot.Capture(item)));
        }
        Window.Closed += OnClosed;
        Window.PreviewKeyDown += OnKey;
        Window.PreviewMouseDoubleClick += OnDoubleClick;
    }

    internal static ResultViewportFullscreenScope Open(FrameworkElement view, Window owner,
        IReadOnlyList<FrameworkElement>? navigation = null)
    {
        ArgumentNullException.ThrowIfNull(view); ArgumentNullException.ThrowIfNull(owner);
        view.Dispatcher.VerifyAccess(); owner.Dispatcher.VerifyAccess();
        if (!ReferenceEquals(view.Dispatcher, owner.Dispatcher)) throw new ArgumentException("图像与主窗必须属于同一Dispatcher。");
        var host = Hosts.GetValue(owner, value => new HostState(value));
        if (host.Closing || host.Closed || owner.Dispatcher.HasShutdownStarted)
            throw new InvalidOperationException("主窗正在关闭，不能进入全屏。");
        var generation = ++host.Generation;
        host.Active?.Dispose();
        if (generation != host.Generation || host.Closing || host.Closed)
            throw new InvalidOperationException("全屏请求已被新的窗口切换替换。");
        var viewState = Views.GetValue(view, _ => new ViewState());
        viewState.Active?.Dispose();
        if (generation != host.Generation || host.Closing || host.Closed)
            throw new InvalidOperationException("全屏请求已被新的窗口切换替换。");
        if (!ReferenceEquals(System.Windows.Window.GetWindow(view), owner))
            throw new InvalidOperationException("原图像视图不属于请求的主窗，不能借其他窗口的关闭生命周期。");
        var slot = ParentSlot.Capture(view);
        navigation ??= Array.Empty<FrameworkElement>();
        if (navigation.Distinct().Count() != navigation.Count || navigation.Any(item => ReferenceEquals(item, view)
            || !ReferenceEquals(System.Windows.Window.GetWindow(item), owner)))
            throw new ArgumentException("全屏导航必须是同一主窗中的独立原控件。", nameof(navigation));
        ResultViewportFullscreenScope? scope = null;
        try
        {
            scope = new(view, owner, host, viewState, slot, generation, navigation);
            host.Active = scope; viewState.Active = scope;
            scope._detached = true; slot.Detach(view);
            scope.RequireOwnership();
            if (scope._surface is null) scope.Window.Content = view;
            else
            {
                scope._surface.Children.Add(view);
                foreach (var item in scope._navigation)
                {
                    item.Slot.Detach(item.View);
                    scope.RequireOwnership();
                    scope._surface.Children.Add(item.View);
                }
                scope.Window.Content = scope._surface;
            }
            scope._installed = true;
            scope.RequireOwnership();
            scope.Window.Show();
            scope.RequireOwnership();
            return scope;
        }
        catch (Exception failure)
        {
            if (scope is not null)
                try { scope.Dispose(); } catch (Exception cleanup) { throw new AggregateException("全屏安装及还原失败。", failure, cleanup); }
            throw;
        }
    }
    private void RequireOwnership()
    {
        if (_disposed || _generation != _host.Generation || !ReferenceEquals(_host.Active, this)
            || !ReferenceEquals(_viewState.Active, this) || _host.Closing || _host.Closed)
            throw new InvalidOperationException("全屏安装期间窗口所有权已失效。");
    }
    private void OnClosed(object? sender, EventArgs args) { _closed = true; CloseFromEvent(); }
    private void OnKey(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape) return;
        args.Handled = true; CloseFromEvent();
    }
    private void OnDoubleClick(object sender, MouseButtonEventArgs args)
    {
        if (args.OriginalSource is FrameworkElement { IsMouseCaptured: true }) return;
        args.Handled = true; CloseFromEvent();
    }
    private void CloseFromEvent()
    {
        try { Dispose(); }
        catch (Exception error) { CloseError = error; } // Keep WPF close events terminal; explicit Dispose still reports failure.
    }
    public void Dispose()
    {
        View.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        var failures = new List<Exception>();
        void Run(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        Window.Closed -= OnClosed; Window.PreviewKeyDown -= OnKey;
        Window.PreviewMouseDoubleClick -= OnDoubleClick;
        try
        {
            if (ReferenceEquals(_viewState.Active, this))
            {
                var mayRestore = false;
                if (_surface is not null)
                {
                    foreach (var item in _navigation)
                    {
                        if (_surface.Children.Contains(item.View)) Run(() => _surface.Children.Remove(item.View));
                        if (ParentSlot.ParentOf(item.View) is null) Run(() => item.Slot.RestoreIfVacant(item.View));
                    }
                    if (_surface.Children.Contains(View))
                    {
                        Run(() => _surface.Children.Remove(View));
                        mayRestore = ParentSlot.ParentOf(View) is null;
                    }
                    if (ReferenceEquals(Window.Content, _surface)) Run(() => Window.Content = null);
                }
                if (ReferenceEquals(Window.Content, View))
                {
                    Run(() => Window.Content = null);
                    mayRestore = ParentSlot.ParentOf(View) is null;
                }
                else if (_detached && !_installed && ParentSlot.ParentOf(View) is null) mayRestore = true;
                // External replacement/reparenting wins. Never detach a view from its newer parent,
                // and never overwrite a newer child in the captured Decorator/ContentControl slot.
                if (mayRestore && ReferenceEquals(_viewState.Active, this)) Run(() => _slot.RestoreIfVacant(View));
            }
        }
        finally
        {
            if (ReferenceEquals(_viewState.Active, this)) _viewState.Active = null;
            if (ReferenceEquals(_host.Active, this)) _host.Active = null;
            if (!_closed) Run(Window.Close);
        }
        if (failures.Count != 0)
        {
            CloseError = new AggregateException("全屏视图还原失败。", failures);
            throw CloseError;
        }
    }

    private abstract class ParentSlot
    {
        internal static DependencyObject? ParentOf(FrameworkElement view) => view.Parent ?? VisualTreeHelper.GetParent(view);
        internal static ParentSlot Capture(FrameworkElement view) => ParentOf(view) switch
        {
            Panel panel when panel.Children.Contains(view) => new PanelSlot(panel, panel.Children.IndexOf(view)),
            Decorator decorator when ReferenceEquals(decorator.Child, view) => new DecoratorSlot(decorator),
            ContentControl content when ReferenceEquals(content.Content, view) => new ContentSlot(content),
            _ => throw new InvalidOperationException("原图像视图必须位于明确的Panel、Decorator或ContentControl槽位。")
        };
        internal abstract void Detach(FrameworkElement view);
        internal abstract void RestoreIfVacant(FrameworkElement view);
    }
    private sealed class PanelSlot(Panel parent, int index) : ParentSlot
    {
        internal override void Detach(FrameworkElement view)
        {
            if (!parent.Children.Contains(view)) throw new InvalidOperationException("原图像Panel槽位已变化。");
            parent.Children.Remove(view);
        }
        internal override void RestoreIfVacant(FrameworkElement view)
        { if (ParentOf(view) is null) parent.Children.Insert(Math.Min(index, parent.Children.Count), view); }
    }
    private sealed class DecoratorSlot(Decorator parent) : ParentSlot
    {
        internal override void Detach(FrameworkElement view)
        {
            if (!ReferenceEquals(parent.Child, view)) throw new InvalidOperationException("原图像Decorator槽位已变化。");
            parent.Child = null;
        }
        internal override void RestoreIfVacant(FrameworkElement view)
        { if (parent.Child is null && ParentOf(view) is null) parent.Child = view; }
    }
    private sealed class ContentSlot(ContentControl parent) : ParentSlot
    {
        internal override void Detach(FrameworkElement view)
        {
            if (!ReferenceEquals(parent.Content, view)) throw new InvalidOperationException("原图像Content槽位已变化。");
            parent.SetCurrentValue(ContentControl.ContentProperty, null);
        }
        internal override void RestoreIfVacant(FrameworkElement view)
        { if (parent.Content is null && ParentOf(view) is null) parent.SetCurrentValue(ContentControl.ContentProperty, view); }
    }
}
