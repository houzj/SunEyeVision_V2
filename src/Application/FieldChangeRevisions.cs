using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Opaque revisions of admitted host-input settings, independent of runtime readback and history payloads.</summary>
internal sealed class FieldChangeRevisions
{
    private FieldSettings _fields;
    private readonly Dictionary<string,Guid> _revisions;
    internal FieldChangeRevisions(FieldSettings fields,IReadOnlyDictionary<string,Guid>? revisions=null)
    {
        _fields=fields;
        _revisions=revisions?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
    }
    internal FieldChangeRevisions Fork(FieldSettings fields)
    {
        var fork=new FieldChangeRevisions(_fields,_revisions);
        fork.Advance(fields);
        return fork;
    }
    internal void Advance(FieldSettings fields)
    {
        foreach(var id in _fields.Declarations.Keys.Union(fields.Declarations.Keys,StringComparer.Ordinal))
            if(!_fields.HasSameSetting(fields,id))_revisions[id]=Guid.NewGuid();
        _fields=fields;
    }
    internal void AdvanceConfiguration(FieldSettings fields)=>Advance(fields.Select(_fields.Declarations.Values));
    internal IReadOnlyDictionary<string,Guid> Capture()=>new ReadOnlyDictionary<string,Guid>(
        _revisions.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal));
}
