using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;

namespace SunEyeVision.Plugin.Wpf.Regions;

public sealed record RoiEditItem(string Group,Guid Id,string Name,RecordValue Geometry,bool Enabled=true,bool Visible=true);
public sealed record RoiEditTarget(string Group,Guid Id);
/// <summary>Closed requests: collection and history operations never carry fictitious geometry.</summary>
public abstract record RoiEditProposal
{
    private RoiEditProposal(){}
    public sealed record Create(string Group,RecordValue Geometry):RoiEditProposal;
    public sealed record Replace(RoiEditTarget Target,RecordValue Geometry):RoiEditProposal;
    public sealed record Delete:RoiEditProposal
    {
        public IReadOnlyList<RoiEditTarget> Targets {get;}
        public Delete(IEnumerable<RoiEditTarget> targets)
        {
            ArgumentNullException.ThrowIfNull(targets);
            var copy=targets.ToArray();
            if(copy.Length==0||copy.Any(target=>target is null||string.IsNullOrWhiteSpace(target.Group)||target.Id==Guid.Empty)
                ||copy.Distinct().Count()!=copy.Length)throw new ArgumentException("删除目标必须非空、身份有效且不重复。",nameof(targets));
            Targets=Array.AsReadOnly(copy);
        }
    }
    public sealed record Undo:RoiEditProposal;
    public sealed record Redo:RoiEditProposal;
}
/// <summary>Selection is independent of the affected objects; deleted objects cannot be selected.</summary>
public abstract record RoiEditSelection
{
    private RoiEditSelection(){}
    public sealed record Keep:RoiEditSelection;
    public sealed record Clear:RoiEditSelection;
    public sealed record Select(RoiEditTarget Target):RoiEditSelection;
}
public sealed record RoiHistoryAvailability(bool CanUndo,bool CanRedo);
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
