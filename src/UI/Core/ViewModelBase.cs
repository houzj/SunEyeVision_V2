using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace SunEyeVision.UI.Core
{
    /// <summary>
    /// UI层 ViewModel 基类 - 继承 SDK 的 ObservableObject
    /// </summary>
    /// <remarks>
    /// 所有属性变化通知功能继承自 Plugin.SDK.Models.ObservableObject。
    /// 使用 SetProperty 方法设置属性值，支持自动日志记录：
    ///
    /// <code>
    /// // 不记录日志
    /// public string Name
    /// {
    ///     get => _name;
    ///     set => SetProperty(ref _name, value);
    /// }
    ///
    /// // 自动记录日志
    /// public int Threshold
    /// {
    ///     get => _threshold;
    ///     set => SetProperty(ref _threshold, value, "阈值");
    /// }
    /// </code>
    ///
    /// 提供便捷的日志方法（LogInfo、LogSuccess、LogWarning、LogError）
    /// 提供线程安全的集合操作方法（避免 Collection modified 异常）
    /// </remarks>
    public abstract class ViewModelBase : ObservableObject
    {
        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="source">来源标识</param>
        protected void LogInfo(string message, string? source = null, LogTarget target = LogTarget.UI)
        {
            var logger = VisionLogger.Instance;
            logger.Info(message, source ?? GetLogSource(), target);
        }

        protected void LogSuccess(string message, string? source = null, LogTarget target = LogTarget.UI)
        {
            var logger = VisionLogger.Instance;
            logger.Success(message, source ?? GetLogSource(), target);
        }

        public void LogWarning(string message, string? source = null, LogTarget target = LogTarget.UI)
        {
            var logger = VisionLogger.Instance;
            logger.Warning(message, source ?? GetLogSource(), target);
        }

        protected void LogError(string message, string? source = null, Exception? exception = null, LogTarget target = LogTarget.UI)
        {
            var logger = VisionLogger.Instance;
            logger.Error(message, source ?? GetLogSource(), exception, target);
        }

        /// <summary>
        /// 注册命令异常处理（在构造函数中调用）
        /// </summary>
        /// <param name="command">要注册异常处理的命令</param>
        protected void RegisterCommandExceptionHandler(RelayCommand command)
        {
            command.ExecutionFailed += (sender, e) =>
            {
                LogError($"命令执行失败 [{e.CommandName}]: {e.Exception.Message}", e.CommandName, e.Exception);
            };
        }

        #region 集合操作辅助方法

        /// <summary>
        /// 线程安全的集合更新方法
        /// </summary>
        /// <typeparam name="T">集合元素类型</typeparam>
        /// <param name="collection">要更新的集合</param>
        /// <param name="updateAction">更新操作</param>
        /// <remarks>
        /// 使用 Dispatcher 保护集合操作，避免 UI 枚举时修改异常。
        /// 适用于批量操作，减少 UI 更新次数。
        /// </remarks>
        protected void UpdateCollection<T>(ObservableCollection<T> collection, Action<ObservableCollection<T>> updateAction)
        {
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() => updateAction(collection));
            }
            else
            {
                // 非主线程或无 Dispatcher 的情况（设计时等）
                updateAction(collection);
            }
        }

        /// <summary>
        /// 线程安全地添加元素到集合
        /// </summary>
        protected void AddToCollection<T>(ObservableCollection<T> collection, T item)
        {
            UpdateCollection(collection, c => c.Add(item));
        }

        /// <summary>
        /// 线程安全地从集合移除元素
        /// </summary>
        protected bool RemoveFromCollection<T>(ObservableCollection<T> collection, T item)
        {
            bool removed = false;
            UpdateCollection(collection, c =>
            {
                removed = c.Remove(item);
            });
            return removed;
        }

        /// <summary>
        /// 线程安全地清空集合
        /// </summary>
        protected void ClearCollection<T>(ObservableCollection<T> collection)
        {
            UpdateCollection(collection, c => c.Clear());
        }

        /// <summary>
        /// 线程安全地批量添加元素
        /// </summary>
        protected void AddRangeToCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
        {
            UpdateCollection(collection, c =>
            {
                foreach (var item in items)
                {
                    c.Add(item);
                }
            });
        }

        /// <summary>
        /// 线程安全地批量移除元素
        /// </summary>
        protected void RemoveRangeFromCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
        {
            UpdateCollection(collection, c =>
            {
                foreach (var item in items)
                {
                    c.Remove(item);
                }
            });
        }

        #endregion
    }
}
