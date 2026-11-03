using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>Transfers the invocation output resources and directory lease to its result.</summary>
internal sealed class GenerationResourceOwner(IDisposable resources, RuntimeGenerationLease generation) : IDisposable
{
    private sealed record Owned(IDisposable Resources, RuntimeGenerationLease Generation);
    private Owned? _owned = new(resources, generation);
    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref _owned, null);
        if (owned is null) return;
        var failures = new List<Exception>();
        try { owned.Resources.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { owned.Generation.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("Runtime resources and generation cleanup failed.", failures);
    }
}
