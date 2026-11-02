using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Opaque, session-local raw input. Never a saved geometry or a lease on the old image.</summary>
public sealed class NativeRoiPendingInput
{
    internal NativeRoiEditor Owner {get;}
    public string Group {get;}
    public Guid Id {get;}
    public RoiEditStamp OriginalStamp {get;}
    internal RecordValue Geometry {get;}
    internal string?[] Raw {get;}
    internal IReadOnlyDictionary<(int Index,bool X),string> PolygonRaw {get;}
    internal NativeRoiPendingInput(NativeRoiEditor owner,NativeRoiSession session,NativeRoiProjection item,string?[] raw,IReadOnlyDictionary<(int,bool),string> polygonRaw)
    {Owner=owner;Group=item.Group;Id=item.Id;OriginalStamp=session.Stamp;Geometry=item.AcceptedGeometry;Raw=raw.ToArray();PolygonRaw=polygonRaw;}
}
