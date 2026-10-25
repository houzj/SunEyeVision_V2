using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Regions;

public sealed record RoiEditItem(string Group,Guid Id,string Name,RecordValue Geometry,bool Enabled=true,bool Visible=true);
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
