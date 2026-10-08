using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class ModbusReceiveRecordsDialog : ThemeDialogWindow
{
    private DispatcherOperation? _pendingScroll;

    public ModbusReceiveRecordsDialog(ModbusAddressPoint point)
    {
        InitializeComponent();
        DataContext = point;
        Loaded += Dialog_Loaded;
        Closed += Dialog_Closed;
    }

    private void Dialog_Loaded(object sender, RoutedEventArgs e)
    {
        ((INotifyCollectionChanged)RecordsList.Items).CollectionChanged -= Records_CollectionChanged;
        ((INotifyCollectionChanged)RecordsList.Items).CollectionChanged += Records_CollectionChanged;
        ScheduleScrollToBottom();
    }

    private void Dialog_Closed(object? sender, EventArgs e)
    {
        ((INotifyCollectionChanged)RecordsList.Items).CollectionChanged -= Records_CollectionChanged;
        _pendingScroll?.Abort();
    }

    private void Records_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScheduleScrollToBottom();

    private void ScheduleScrollToBottom()
    {
        if (_pendingScroll?.Status == DispatcherOperationStatus.Pending) return;
        _pendingScroll = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _pendingScroll = null;
            if (!IsLoaded || RecordsList.Items.Count == 0) return;
            RecordsList.ScrollIntoView(RecordsList.Items[RecordsList.Items.Count - 1]);
            RecordsList.UpdateLayout();
            FindScrollViewer(RecordsList)?.ScrollToBottom();
        }));
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer viewer) return viewer;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var result = FindScrollViewer(VisualTreeHelper.GetChild(parent, index));
            if (result != null) return result;
        }
        return null;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
