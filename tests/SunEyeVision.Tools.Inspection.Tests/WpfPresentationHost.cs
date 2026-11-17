using System.Windows.Threading;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>
/// 呈现层用例专用单线程 STA 宿主：WPF 的应用级资源字典按线程归属，
/// 若每个用例各起一个 STA 线程会互相争用同一份资源（表现为「另一个线程拥有该对象」），
/// 因此全部呈现层用例统一在同一个 STA 线程上执行。
/// </summary>
internal static class WpfPresentationHost
{
    private static readonly object Gate = new();
    private static Dispatcher? _dispatcher;

    private static Dispatcher Dispatcher
    {
        get
        {
            lock (Gate)
            {
                if (_dispatcher is not null) return _dispatcher;
                var ready = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                    Name = "WPF 呈现层用例线程"
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait(TimeSpan.FromSeconds(30));
                return _dispatcher ?? throw new InvalidOperationException("STA 呈现层线程启动失败。");
            }
        }
    }

    /// <summary>在共享 STA 线程上同步执行；异常原样抛回调用方。</summary>
    internal static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.Invoke(action);
    }
}
