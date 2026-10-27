using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;

namespace SunEyeVision.Plugin.Wpf.Regions;

public sealed record RoiEditItem(string Group,Guid Id,string Name,RecordValue Geometry,bool Enabled=true,bool Visible=true);
public enum RoiEditOperation { Create,Replace }
public sealed record RoiEditTarget(string Group,Guid Id);
public sealed record RoiEditProposal(RoiEditOperation Operation,string Group,Guid? TargetId,RecordValue Geometry);
/// <summary>Single-use request fixed after pending parameter edits; a host alone assigns the final object identity.</summary>
public sealed class RoiDrawRequest
{
    internal RoiDrawRequest(RoiEditStamp stamp,string group,string kind){Id=Guid.NewGuid();Stamp=stamp;Group=group;Kind=kind;}
    public Guid Id {get;}
    public RoiEditStamp Stamp {get;}
    public string Group {get;}
    public string Kind {get;}
}
public sealed record RoiGeometryChange(string Group,Guid? Id,RecordValue Geometry);
public enum RoiEditAction { Select, Move, Resize, Rotate }

/// <summary>Restricted viewport capability. No canvas, image ownership, result clearing or execution access.</summary>
public interface IRoiEditViewport
{
    bool HasPendingGesture {get;}
    event Action<RoiGeometryChange>? GeometryChanged;
    event Action<string,Guid?>? SelectionChanged;
    event Action<string>? StatusChanged;
    bool BeginDraw(string group,string kind,out string error);
    void Select(string group,Guid? id);
    void SetAction(RoiEditAction action);
    void CancelGesture();
}
