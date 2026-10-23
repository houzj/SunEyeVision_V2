using System.Windows.Threading;

namespace SunEyeVision.Tool.Script.Tests;

// WPF process-wide resources must be created and consumed on the same UI thread.
internal static class ScriptTestDispatcher
{
    private static readonly Lazy<Dispatcher> Shared = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            ready.SetResult(dispatcher);
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Script contract UI" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    });

    internal static void Run(Func<Task> action, int seconds)
    {
        Shared.Value.InvokeAsync(action).Task.Unwrap().WaitAsync(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}
