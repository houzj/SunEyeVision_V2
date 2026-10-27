using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Adapters.Resources;

/// <summary>Restores inherited original header text properties without changing page content or templates.</summary>
internal sealed class NativeTabHeaderTextScope : IDisposable
{
    private readonly TabControl _tabs;
    private DispatcherOperation? _pending;
    private long _generation;
    private bool _disposed;

    public NativeTabHeaderTextScope(TabControl tabs)
    {
        _tabs = tabs;
        tabs.Loaded += OnLoaded;
        tabs.Unloaded += OnUnloaded;
        tabs.SelectionChanged += OnSelectionChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => Queue();
    private void OnUnloaded(object sender, RoutedEventArgs args) => Cancel();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.OriginalSource, _tabs)) Queue();
    }

    private void Cancel()
    {
        _generation++;
        _pending?.Abort();
        _pending = null;
    }

    private void Queue()
    {
        Cancel();
        if (_disposed || !_tabs.IsLoaded) return;
        var generation = _generation;
        _pending = _tabs.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_disposed || generation != _generation || !_tabs.IsLoaded) return;
            _pending = null;
            foreach (var tab in _tabs.Items.OfType<TabItem>())
                if (tab.Template?.FindName("Content", tab) is ContentControl header)
                    NativeTextInheritance.Restore(header);
        }));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel();
        _tabs.Loaded -= OnLoaded;
        _tabs.Unloaded -= OnUnloaded;
        _tabs.SelectionChanged -= OnSelectionChanged;
    }
}
