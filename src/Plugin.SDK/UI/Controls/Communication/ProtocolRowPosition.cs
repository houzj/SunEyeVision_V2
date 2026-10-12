using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Communication;

/// <summary>Tracks the current item position, independently of WPF alternating row indices.</summary>
public static class ProtocolRowPosition
{
    private static readonly DependencyPropertyKey IndexPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "Index", typeof(int), typeof(ProtocolRowPosition), new PropertyMetadata(-1));
    public static readonly DependencyProperty IndexProperty = IndexPropertyKey.DependencyProperty;
    public static int GetIndex(DependencyObject target) => (int)target.GetValue(IndexProperty);

    private static readonly DependencyProperty TrackerProperty = DependencyProperty.RegisterAttached(
        "Tracker", typeof(Tracker), typeof(ProtocolRowPosition));
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ProtocolRowPosition), new PropertyMetadata(false, EnabledChanged));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);

    private static void EnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not DataGrid grid) return;
        (grid.GetValue(TrackerProperty) as Tracker)?.Dispose();
        grid.SetValue(TrackerProperty, (bool)e.NewValue ? new Tracker(grid) : null);
    }

    private sealed class Tracker : IDisposable
    {
        private readonly DataGrid _grid;
        private bool _pending, _disposed;
        public Tracker(DataGrid grid)
        {
            _grid = grid;
            ((INotifyCollectionChanged)grid.Items).CollectionChanged += ItemsChanged;
            grid.LoadingRow += LoadingRow;
            grid.ItemContainerGenerator.StatusChanged += StatusChanged;
            Schedule();
        }
        private void LoadingRow(object? sender, DataGridRowEventArgs e)
            => e.Row.SetValue(IndexPropertyKey, _grid.Items.IndexOf(e.Row.Item));
        private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Schedule();
        private void StatusChanged(object? sender, EventArgs e) => Schedule();
        private void Schedule()
        {
            if (_pending || _disposed) return;
            _pending = true;
            // Move notifications precede container reassignment; refresh after that reassignment.
            _grid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _pending = false;
                if (_disposed) return;
                for (var i = 0; i < _grid.Items.Count; i++)
                    if (_grid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                        row.SetValue(IndexPropertyKey, _grid.Items.IndexOf(row.Item));
            }));
        }
        public void Dispose()
        {
            _disposed = true;
            ((INotifyCollectionChanged)_grid.Items).CollectionChanged -= ItemsChanged;
            _grid.LoadingRow -= LoadingRow;
            _grid.ItemContainerGenerator.StatusChanged -= StatusChanged;
        }
    }
}
