using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.UI.Services.Performance
{
    /// <summary>
    /// 性能日志记录器
    /// </summary>
    public class PerformanceLogger
    {
        private readonly string _category;
        private static readonly object _lock = new object();
        private static int _logCounter = 0;

        public PerformanceLogger(string category)
        {
            _category = category;
        }

        /// <summary>
        /// 记录操作耗时
        /// </summary>
        public void LogOperation(string operation, TimeSpan elapsed, string details = "")
        {
            int id = Interlocked.Increment(ref _logCounter);
            var logMsg = $"[{id:D4}] [{_category}] {operation} - 耗时: {elapsed.TotalMilliseconds:F2}ms";
            if (!string.IsNullOrEmpty(details))
            {
                logMsg += $" | {details}";
            }
}

        /// <summary>
        /// 执行并计时
        /// </summary>
        public T ExecuteAndTime<T>(string operation, Func<T> func, string details = "")
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = func();
                sw.Stop();
                LogOperation(operation, sw.Elapsed, details);
                return result;
            }
            catch
            {
                sw.Stop();
                LogOperation($"{operation} (异常)", sw.Elapsed, details);
                throw;
            }
        }

        /// <summary>
        /// 异步执行并计时
        /// </summary>
        public async Task<T> ExecuteAndTimeAsync<T>(string operation, Func<Task<T>> func, string details = "")
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await func();
                sw.Stop();
                LogOperation(operation, sw.Elapsed, details);
                return result;
            }
            catch
            {
                sw.Stop();
                LogOperation($"{operation} (异常)", sw.Elapsed, details);
                throw;
            }
        }

        /// <summary>
        /// 重置计数器
        /// </summary>
        public static void ResetCounter()
        {
            _logCounter = 0;
        }
    }
}
