using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.Plugin.Editing.Wpf.Models
{
    /// <summary>
    /// 属性变更记录
    /// </summary>
    public class PropertyChangeRecord
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 目标类型
        /// </summary>
        public Type TargetType { get; set; } = null!;

        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; set; } = null!;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// 旧值
        /// </summary>
        public object? OldValue { get; set; }

        /// <summary>
        /// 新值
        /// </summary>
        public object? NewValue { get; set; }

        /// <summary>
        /// 修改者类名
        /// </summary>
        public string? ModifierClass { get; set; }

        /// <summary>
        /// 修改者方法名
        /// </summary>
        public string? ModifierMethod { get; set; }

        /// <summary>
        /// 调用栈
        /// </summary>
        public string? CallStack { get; set; }

        public override string ToString()
        {
            var modifierInfo = !string.IsNullOrEmpty(ModifierClass) && !string.IsNullOrEmpty(ModifierMethod)
                ? $"{ModifierClass}.{ModifierMethod}()"
                : "未知调用者";

            return $"[{Timestamp:HH:mm:ss.fff}] {TargetType.Name}.{PropertyName} ({DisplayName ?? "N/A"}) = {NewValue} | 修改者: {modifierInfo}";
        }
    }

    /// <summary>
    /// 属性变更监控器 - 全局单例
    /// </summary>
    public class PropertyChangeMonitor
    {
        private static readonly Lazy<PropertyChangeMonitor> _instance = new Lazy<PropertyChangeMonitor>(() => new PropertyChangeMonitor());
        public static PropertyChangeMonitor Instance => _instance.Value;

        private readonly List<PropertyChangeRecord> _records = new List<PropertyChangeRecord>();
        private readonly object _lock = new object();

        private PropertyChangeMonitor() { }

        /// <summary>
        /// 记录属性变更
        /// </summary>
        public void RecordChange(PropertyChangeRecord record)
        {
            lock (_lock)
            {
                _records.Add(record);
            }
        }

        /// <summary>
        /// 查询指定属性的变更记录
        /// </summary>
        public List<PropertyChangeRecord> QueryChanges(Type targetType, string propertyName)
        {
            lock (_lock)
            {
                return _records.Where(r => r.TargetType == targetType && r.PropertyName == propertyName).ToList();
            }
        }

        /// <summary>
        /// 查询指定类型的所有变更记录
        /// </summary>
        public List<PropertyChangeRecord> QueryChanges(Type targetType)
        {
            lock (_lock)
            {
                return _records.Where(r => r.TargetType == targetType).ToList();
            }
        }

        /// <summary>
        /// 查询所有变更记录
        /// </summary>
        public List<PropertyChangeRecord> QueryAllChanges()
        {
            lock (_lock)
            {
                return new List<PropertyChangeRecord>(_records);
            }
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _records.Clear();
            }
        }

        /// <summary>
        /// 获取记录数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _records.Count;
                }
            }
        }
    }

    /// <summary>
    /// 可观察对象基类 - 项目中所有属性变化通知的唯一源头
    /// </summary>
    /// <remarks>
    /// 特性：
    /// 1. 统一的属性变化通知机制
    /// 2. 可选的自动日志记录（通过 displayName 参数启用）
    /// 3. 可选的属性变更监控（通过 displayName 参数启用）
    /// 4. 自动追踪修改者信息（通过 StackTrace）
    /// 5. 扩展点支持子类自定义行为
    /// 6. 批量通知抑制（避免频繁的属性变更通知）
    ///
    /// 使用方式：
    /// <code>
    /// // 不记录日志的属性
    /// public int InternalValue
    /// {
    ///     get => _internalValue;
    ///     set => SetProperty(ref _internalValue, value);
    /// }
    ///
    /// // 自动记录日志并监控的属性（指定显示名称）
    /// public int Threshold
    /// {
    ///     get => _threshold;
    ///     set => SetProperty(ref _threshold, value, "阈值");
    /// }
    ///
    /// // 批量更新抑制通知
    /// using (BeginSuppressNotifications())
    /// {
    ///     // 多次修改属性
    ///     Property1 = value1;
    ///     Property2 = value2;
    ///     Property3 = value3;
    /// } // 离开 using 块时，一次性触发所有属性变更通知
    /// </code>
    /// </remarks>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        private static readonly string Version = "V6.0_20260330_批量通知抑制版";

        /// <summary>
        /// 静态构造函数：输出版本信息，确认代码已加载
        /// </summary>
        static ObservableObject()
        {
            PluginLogger.Info($"ObservableObject 已加载 (版本: {Version})", "ObservableObject");
        }

        #region INotifyPropertyChanged

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 通知抑制深度计数器
        /// </summary>
        private int _suppressNotificationDepth = 0;

        /// <summary>
        /// 在抑制通知期间记录变更的属性
        /// </summary>
        private readonly HashSet<string> _changedProperties = new HashSet<string>();

        /// <summary>
        /// 是否正在抑制通知
        /// </summary>
        protected bool IsSuppressingNotification => _suppressNotificationDepth > 0;

        /// <summary>
        /// 批量通知抑制上下文
        /// </summary>
        private class NotificationSuppressor : IDisposable
        {
            private readonly ObservableObject _target;
            private bool _disposed = false;

            public NotificationSuppressor(ObservableObject target)
            {
                _target = target;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _target.EndSuppressNotifications();
            }
        }

        /// <summary>
        /// 开始抑制属性变更通知
        /// </summary>
        /// <returns>IDisposable 对象，用于结束抑制</returns>
        /// <remarks>
        /// 使用方式：
        /// <code>
        /// using (BeginSuppressNotifications())
        /// {
        ///     Property1 = value1;  // 不会立即触发通知
        ///     Property2 = value2;  // 不会立即触发通知
        ///     Property3 = value3;  // 不会立即触发通知
        /// } // 离开 using 块时，一次性触发所有属性变更通知
        /// </code>
        /// </remarks>
        public IDisposable BeginSuppressNotifications()
        {
            _suppressNotificationDepth++;
            return new NotificationSuppressor(this);
        }

        /// <summary>
        /// 结束抑制属性变更通知
        /// </summary>
        private void EndSuppressNotifications()
        {
            _suppressNotificationDepth--;

            if (_suppressNotificationDepth == 0)
            {
                // 抑制结束，触发所有变更的属性通知
                if (_changedProperties.Count > 0)
                {
                    var propertiesToNotify = new List<string>(_changedProperties);
                    _changedProperties.Clear();

                    foreach (var propertyName in propertiesToNotify)
                    {
                        OnPropertyChangedDirect(propertyName);
                    }
                }
            }
            else if (_suppressNotificationDepth < 0)
            {
                // 防御性编程：抑制深度不应该为负
                _suppressNotificationDepth = 0;
            }
        }

        /// <summary>
        /// 直接触发属性变更通知（绕过抑制机制）
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        private void OnPropertyChangedDirect(string? propertyName)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged == null)
                return;

            var args = new PropertyChangedEventArgs(propertyName);
            propertyChanged.Invoke(this, args);
        }

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称（自动获取）</param>
        /// <remarks>
        /// 性能优化版本：
        /// - 正常情况：直接触发事件，不输出日志
        /// - 超阈值情况：输出详细性能分析日志
        /// - 批量更新：在抑制期间记录变更，抑制结束后一次性触发
        /// </remarks>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // 如果正在抑制通知，记录变更但不立即触发
            if (IsSuppressingNotification)
            {
                if (!string.IsNullOrEmpty(propertyName))
                {
                    _changedProperties.Add(propertyName);
                }
                return;
            }

            OnPropertyChangedDirect(propertyName);
        }

        /// <summary>
        /// 触发属性变更通知（带性能监控）
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        /// <param name="enableProfiling">是否启用性能监控</param>
        protected virtual void OnPropertyChangedWithProfiling(string propertyName, bool enableProfiling = true)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged == null)
            {
                return;
            }

            if (!enableProfiling)
            {
                propertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return;
            }

            // 性能监控模式
            var totalStopwatch = Stopwatch.StartNew();
            var targetType = GetType().Name;
            var logSource = GetLogSource() ?? targetType;
            var invocationList = propertyChanged.GetInvocationList();
            var totalSubscribers = invocationList.Length;

            // 直接调用所有订阅者
            var args = new PropertyChangedEventArgs(propertyName);
            propertyChanged.Invoke(this, args);

            totalStopwatch.Stop();
            var totalTime = totalStopwatch.ElapsedMilliseconds;

            // 仅在超阈值时输出详细日志
            if (totalTime > 50) // 保留阈值判断，但简化日志输出
            {
                PluginLogger.Warning($"[性能瓶颈] {targetType}.{propertyName} 耗时 {totalTime}ms", logSource);
            }
        }

        #endregion

        #region SetProperty 方法

        /// <summary>
        /// 调用者信息
        /// </summary>
        private class CallerInfo
        {
            public string? ClassName { get; set; }
            public string? MethodName { get; set; }
            public string? CallStack { get; set; }
        }

        /// <summary>
        /// 通过StackTrace获取调用者信息
        /// </summary>
        private static CallerInfo GetCallerInfo()
        {
            var info = new CallerInfo();
            var stackTrace = new StackTrace(skipFrames: 2);

            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                if (frame == null) continue;

                var method = frame.GetMethod();
                if (method == null) continue;

                if (method.DeclaringType == typeof(ObservableObject) ||
                    method.Name.StartsWith("set_") ||
                    method.Name.StartsWith("get_"))
                {
                    continue;
                }

                info.ClassName = method.DeclaringType?.Name;
                info.MethodName = method.Name;

                info.CallStack = string.Join("\n", stackTrace.GetFrames()
                    .Where(f => f?.GetMethod() != null)
                    .Select(f => $"  at {f?.GetMethod()?.DeclaringType?.FullName}.{f?.GetMethod()?.Name}"));
                break;
            }

            return info;
        }

        /// <summary>
        /// 设置属性值（可选自动记录日志和监控）
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="displayName">显示名称（传入时自动记录日志并启用监控）</param>
        /// <param name="propertyName">属性名（自动获取）</param>
        /// <returns>是否发生了变化</returns>
        protected bool SetProperty<T>(
            ref T field,
            T value,
            string? displayName = null,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            var oldValue = field;
            field = value;

            OnPropertyChanging(propertyName, oldValue, value);

            if (!string.IsNullOrEmpty(displayName))
            {
                LogPropertyChange(displayName, oldValue, value);

                var callerInfo = GetCallerInfo();
                var record = new PropertyChangeRecord
                {
                    Timestamp = DateTime.Now,
                    TargetType = GetType(),
                    PropertyName = propertyName ?? "Unknown",
                    DisplayName = displayName,
                    OldValue = oldValue,
                    NewValue = value,
                    ModifierClass = callerInfo.ClassName,
                    ModifierMethod = callerInfo.MethodName,
                    CallStack = callerInfo.CallStack
                };
                PropertyChangeMonitor.Instance.RecordChange(record);
            }

            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region 扩展点

        /// <summary>
        /// 属性即将变化时的扩展处理（子类可重写）
        /// </summary>
        protected virtual void OnPropertyChanging(string? propertyName, object? oldValue, object? newValue)
        {
        }

        /// <summary>
        /// 记录属性变化日志（子类可重写自定义日志格式）
        /// </summary>
        protected virtual void LogPropertyChange(string displayName, object? oldValue, object? newValue)
        {
            var source = GetLogSource();
            if (!string.IsNullOrEmpty(displayName)) // 仅记录有显示名称的属性变更
            {
                PluginLogger.ParameterChanged(displayName, oldValue, newValue, source);
            }
        }

        /// <summary>
        /// 获取日志来源（子类可重写）
        /// </summary>
        protected virtual string? GetLogSource() => null;

        #endregion
    }
}
