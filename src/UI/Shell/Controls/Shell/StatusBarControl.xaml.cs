using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Foundation.Versioning;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Logging;

namespace SunEyeVision.UI.Shell.Controls.Shell;

/// <summary>右侧日志摘要；完整日志列表等待原件控件接入。</summary>
public partial class StatusBarControl : UserControl
{
    private UILogWriter? _writer;

    public StatusBarControl()
    {
        InitializeComponent();
        VersionText.Text = AppVersionInfo.Product;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(this) || _writer is not null) return;
        _writer = ServiceInitializer.UILogWriter;
        _writer.LogsAdded += OnLogsChanged;
        _writer.LogsCleared += OnLogsCleared;
        RefreshLatestLog();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_writer is null) return;
        _writer.LogsAdded -= OnLogsChanged;
        _writer.LogsCleared -= OnLogsCleared;
        _writer = null;
        LogSummary.DataContext = null;
        LogSummary.Visibility = Visibility.Collapsed;
        NoLogText.Visibility = Visibility.Visible;
    }

    private void OnLogsChanged(object? sender, LogBatchEventArgs e) => RefreshLatestLog();
    private void OnLogsCleared(object? sender, EventArgs e) => RefreshLatestLog();

    private void RefreshLatestLog()
    {
        var entries = _writer?.LogEntries;
        var latest = entries is { Count: > 0 } ? entries[entries.Count - 1] : null;
        LogSummary.DataContext = latest;
        LogSummary.Visibility = latest is null ? Visibility.Collapsed : Visibility.Visible;
        NoLogText.Visibility = latest is null ? Visibility.Visible : Visibility.Collapsed;
    }
}
