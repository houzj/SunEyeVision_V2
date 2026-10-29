using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>Only immutable geometry is copied; neither the image nor its display lease is retained.</summary>
public sealed record RoiGeometryClipboard(Guid ScopeId,ImageFrameAddress Frame,string Group,string Name,RecordValue Geometry);

public sealed partial class NativeRoiSession
{
    private bool _queryingHistory;
    private bool _queryingCoverage;
    private bool CanSetGroupCoverage(string group)
    {
        if(_queryingCoverage||!IsCurrent||_canSetCoverage is null||!_groups.Any(state=>state.Group==group))return false;
        var stamp=_stamp;var generation=_groupGeneration;_queryingCoverage=true;
        try{return _canSetCoverage(group)&&IsCurrent&&stamp==_stamp&&generation==_groupGeneration&&DrawingGroup==group;}
        finally{_queryingCoverage=false;}
    }
    public bool TrySetCoverage(RoiCoverage coverage)
    {
        if(coverage is not (RoiCoverage.None or RoiCoverage.FullDomain))throw new ArgumentOutOfRangeException(nameof(coverage));
        if(!CommandReady||!CanSetGroupCoverage(DrawingGroup))return false;
        var group=DrawingGroup;var generation=_groupGeneration;_requesting=true;
        try
        {
            if(!PrepareCommand(out var current)||generation!=_groupGeneration||!CanSetGroupCoverage(group)||current.Stamp!=Stamp)return false;
            if(current.Groups.Single(state=>state.Group==group).Coverage==coverage)
            {Error="";Notify();return true;}
            return AcceptCommand(current,new RoiEditProposal.SetCoverage(group,coverage));
        }
        finally{_requesting=false;}
    }
    private bool CommandReady=>IsCurrent&&!_committing&&!_reading&&!_refreshing&&!_selecting&&!_requesting;
    public bool CanCopy(NativeRoiProjection item)=>CommandReady&&Owns(item);
    public bool CanDelete(IReadOnlyList<NativeRoiProjection> items)=>CommandReady&&items.Count>0&&items.Distinct().Count()==items.Count&&items.All(Owns);
    public RoiHistoryAvailability History
    {
        get
        {
            return CommandReady?ReadHistory():new(false,false);
        }
    }
    private RoiHistoryAvailability ReadHistory()
    {
        if(_queryingHistory||!IsCurrent)return new(false,false);
        var stamp=_stamp;_queryingHistory=true;
        try{var value=_history();return IsCurrent&&stamp==_stamp?value:new(false,false);}
        finally{_queryingHistory=false;}
    }
    private bool PrepareCommand(out RoiEditSnapshot snapshot)
    {
        snapshot=null!;
        if(!PrepareEditor()||!IsCurrent)return false;
        var current=Capture(ReadCurrent());
        if(!IsCurrent)return false;
        if(current.Stamp!=Stamp){Install(current);return false;}
        var stamp=Stamp;CancelPending();
        if(!IsCurrent||Stamp!=stamp)return false;
        var after=Capture(ReadCurrent());
        if(!IsCurrent)return false;
        if(after.Stamp!=stamp){Install(after);return false;}
        if(!SameItems(current.Items,after.Items)||!SameGroups(current.Groups,after.Groups))
        {_invalid=true;Error="取消回调改变区域内容时未递增宿主版本。";NativeRoiCallbacks.Run([CancelPending,Notify]);return false;}
        snapshot=after;return true;
    }
    public bool TryCopy(NativeRoiProjection item,out RoiGeometryClipboard? clipboard)
    {
        clipboard=null;if(!CanCopy(item))return false;
        _requesting=true;
        try
        {
            if(!PrepareCommand(out var current)||!Owns(item))return false;
            var row=current.Items.SingleOrDefault(row=>row.Group==item.Group&&row.Id==item.Id);
            if(row is null||!item.Matches(row))return false;
            clipboard=new(current.Stamp.ScopeId,current.Stamp.Frame,row.Group,row.Name,row.Geometry);return true;
        }
        finally{_requesting=false;}
    }
    public bool TryDelete(IReadOnlyList<NativeRoiProjection> items,Func<bool>? isCallerCurrent=null)
    {
        var targets=items.ToArray();if(!CanDelete(targets))return false;
        _requesting=true;
        try
        {
            if(!PrepareCommand(out var current)||!targets.All(Owns))return false;
            if(targets.Any(item=>!current.Items.Any(row=>row.Group==item.Group&&row.Id==item.Id&&item.Matches(row))))return false;
            if(!IsCurrent||current.Stamp!=Stamp||!targets.All(Owns))return false;
            if(isCallerCurrent?.Invoke()==false||current.Stamp!=Stamp)return false;
            return AcceptCommand(current,new RoiEditProposal.Delete(targets.Select(item=>new RoiEditTarget(item.Group,item.Id))));
        }
        finally{_requesting=false;}
    }
    public bool CanPaste(RoiGeometryClipboard? clipboard)=>CommandReady&&clipboard is not null&&clipboard.ScopeId==Stamp.ScopeId&&clipboard.Frame==Frame&&CanDrawGroup(DrawingGroup);
    public bool TryPaste(RoiGeometryClipboard clipboard,Point2 pixelAnchor,Func<bool>? isCallerCurrent=null)
    {
        if(!CanPaste(clipboard)||!double.IsFinite(pixelAnchor.X)||!double.IsFinite(pixelAnchor.Y))return false;
        var group=DrawingGroup;var generation=_groupGeneration;_requesting=true;
        try
        {
            if(!PrepareCommand(out var current)||generation!=_groupGeneration||!CanDrawGroup(group)||clipboard.ScopeId!=current.Stamp.ScopeId||clipboard.Frame!=Frame)return false;
            var shape=clipboard.Geometry;var center=RoiGeometryEditing.Center(shape);
            if(shape.Get<string>("kind")=="polygon")
            {
                var points=RoiGeometryEditing.Vertices(shape);
                center=new((points.Min(point=>point.X)+points.Max(point=>point.X))/2,(points.Min(point=>point.Y)+points.Max(point=>point.Y))/2);
            }
            var geometry=RoiGeometryEditing.Translate(shape,pixelAnchor.X-center.X,pixelAnchor.Y-center.Y);
            ValidateNativeGeometry(geometry);
            if(!IsCurrent||current.Stamp!=Stamp||generation!=_groupGeneration)return false;
            if(isCallerCurrent?.Invoke()==false||current.Stamp!=Stamp||generation!=_groupGeneration)return false;
            return AcceptCommand(current,new RoiEditProposal.Create(group,geometry));
        }
        finally{_requesting=false;}
    }
    public bool TryUndo(Func<bool>? isCallerCurrent=null)=>TryHistory(false,isCallerCurrent);
    public bool TryRedo(Func<bool>? isCallerCurrent=null)=>TryHistory(true,isCallerCurrent);
    private bool TryHistory(bool redo,Func<bool>? isCallerCurrent)
    {
        var available=History;if(redo?!available.CanRedo:!available.CanUndo)return false;
        _requesting=true;
        try
        {
            if(!PrepareCommand(out var current))return false;
            available=ReadHistory();
            if(!IsCurrent||current.Stamp!=Stamp||(redo?!available.CanRedo:!available.CanUndo))return false;
            if(isCallerCurrent?.Invoke()==false||current.Stamp!=Stamp)return false;
            return AcceptCommand(current,redo?new RoiEditProposal.Redo():new RoiEditProposal.Undo());
        }
        finally{_requesting=false;}
    }
    private bool AcceptCommand(RoiEditSnapshot current,RoiEditProposal proposal)
    {
        _committing=true;
        try
        {
            var result=_accept(current.Stamp,proposal);
            return PublishAcceptance(result,()=>
            {
                if(!IsCurrent)return;
                var installed=ValidateAcceptance(current,proposal,result);
                Error=result.Accepted?"":result.Error??"宿主拒绝区域操作。";
                var unchanged=installed.Stamp==_stamp;
                InstallAfterCommit(installed,result.Selection);
                if(unchanged)Notify();
            });
        }
        finally{_committing=false;}
    }
}
