using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.UI.Commands;
using SunEyeVision.UI.Services.Logging;
using SunEyeVision.UI.Adapters;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 日志面板视图模型
    /// 
    /// 特性：
    /// - 虚拟化渲染：支持万条日志流畅滚动
    /// - 循环缓冲区：O(1) 添加/移除操作
    /// - 增量统计：避免遍历计算
    /// - 过滤功能：按日志级别过滤
    /// - 搜索功能：按消息内容搜索
    /// </summary>
    public class LogPanelViewModel : ViewModelBase, IDisposable
    {
        #region 私有字段

        private readonly UILogWriter _uiLogWriter;
        private readonly VisionLogger _logger;
        private bool _autoScroll = true;
        private bool _isDiagnosticMode = false;
        private string _activeFilter = "All"; // 当前选中的日志级别过滤模式：All/Info/Success/Warning/Error
        private string _searchText = string.Empty;
        private bool _isDisposed = false;
        private bool _isCopying = false; // 防止重复复制
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private readonly TimeSpan _throttleInterval = TimeSpan.FromMilliseconds(50); // 50ms节流间隔

        #endregion

        #region 属性

        /// <summary>
        /// 日志条目集合（高性能循环缓冲区）
        /// </summary>
        public CircularBufferLogCollection LogEntries => _uiLogWriter.LogEntries;

        /// <summary>
        /// 日志条目的集合视图（用于过滤）
        /// </summary>
        public ICollectionView LogEntriesView { get; }

        /// <summary>
        /// 是否自动滚动到最新
        /// </summary>
        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        /// <summary>
        /// 是否开启诊断模式（开启后显示 Debug 级别日志）
        /// </summary>
        public bool IsDiagnosticMode
        {
            get => _isDiagnosticMode;
            set
            {
                if (SetProperty(ref _isDiagnosticMode, value))
                {
                    // 切换 VisionLogger 全局最低级别
                    _logger.MinLevel = value ? LogLevel.Debug : LogLevel.Info;
                    // 同步 UILogWriter 的最低级别（UILogWriter.WriteBatch 中会再次过滤）
                    _uiLogWriter.MinLevel = value ? LogLevel.Debug : LogLevel.Info;
                    RefreshFilter();
                }
            }
        }

        /// <summary>
        /// 是否显示全部级别（单选过滤模式，与 Info/Success/Warning/Error 互斥）
        /// </summary>
        public bool ShowAll
        {
            get => _activeFilter == "All";
            set { if (value) SelectLevelFilter("All"); }
        }

        /// <summary>
        /// 是否显示信息日志（单选过滤模式）
        /// </summary>
        public bool ShowInfo
        {
            get => _activeFilter == "Info";
            set { if (value) SelectLevelFilter("Info"); }
        }

        /// <summary>
        /// 是否显示成功日志（单选过滤模式）
        /// </summary>
        public bool ShowSuccess
        {
            get => _activeFilter == "Success";
            set { if (value) SelectLevelFilter("Success"); }
        }

        /// <summary>
        /// 是否显示警告日志（单选过滤模式）
        /// </summary>
        public bool ShowWarning
        {
            get => _activeFilter == "Warning";
            set { if (value) SelectLevelFilter("Warning"); }
        }

        /// <summary>
        /// 是否显示错误日志（单选过滤模式）
        /// </summary>
        public bool ShowError
        {
            get => _activeFilter == "Error";
            set { if (value) SelectLevelFilter("Error"); }
        }

        /// <summary>
        /// 切换当前选中的日志级别过滤模式（5 个级别互斥单选，重复点击已选项不取消）
        /// </summary>
        private void SelectLevelFilter(string filter)
        {
            if (_activeFilter == filter)
                return;

            _activeFilter = filter;
            OnPropertyChanged(nameof(ShowAll));
            OnPropertyChanged(nameof(ShowInfo));
            OnPropertyChanged(nameof(ShowSuccess));
            OnPropertyChanged(nameof(ShowWarning));
            OnPropertyChanged(nameof(ShowError));
            RefreshFilter();
        }

        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    RefreshFilter();
            }
        }

        /// <summary>
        /// 日志总数（O(1)访问）- UI显示的数量
        /// </summary>
        public int TotalCount => LogEntries.Count;

        /// <summary>
        /// 总入队数量（不受容量限制影响）
        /// </summary>
        public long TotalEnqueued => _uiLogWriter.TotalEnqueued;

        /// <summary>
        /// 错误数量（O(1)访问）
        /// </summary>
        public int ErrorCount => LogEntries.ErrorCount;

        /// <summary>
        /// 警告数量（O(1)访问）
        /// </summary>
        public int WarningCount => LogEntries.WarningCount;

        /// <summary>
        /// 统计信息
        /// </summary>
        public string Statistics
        {
            get
            {
                var (total, dropped, queueSize, writers) = _logger.GetStatistics();
                return $"已记录: {TotalEnqueued} | 显示: {TotalCount} | 丢弃: {dropped} | 队列: {queueSize}";
            }
        }

        /// <summary>
        /// 统计摘要（用于状态栏）
        /// </summary>
        public string StatisticsSummary =>
            $"已记录: {TotalEnqueued} | 显示: {TotalCount} | 错误: {ErrorCount} | 警告: {WarningCount}";

        #endregion

        #region 命令

        /// <summary>
        /// 清空日志命令
        /// </summary>
        public ICommand ClearCommand { get; }

        /// <summary>
        /// 切换自动滚动命令
        /// </summary>
        public ICommand ToggleAutoScrollCommand { get; }

        /// <summary>
        /// 导出日志命令
        /// </summary>
        public ICommand ExportCommand { get; }

        /// <summary>
        /// 复制选中日志命令
        /// </summary>
        public ICommand CopyCommand { get; }

        /// <summary>
        /// 复制所有日志命令
        /// </summary>
        public ICommand CopyAllCommand { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 日志更新事件（用于触发 UI 滚动）
        /// </summary>
        public event EventHandler<LogBatchEventArgs>? LogsUpdated;

        #endregion

        #region 构造函数

        public LogPanelViewModel()
        {
            _logger = VisionLogger.Instance;
            // 使用预创建的 UILogWriter 实例（在应用启动时已初始化）
            // 确保日志从启动开始就能显示到UI
            _uiLogWriter = ServiceInitializer.UILogWriter;

            // 创建集合视图
            LogEntriesView = CollectionViewSource.GetDefaultView(LogEntries);
            LogEntriesView.Filter = FilterLogEntry;

            // 初始化命令
            ClearCommand = new RelayCommand(ClearLogs, null, "ClearLogs");
            ToggleAutoScrollCommand = new RelayCommand(() => AutoScroll = !AutoScroll, null, "ToggleAutoScroll");
            ExportCommand = new RelayCommand(ExportLogs, null, "ExportLogs");
            CopyCommand = new RelayCommand(CopySelectedLog, null, "CopySelectedLog");
            CopyAllCommand = new RelayCommand(CopyAllLogs, null, "CopyAllLogs");

            // 订阅日志事件
            _uiLogWriter.LogsAdded += OnLogsAdded;
            _uiLogWriter.LogsCleared += OnLogsCleared;
            _uiLogWriter.StatisticsChanged += OnStatisticsChanged;

            // 订阅集合变化
            LogEntries.CollectionChanged += OnLogEntriesCollectionChanged;

            // 验证命令初始化
            LogInfo($"ClearCommand 对象哈希: {ClearCommand?.GetHashCode()}");
            LogInfo($"ClearCommand CanExecute: {ClearCommand?.CanExecute(null)}");
            LogInfo("日志面板ViewModel初始化成功，命令已创建");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 刷新过滤器
        /// </summary>
        public void RefreshFilter()
        {
            LogEntriesView.Refresh();
            OnPropertyChanged(nameof(Statistics));
        }

        /// <summary>
        /// 公共方法：复制文本到剪贴板（供外部调用）
        /// </summary>
        public bool CopyToClipboardInternal(string text)
        {
            // 空引用检查
            if (string.IsNullOrEmpty(text))
            {
                LogWarning("复制内容为空");
                return false;
            }

            if (_isCopying)
            {
                LogWarning("复制操作正在进行中，请稍候...");
                return false;
            }

            _isCopying = true;
            try
            {
                return CopyToClipboard(text);
            }
            catch (NullReferenceException ex)
            {
                LogError($"空引用异常: {ex.Message}");
                return false;
            }
            finally
            {
                _isCopying = false;
            }
        }

        /// <summary>
        /// 添加日志（通过核心日志器）
        /// </summary>
        public void AddLog(LogLevel level, string message, string? source = null)
        {
            _logger.Log(level, message, source);
        }

        /// <summary>
        /// 添加信息日志
        /// </summary>
        public void LogInfo(string message, string? source = null)
        {
            _logger.Log(LogLevel.Info, message, source);
        }

        /// <summary>
        /// 添加成功日志
        /// </summary>
        public void LogSuccess(string message, string? source = null)
        {
            _logger.Log(LogLevel.Success, message, source);
        }

        /// <summary>
        /// 添加警告日志
        /// </summary>
        public void LogWarning(string message, string? source = null)
        {
            _logger.Log(LogLevel.Warning, message, source);
        }

        /// <summary>
        /// 添加错误日志
        /// </summary>
        public void LogError(string message, string? source = null, Exception? exception = null)
        {
            _logger.Log(LogLevel.Error, message, source, exception);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 日志过滤器
        /// </summary>
        private bool FilterLogEntry(object obj)
        {
            if (obj is not LogEntry entry)
                return false;

            // 级别过滤（单选模式：All=全部；Info/Success/Warning/Error=只看该级别）
            var levelVisible = _activeFilter switch
            {
                "Info" => entry.Level == LogLevel.Info,
                "Success" => entry.Level == LogLevel.Success,
                "Warning" => entry.Level == LogLevel.Warning,
                "Error" => entry.Level == LogLevel.Error,
                // All：非 Debug 级别全显示，Debug 级别按诊断模式开关
                _ => entry.Level != LogLevel.Debug || IsDiagnosticMode
            };

            if (!levelVisible)
                return false;

            // 搜索过滤
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                return entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       (entry.Source?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            return true;
        }

        /// <summary>
        /// 处理日志批量添加事件
        /// </summary>
        private void OnLogsAdded(object? sender, LogBatchEventArgs e)
        {
            var now = DateTime.UtcNow;

            // 节流控制：限制UI更新频率
            if ((now - _lastUpdateTime).TotalMilliseconds < _throttleInterval.TotalMilliseconds)
            {
                // 间隔未到，跳过本次更新
                return;
            }

            _lastUpdateTime = now;

            // 触发更新事件
            LogsUpdated?.Invoke(this, e);

            // 更新属性
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalEnqueued));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(Statistics));
            OnPropertyChanged(nameof(StatisticsSummary));
        }

        /// <summary>
        /// 处理日志清空事件
        /// </summary>
        private void OnLogsCleared(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalEnqueued));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(Statistics));
            OnPropertyChanged(nameof(StatisticsSummary));
        }

        /// <summary>
        /// 处理统计变更事件
        /// </summary>
        private void OnStatisticsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalEnqueued));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(Statistics));
            OnPropertyChanged(nameof(StatisticsSummary));
        }

        /// <summary>
        /// 集合变化处理
        /// </summary>
        private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 触发统计更新
            OnPropertyChanged(nameof(TotalCount));
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        private void ClearLogs()
        {
            try
            {
                // 清空日志
                _uiLogWriter.Clear();

                // 更新属性通知
                OnPropertyChanged(nameof(Statistics));
                OnPropertyChanged(nameof(StatisticsSummary));
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error, $"清空日志失败: {ex.Message}", "日志面板", ex);
            }
        }

        /// <summary>
        /// 复制选中的日志
        /// </summary>
        private void CopySelectedLog()
        {
            // 防止重复调用
            if (_isCopying)
            {
                LogWarning("复制操作正在进行中，请稍候...");
                return;
            }

            try
            {
                var selectedItems = LogEntriesView.Cast<LogEntry>().Where(entry => entry != null).ToList();
                if (selectedItems.Count == 0)
                {
                    LogWarning("未选中任何日志条目");
                    return;
                }

                var logText = string.Join(Environment.NewLine, selectedItems.Select(entry =>
                    $"[{entry.FormattedTime}] [{entry.Level}] {entry.DisplayMessage}"));

                _isCopying = true;
                try
                {
                    if (CopyToClipboard(logText))
                    {
                        LogSuccess($"已复制 {selectedItems.Count} 条日志到剪贴板");
                    }
                }
                finally
                {
                    _isCopying = false;
                }
            }
            catch (Exception ex)
            {
                _isCopying = false;
                LogError($"复制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 复制所有日志
        /// </summary>
        private void CopyAllLogs()
        {
            // 防止重复调用
            if (_isCopying)
            {
                LogWarning("复制操作正在进行中，请稍候...");
                return;
            }

            try
            {
                if (LogEntries.Count == 0)
                {
                    LogWarning("没有可复制的日志");
                    return;
                }

                var logText = string.Join(Environment.NewLine, LogEntries.Select(entry =>
                    $"[{entry.FormattedTime}] [{entry.Level}] {entry.DisplayMessage}"));

                _isCopying = true;
                try
                {
                    if (CopyToClipboard(logText))
                    {
                        LogSuccess($"已复制所有日志 ({LogEntries.Count} 条) 到剪贴板");
                    }
                }
                finally
                {
                    _isCopying = false;
                }
            }
            catch (Exception ex)
            {
                _isCopying = false;
                LogError($"复制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全地复制文本到剪贴板（带重试机制）
        /// </summary>
        private bool CopyToClipboard(string text)
        {
            // 记录本次复制的内容长度，用于调试
            int textLength = text?.Length ?? 0;
            int lineCount = text?.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length ?? 0;

            // 优先使用 WPF Clipboard，更可靠
            try
            {
                // 清空剪贴板
                Clipboard.Clear();
                // 设置新文本（替换而非追加）
                Clipboard.SetText(text);
                // 强制刷新
                Clipboard.Flush();

                // 验证复制结果
                string? copiedText = Clipboard.GetText();
                if (copiedText?.Length == textLength)
                {
                    LogInfo($"复制成功: {lineCount} 行, {textLength} 字符");
                    return true;
                }
                else
                {
                    LogWarning($"复制验证失败: 期望 {textLength} 字符, 实际 {copiedText?.Length ?? 0} 字符");
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                // 剪贴板被占用，尝试使用 Win32 API
                LogWarning($"WPF Clipboard 失败，尝试使用 Win32 API: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"剪贴板操作失败: {ex.Message}");
                return false;
            }

            // 备用方案：使用 Win32 API
            int maxRetries = 3;
            int delayMs = 20;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        try
                        {
                            // 清空剪贴板
                            EmptyClipboard();
                            // 设置新数据
                            var hGlobal = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(text);
                            SetClipboardData(13, hGlobal); // CF_UNICODETEXT = 13
                            CloseClipboard();

                            LogInfo($"Win32 API 复制成功: {lineCount} 行, {textLength} 字符");
                            return true;
                        }
                        catch
                        {
                            CloseClipboard();
                            throw;
                        }
                    }
                }
                catch
                {
                    // 忽略异常，继续重试
                }

                // 等待后重试
                if (i < maxRetries - 1)
                {
                    System.Threading.Thread.Sleep(delayMs);
                    delayMs *= 2;
                }
            }

            LogError($"剪贴板操作失败（已重试 {maxRetries} 次）");
            return false;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        /// <summary>
        /// 导出日志
        /// </summary>
        private void ExportLogs()
        {
            // TODO: 实现日志导出功能
            // 可以导出为文本文件或 CSV
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _uiLogWriter.LogsAdded -= OnLogsAdded;
            _uiLogWriter.LogsCleared -= OnLogsCleared;
            _uiLogWriter.StatisticsChanged -= OnStatisticsChanged;
            LogEntries.CollectionChanged -= OnLogEntriesCollectionChanged;

            // 注意：不 Dispose _uiLogWriter，因为它是全局共享实例（由 ServiceInitializer 管理）
            // 每个 LogPanelViewModel 实例只是取消订阅事件

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
