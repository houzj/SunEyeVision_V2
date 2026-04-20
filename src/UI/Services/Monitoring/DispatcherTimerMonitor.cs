using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;

namespace SunEyeVision.UI.Services.Monitoring;

/// <summary>
/// DispatcherTimer监控服务 - 用于监控和诊断DispatcherTimer相关的问题
/// </summary>
public class DispatcherTimerMonitor
{
    private static readonly Lazy<DispatcherTimerMonitor> _instance = new(() => new DispatcherTimerMonitor());
    public static DispatcherTimerMonitor Instance => _instance.Value;

    private readonly Dictionary<int, DispatcherTimerInfo> _timers = new();
    private readonly object _lock = new();
    private int _nextTimerId = 1;
    private readonly string _logFilePath;

    private DispatcherTimerMonitor()
    {
        _logFilePath = AppDomain.CurrentDomain.BaseDirectory + "dispatcher_timer_monitor.log";
        InitializeMonitoring();
    }

    /// <summary>
    /// 初始化监控
    /// </summary>
    private void InitializeMonitoring()
    {
        LogToFile("\n\n" + new string('=', 80));
        LogToFile("DispatcherTimerMonitor 初始化: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        LogToFile("监控文件: " + _logFilePath);
        LogToFile(new string('=', 80));
    }

    /// <summary>
    /// 注册一个DispatcherTimer
    /// </summary>
    public int RegisterTimer(DispatcherTimer timer, string? ownerName = null, string? timerName = null)
    {
        lock (_lock)
        {
            int timerId = _nextTimerId++;
            var info = new DispatcherTimerInfo
            {
                Id = timerId,
                Timer = timer,
                OwnerName = ownerName ?? GetCallingMethodName(),
                TimerName = timerName ?? "Timer_" + timerId,
                Interval = timer.Interval,
                IsEnabled = timer.IsEnabled,
                CreatedAt = DateTime.Now
            };

            _timers[timerId] = info;

            // 监控Tick事件订阅
            MonitorTickEvent(timer, info);

            LogToFile("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] Timer注册: ID=" + timerId + ", 名称=" + info.TimerName + ", 所有者=" + info.OwnerName + ", 间隔=" + timer.Interval.TotalSeconds + "s, 启用=" + timer.IsEnabled);

            return timerId;
        }
    }

    /// <summary>
    /// 监控Tick事件订阅
    /// </summary>
    private void MonitorTickEvent(DispatcherTimer timer, DispatcherTimerInfo info)
    {
        // 尝试使用反射获取Tick事件处理器
        try
        {
            var eventField = typeof(DispatcherTimer).GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            if (eventField == null)
            {
                LogToFile("[警告] 无法访问Tick事件字段，Timer ID=" + info.Id);
                return;
            }

            var eventDelegate = eventField.GetValue(timer) as Delegate;
            if (eventDelegate != null)
            {
                info.TickHandlerInfo = AnalyzeEventHandler(eventDelegate);
                LogToFile("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] Tick事件处理器已注册: Timer ID=" + info.Id);
                LogToFile("  - 处理器数量: " + eventDelegate.GetInvocationList().Length);
                foreach (var handler in eventDelegate.GetInvocationList())
                {
                    LogToFile("  - 方法: " + (handler.Method?.DeclaringType?.Name ?? "Unknown") + "." + (handler.Method?.Name ?? "Unknown"));
                    LogToFile("  - 签名: " + (handler.Method?.ToString() ?? "Unknown"));
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile("[错误] 监控Tick事件失败: Timer ID=" + info.Id + ", 错误=" + ex.Message);
        }
    }

    /// <summary>
    /// 分析事件处理器信息
    /// </summary>
    private EventHandlerInfo AnalyzeEventHandler(Delegate eventDelegate)
    {
        var handlerInfo = new EventHandlerInfo();

        try
        {
            var invocationList = eventDelegate.GetInvocationList();
            handlerInfo.HandlerCount = invocationList.Length;
            handlerInfo.Handlers = new List<HandlerDetail>();

            foreach (var handler in invocationList)
            {
                var detail = new HandlerDetail
                {
                    MethodName = handler.Method?.Name ?? "Unknown",
                    DeclaringType = handler.Method?.DeclaringType?.FullName ?? "Unknown",
                    Signature = handler.Method?.ToString() ?? "Unknown",
                    ParameterCount = handler.Method?.GetParameters().Length ?? 0,
                    Parameters = handler.Method?.GetParameters()
                        .Select(p => p.ParameterType.Name + " " + p.Name)
                        .ToArray() ?? Array.Empty<string>()
                };

                // 检查签名是否正确
                if (detail.ParameterCount != 2)
                {
                    handlerInfo.IsSignatureValid = false;
                    handlerInfo.ValidationError = "参数数量不正确: 期望2个(object sender, EventArgs e), 实际" + detail.ParameterCount + "个";
                }
                else
                {
                    var parameters = handler.Method?.GetParameters();
                    if (parameters != null && parameters.Length == 2)
                    {
                        bool firstParamOk = parameters[0].ParameterType == typeof(object);
                        bool secondParamOk = parameters[1].ParameterType == typeof(EventArgs) ||
                                           parameters[1].ParameterType == typeof(DispatcherEventArgs);

                        if (!firstParamOk || !secondParamOk)
                        {
                            handlerInfo.IsSignatureValid = false;
                            handlerInfo.ValidationError = "参数类型不正确: 期望(object, EventArgs/DispatcherEventArgs), 实际(" + parameters[0].ParameterType.Name + ", " + parameters[1].ParameterType.Name + ")";
                        }
                    }
                }

                handlerInfo.Handlers.Add(detail);
            }
        }
        catch (Exception ex)
        {
            handlerInfo.ValidationError = "分析失败: " + ex.Message;
        }

        return handlerInfo;
    }

    /// <summary>
    /// 记录Timer状态变更
    /// </summary>
    public void LogTimerStateChange(int timerId, string action, string? details = null)
    {
        lock (_lock)
        {
            if (_timers.TryGetValue(timerId, out var info))
            {
                LogToFile("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] Timer状态变更: ID=" + timerId + ", 动作=" + action + (details != null ? ", 详情=" + details : ""));
                info.LastStateChangeAt = DateTime.Now;
            }
        }
    }

    /// <summary>
    /// 记录Timer Tick事件触发
    /// </summary>
    public void LogTimerTick(int timerId)
    {
        lock (_lock)
        {
            if (_timers.TryGetValue(timerId, out var info))
            {
                info.TickCount++;
                info.LastTickAt = DateTime.Now;

                if (info.TickCount <= 5) // 只记录前5次，避免日志过多
                {
                    LogToFile("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] Timer触发: ID=" + timerId + ", 次数=" + info.TickCount);
                }
            }
        }
    }

    /// <summary>
    /// 获取所有Timer信息
    /// </summary>
    public string GetAllTimersInfo()
    {
        lock (_lock)
        {
            var report = "\n\n=== DispatcherTimer 监控报告 ===\n";
            report += "生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\n";
            report += "Timer总数: " + _timers.Count + "\n";
            report += new string('-', 80) + "\n";

            foreach (var kvp in _timers.OrderBy(x => x.Key))
            {
                var info = kvp.Value;
                report += "\nTimer ID: " + info.Id + "\n";
                report += "  名称: " + info.TimerName + "\n";
                report += "  所有者: " + info.OwnerName + "\n";
                report += "  间隔: " + info.Interval.TotalSeconds + "秒\n";
                report += "  启用: " + info.IsEnabled + "\n";
                report += "  创建时间: " + info.CreatedAt.ToString("HH:mm:ss.fff") + "\n";
                report += "  Tick次数: " + info.TickCount + "\n";
                report += "  最后Tick: " + info.LastTickAt.ToString("HH:mm:ss.fff") + "\n";
                report += "  最后状态变更: " + info.LastStateChangeAt.ToString("HH:mm:ss.fff") + "\n";

                if (info.TickHandlerInfo != null)
                {
                    report += "  Tick事件处理器:\n";
                    report += "    处理器数量: " + info.TickHandlerInfo.HandlerCount + "\n";
                    if (!info.TickHandlerInfo.IsSignatureValid)
                    {
                        report += "    [错误] 签名验证失败: " + info.TickHandlerInfo.ValidationError + "\n";
                    }

                    if (info.TickHandlerInfo.Handlers != null)
                    {
                        foreach (var handler in info.TickHandlerInfo.Handlers)
                        {
                            report += "    - 方法: " + handler.DeclaringType + "." + handler.MethodName + "\n";
                            report += "      签名: " + handler.Signature + "\n";
                            report += "      参数: " + string.Join(", ", handler.Parameters) + "\n";
                        }
                    }
                }
                else
                {
                    report += "  Tick事件处理器: 未注册或无法访问\n";
                }
            }

            report += "\n" + new string('=', 80) + "\n";
            return report;
        }
    }

    /// <summary>
    /// 获取调用方法名
    /// </summary>
    private string GetCallingMethodName()
    {
        try
        {
            var stackTrace = new StackTrace(3); // 跳过当前方法、RegisterTimer、调用者
            var frame = stackTrace.GetFrame(0);
            return frame?.GetMethod()?.DeclaringType?.FullName ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// 写入日志文件
    /// </summary>
    private void LogToFile(string message)
    {
        try
        {
            System.IO.File.AppendAllText(_logFilePath, message + "\n");
        }
        catch
        {
            // 忽略日志写入失败
        }
    }

    /// <summary>
    /// 打印所有Timer信息到Debug输出
    /// </summary>
    public void PrintAllTimersInfo()
    {
        var report = GetAllTimersInfo();
LogToFile(report);
    }

    /// <summary>
    /// DispatcherTimer信息
    /// </summary>
    private class DispatcherTimerInfo
    {
        public int Id { get; set; }
        public DispatcherTimer Timer { get; set; } = null!;
        public string OwnerName { get; set; } = null!;
        public string TimerName { get; set; } = null!;
        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastTickAt { get; set; }
        public DateTime LastStateChangeAt { get; set; }
        public int TickCount { get; set; }
        public EventHandlerInfo? TickHandlerInfo { get; set; }
    }

    /// <summary>
    /// 事件处理器信息
    /// </summary>
    private class EventHandlerInfo
    {
        public int HandlerCount { get; set; }
        public List<HandlerDetail>? Handlers { get; set; }
        public bool IsSignatureValid { get; set; } = true;
        public string? ValidationError { get; set; }
    }

    /// <summary>
    /// 处理器详情
    /// </summary>
    private class HandlerDetail
    {
        public string MethodName { get; set; } = null!;
        public string DeclaringType { get; set; } = null!;
        public string Signature { get; set; } = null!;
        public int ParameterCount { get; set; }
        public string[] Parameters { get; set; } = Array.Empty<string>();
    }
}
