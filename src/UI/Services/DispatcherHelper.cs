using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// Dispatcher安全包装器 - 提供安全的Dispatcher调用和诊断功能
    /// </summary>
    public static class DispatcherHelper
    {
        /// <summary>
        /// 安全的BeginInvoke调用 - 自动验证参数顺序和捕获异常
        /// </summary>
        /// <param name="dispatcher">Dispatcher实例</param>
        /// <param name="action">要执行的Action</param>
        /// <param name="priority">调度优先级（默认：Normal）</param>
        public static void SafeBeginInvoke(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                // 正确的参数顺序：Action在前，DispatcherPriority在后
                dispatcher.BeginInvoke(action, priority);
            }
            catch (Exception ex)
            {
                // 尝试不带优先级参数的调用
                try
                {
                    dispatcher.BeginInvoke(action);
}
                catch (Exception ex2)
                {
throw;
                }
            }
        }

        /// <summary>
        /// 安全的Invoke调用 - 自动验证参数顺序和捕获异常
        /// </summary>
        /// <param name="dispatcher">Dispatcher实例</param>
        /// <param name="action">要执行的Action</param>
        /// <param name="priority">调度优先级（默认：Normal）</param>
        public static void SafeInvoke(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                // 正确的参数顺序：Action在前，DispatcherPriority在后
                dispatcher.Invoke(action, priority);
            }
            catch (Exception ex)
            {
                // 尝试不带优先级参数的调用
                try
                {
                    dispatcher.Invoke(action);
}
                catch (Exception ex2)
                {
throw;
                }
            }
        }

        /// <summary>
        /// 安全的BeginInvoke调用（带优先级）- 使用扩展方法避免参数顺序错误
        /// </summary>
        /// <param name="dispatcher">Dispatcher实例</param>
        /// <param name="priority">调度优先级</param>
        /// <param name="action">要执行的Action</param>
        /// <remarks>
        /// 使用命名参数避免参数顺序混淆
        /// 例如: _dispatcher.SafeBeginInvokeWithPriority(priority: DispatcherPriority.ContextIdle, action: () => { ... })
        /// </remarks>
        public static void SafeBeginInvokeWithPriority(this Dispatcher dispatcher, DispatcherPriority priority, Action action)
        {
            SafeBeginInvoke(dispatcher, action, priority);
        }

        /// <summary>
        /// 检查Dispatcher调用是否安全（在正确的线程上）
        /// </summary>
        /// <param name="dispatcher">Dispatcher实例</param>
        /// <returns>如果在Dispatcher线程上返回true，否则返回false</returns>
        public static bool IsSafeToInvoke(this Dispatcher dispatcher)
        {
            return dispatcher != null && dispatcher.CheckAccess();
        }

        /// <summary>
        /// 安全执行Action - 如果在Dispatcher线程上直接执行，否则使用BeginInvoke
        /// </summary>
        /// <param name="dispatcher">Dispatcher实例</param>
        /// <param name="action">要执行的Action</param>
        /// <param name="priority">调度优先级（默认：Background）</param>
        public static void SafeExecute(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (dispatcher.CheckAccess())
            {
                // 在正确的线程上，直接执行
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
            else
            {
                // 不在正确的线程上，使用BeginInvoke
                SafeBeginInvoke(dispatcher, action, priority);
            }
        }
    }
}
