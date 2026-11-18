using System.Windows.Threading;

namespace SunEyeVision.Tools.ClassifyCalib.Tests;

/// <summary>
/// 呈现层用例专用单线程 STA 宿主：WPF 应用级资源字典按线程归属，全部呈现层用例在同一 STA 线程执行，
/// 避免多 STA 线程争用同一份资源（"另一个线程拥有该对象"）。
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
                    Name = "WPF 分类类呈现层用例线程"
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait(TimeSpan.FromSeconds(30));
                return _dispatcher ?? throw new InvalidOperationException("STA 呈现层线程启动失败。");
            }
        }
    }

    internal static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.Invoke(action);
    }
}
