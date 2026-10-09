using System;
using System.Threading;

namespace SunEyeVision.UI.Services;

/// <summary>协调程序启动与卸载；运行标记必须保持到进程结束，不在窗口关闭时释放。</summary>
public static class ApplicationLifetimeGate
{
    public const string RunningMutexName = @"Global\SunEyeVision.Running";
    public const string MaintenanceMutexName = @"Global\SunEyeVision.Maintenance";

    /// <summary>在维护锁保护下登记运行标记。返回 null 表示维护中，不允许启动。</summary>
    public static IDisposable? TryRegister(string runningMutexName = RunningMutexName,
        string maintenanceMutexName = MaintenanceMutexName)
    {
        using var admission = new Mutex(false, maintenanceMutexName);
        var acquired = false;
        try
        {
            try { acquired = admission.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return null;
            return new Mutex(false, runningMutexName);
        }
        finally
        {
            if (acquired) admission.ReleaseMutex();
        }
    }
}
