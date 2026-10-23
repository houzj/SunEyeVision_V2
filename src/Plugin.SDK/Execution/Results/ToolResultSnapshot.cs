using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

public sealed class ToolResultSnapshot : ReadOnlyDictionary<string, ToolResults>, IDisposable
{
    private readonly List<IDisposable> _leases;
    internal ToolResultSnapshot(IDictionary<string, ToolResults> results, List<IDisposable> leases) : base(results) => _leases = leases;
    public ToolResultSnapshot(IDictionary<string, ToolResults> results) : base(results)
    {
        _leases = new();
        try { foreach (var result in results.Values) if (result.Resources != null) _leases.Add(result.Resources.Retain()); }
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
