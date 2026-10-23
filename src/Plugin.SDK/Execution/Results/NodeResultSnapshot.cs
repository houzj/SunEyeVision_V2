using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

public sealed class NodeResultSnapshot : IDisposable
{
    public IReadOnlyDictionary<string, NodeExecutionResult> Results { get; }
    private readonly List<IDisposable> _leases = new();
    public NodeResultSnapshot(IReadOnlyDictionary<string, NodeExecutionResult> results)
    {
        try
        {
            foreach (var result in results.Values.Select(value => value.Result).Distinct())
                if (result?.Resources != null) _leases.Add(result.Resources.Retain());
            Results = new Dictionary<string, NodeExecutionResult>(results);
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        List<Exception>? errors = null;
        foreach (var lease in _leases)
            try { lease.Dispose(); } catch (Exception ex) { (errors ??= new()).Add(ex); }
        _leases.Clear();
        if (errors != null) throw new AggregateException(errors);
    }
}
