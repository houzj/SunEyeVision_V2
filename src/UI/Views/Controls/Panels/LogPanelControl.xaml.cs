using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.ViewModels;

namespace SunEyeVision.UI.Views.Controls.Panels
{
    /// <summary>
    /// LogPanelControl.xaml 的交互逻辑
    /// 高性能日志面板独立控件
    /// </summary>
    public partial class LogPanelControl : UserControl, INotifyPropertyChanged
    {
        #region 私有字段

        private readonly LogPanelViewModel _logViewModel;
        private long _totalEnqueued;
        private int _totalCount;
        private int _errorCount;
        private int _warningCount;

        #endregion

        #region 公共属性

        /// <summary>
        /// 日志 ViewModel
        /// </summary>
        public LogPanelViewModel ViewModel => _logViewModel;

        /// <summary>
        /// 日志条目总数（已记录）
        /// </summary>
        public long TotalEnqueued
        {
            get => _totalEnqueued;
            private set
            {
                if (_totalEnqueued != value)
                {
                    _totalEnqueued = value;
                    OnPropertyChanged(nameof(TotalEnqueued));
                }
            }
        }

        /// <summary>
        /// 当前显示的日志条目数
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount != value)
                {
                    _totalCount = value;
                    OnPropertyChanged(nameof(TotalCount));
                }
            }
        }

        /// <summary>
        /// 错误日志数量
        /// </summary>
        public int ErrorCount
        {
            get => _errorCount;
            private set
            {
                if (_errorCount != value)
                {
                    _errorCount = value;
                    OnPropertyChanged(nameof(ErrorCount));
                }
            }
        }

        /// <summary>
        /// 警告日志数量
        /// </summary>
        public int WarningCount
        {
            get => _warningCount;
            private set
            {
                if (_warningCount != value)
                {
                    _warningCount = value;
                    OnPropertyChanged(nameof(WarningCount));
                }
            }
        }

        #endregion

        #region 构造函数

        public LogPanelControl()
        {
            InitializeComponent();

            try
            {
                // 创建 ViewModel（此时 ServiceInitializer.UILogWriter 应该已经被初始化）
                _logViewModel = new LogPanelViewModel();
                DataContext = _logViewModel;

                // 订阅日志变化事件
                _logViewModel.LogEntries.CollectionChanged += (s, e) =>
                {
                    UpdateLogCounts();
                    AutoScrollToEnd();
                };
                _logViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(LogPanelViewModel.TotalEnqueued))
                    {
                        TotalEnqueued = _logViewModel.TotalEnqueued;
                    }
                    else if (e.PropertyName == nameof(LogPanelViewModel.SearchText))
                    {
                        UpdateWatermarkVisibility();
                    }
                };

                // 初始化计数
                UpdateLogCounts();

                // 初始化水印可见性
                Loaded += (s, e) => UpdateWatermarkVisibility();
            }
            catch (InvalidOperationException ex)
            {
                // 如果 UILogWriter 未初始化，记录错误
MessageBox.Show($"日志面板初始化失败: {ex.Message}\n\n请确保在 App.xaml.cs 中正确调用 ServiceInitializer.InitializeServices()",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
}
            TotalEnqueued = _logViewModel.TotalEnqueued;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 更新日志统计信息
        /// </summary>
        private void UpdateLogCounts()
        {
            try
            {
                if (_logViewModel.LogEntries == null)
                {
                    TotalCount = 0;
                    ErrorCount = 0;
                    WarningCount = 0;
                    return;
                }

                var entries = _logViewModel.LogEntries;
                TotalCount = entries.Count;
                ErrorCount = entries.Count(e => e.Level == LogLevel.Error);
                WarningCount = entries.Count(e => e.Level == LogLevel.Warning);
            }
            catch (Exception ex)
            {
                _logViewModel?.LogError($"更新日志统计失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动滚动到最新日志
        /// </summary>
        private void AutoScrollToEnd()
        {
            try
            {
                if (LogDataGrid == null || !_logViewModel.AutoScroll)
                    return;

                var entries = _logViewModel.LogEntries;
                if (entries == null || entries.Count == 0)
                    return;

                // 滚动到最后一项
                var lastEntry = entries.LastOrDefault();
                if (lastEntry != null)
                {
                    LogDataGrid.ScrollIntoView(lastEntry);
                }
            }
            catch (Exception ex)
            {
                // 静默处理，避免日志刷屏
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 复制选中的日志 - 右键菜单
        /// </summary>
        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LogDataGrid == null)
                {
                    _logViewModel.LogWarning("LogDataGrid 未初始化");
                    return;
                }

                var selectedItems = LogDataGrid.SelectedItems.Cast<LogEntry>().Where(entry => entry != null).ToList();
                if (selectedItems.Count == 0)
                {
                    _logViewModel.LogWarning("选中的日志条目无效");
                    return;
                }

                _logViewModel.LogInfo($"DataGrid 选中: {selectedItems.Count} 项");

                var logText = string.Join(Environment.NewLine, selectedItems.Select(entry =>
                    $"[{entry.FormattedTime}] [{entry.Level}] {entry.DisplayMessage}"));

                if (_logViewModel?.CopyToClipboardInternal(logText) == true)
                {
                    _logViewModel.LogSuccess($"已复制 {selectedItems.Count} 条日志到剪贴板");
                }
            }
            catch (NullReferenceException ex)
            {
                _logViewModel?.LogError($"空引用异常: {ex.Message}");
}
            catch (Exception ex)
            {
                _logViewModel?.LogError($"复制失败: {ex.Message}");
}
        }

        /// <summary>
        /// 清空日志 - 右键菜单
        /// </summary>
        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logViewModel?.ClearCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _logViewModel?.LogError($"清空日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除搜索内容
        /// </summary>
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logViewModel.SearchText = string.Empty;
                SearchTextBox?.Focus();
            }
            catch (Exception ex)
            {
                _logViewModel?.LogError($"清除搜索失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 搜索框获得焦点 - 隐藏水印
        /// </summary>
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// 搜索框失去焦点 - 根据文本决定是否显示水印
        /// </summary>
        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// 更新水印和清除按钮可见性
        /// </summary>
        private void UpdateWatermarkVisibility()
        {
            if (WatermarkText == null || ClearSearchButton == null) return;

            var hasText = !string.IsNullOrEmpty(SearchTextBox?.Text);
            var hasFocus = SearchTextBox?.IsFocused ?? false;

            // 有焦点或有文本时隐藏水印
            WatermarkText.Visibility = (hasFocus || hasText) ? Visibility.Collapsed : Visibility.Visible;

            // 有文本时显示清除按钮
            ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 全选 - 右键菜单
        /// </summary>
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LogDataGrid != null)
                {
                    LogDataGrid.SelectAll();
                }
            }
            catch (Exception ex)
            {
                _logViewModel?.LogError($"全选失败: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
