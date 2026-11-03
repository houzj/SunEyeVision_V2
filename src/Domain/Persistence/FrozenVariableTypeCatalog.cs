using System.Collections.ObjectModel;
namespace SunEyeVision.Domain.Persistence;

/// <summary>Detached variable vocabulary captured from the admitted host, without live SDK or runtime callbacks.</summary>
public sealed class FrozenVariableTypeCatalog
{
    private readonly IReadOnlyDictionary<string,string> _types;
    public FrozenVariableTypeCatalog(IEnumerable<KeyValuePair<string,string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (name,canonical) in entries)
        {
            if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(canonical))throw new ArgumentException("Variable vocabulary identities must be nonempty.");
            values.Add(name,canonical);
        }
        _types=new ReadOnlyDictionary<string,string>(values);
    }
    public string CanonicalType(string name) => _types.TryGetValue(name,out var canonical) ? canonical : throw new System.Text.Json.JsonException("Unknown variable type: "+name);
}
