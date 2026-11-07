using System;
using System.Diagnostics;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.UI.Services.Editing;

internal static class ToolWindowAssemblyTrace
{
    internal static IDisposable Begin(string stage, string identity)
        => new Scope(stage, identity);

    internal static void Event(string stage, string identity)
        => PluginLogger.Info($"ToolWindow stage={stage} identity={identity}", nameof(ToolWindowAssemblyTrace));

    private sealed class Scope(string stage, string identity) : IDisposable
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _completed;

        public void Dispose()
        {
            if (_completed) return;
            _completed = true;
            PluginLogger.Info($"ToolWindow stage={stage} identity={identity} elapsedMs={_clock.Elapsed.TotalMilliseconds:F1}", nameof(ToolWindowAssemblyTrace));
        }
    }
}
