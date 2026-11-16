using System.Collections.Concurrent;

namespace SunEyeVision.Execution;

/// <summary>Execution-versioned result ownership boundary. UI projections never own execution results.</summary>
public sealed class ResultStore<T> where T : class
{
    private readonly ConcurrentDictionary<Guid, T> _results = new();

    public event Action<Guid, T>? Published;
    public event Action<Guid>? Retired;

    public int Count => _results.Count;

    public void Publish(Guid version, T result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _results[version] = result;
        Published?.Invoke(version, result);
    }

    public bool TryGet(Guid version, out T? result) => _results.TryGetValue(version, out result);

    public bool Retire(Guid version)
    {
        var removed = _results.TryRemove(version, out _);
        if (removed) Retired?.Invoke(version);
        return removed;
    }

    public void Clear()
    {
        foreach (var version in _results.Keys.ToArray()) Retire(version);
    }
}
