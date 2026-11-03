using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Execution;

/// <summary>Owns the resource keys of one root execution until each invocation has drained.</summary>
internal sealed class ExecutionResourceLeaseManager
{
    private readonly object _gate = new();
    private readonly HashSet<string> _leased = new(StringComparer.Ordinal);

    internal bool TryAcquire(string workflowId, string nodeId, ToolCapabilities capabilities, out IDisposable? lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(capabilities);
        // The admitted Runtime path previously resolves every profile to ResourceExclusive.
        // Preserve its node-local fallback and cross-workflow resource-key exclusion.
        var declared = capabilities.ResourceKeys.Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal).Select(key => "resource:" + key).ToArray();
        var keys = declared.Length == 0 ? new[] { $"node:{workflowId}:{nodeId}" } : declared;
        lock (_gate)
        {
            if (keys.Any(_leased.Contains)) { lease = null; return false; }
            foreach (var key in keys) _leased.Add(key);
            lease = new Lease(this, keys);
            return true;
        }
    }

    private sealed class Lease(ExecutionResourceLeaseManager owner, string[] keys) : IDisposable
    {
        private ExecutionResourceLeaseManager? _owner = owner;
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            if (current is null) return;
            lock (current._gate) foreach (var key in keys) current._leased.Remove(key);
        }
    }
}
