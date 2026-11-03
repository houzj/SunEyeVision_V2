using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>Host-owned private edit root, monotonic revision and exact frozen physical frame.</summary>
public sealed record RoiEditStamp(Guid ScopeId,long Revision,ImageFrameAddress Frame);
public sealed record RoiEditSnapshot(RoiEditStamp Stamp,IReadOnlyList<RoiEditItem> Items,IReadOnlyList<RoiGroupState> Groups);
/// <summary>The host atomically compares the expected stamp and returns the installed snapshot.
/// It must return acceptance before publishing fallible observer notifications.</summary>
public sealed record RoiEditAcceptance(bool Accepted,RoiEditSnapshot Snapshot,
    IReadOnlyList<RoiEditTarget> Affected,RoiEditSelection Selection,string? Error=null,Action? Publish=null)
{
    public static RoiEditAcceptance Reject(RoiEditSnapshot snapshot,string error)=>new(false,snapshot,Array.Empty<RoiEditTarget>(),new RoiEditSelection.Keep(),error);
    internal static IReadOnlyList<RoiEditTarget> ChangedTargets(RoiEditSnapshot before,RoiEditSnapshot after)=>Array.AsReadOnly(
        before.Items.Concat(after.Items).Select(row=>new RoiEditTarget(row.Group,row.Id)).Distinct().Where(target=>!NativeRoiSession.SameItems(
            before.Items.Where(row=>row.Group==target.Group&&row.Id==target.Id).ToArray(),after.Items.Where(row=>row.Group==target.Group&&row.Id==target.Id).ToArray())).ToArray());
}

/// <summary>One borrowed edit scope for all original ROI parameter stacks and image handles.</summary>
public sealed partial class NativeRoiSession : INotifyPropertyChanged,IDisposable
{
    private readonly Func<RoiEditSnapshot> _read;
    private readonly Func<RoiEditStamp,RoiEditProposal,RoiEditAcceptance> _accept;
    private readonly Func<bool> _isCurrent;
    private readonly Func<string,bool>? _canDraw;
    private readonly Func<string,bool>? _canSetCoverage;
    private readonly Func<RoiHistoryAvailability> _history;
    private Dictionary<(string,Guid),NativeRoiProjection> _items=new();
    private readonly ProjectionRows<NativeRoiProjection> _rows=new();
    private bool _disposed,_invalid,_committing,_selecting,_refreshing,_reading,_checkingCurrent,_queryingDraw;
    private Func<bool>? _editorCommit;
    private NativeRoiProjection? _selected;
    private RoiEditStamp? _stamp;
    private IReadOnlyList<RoiGroupState> _groups=Array.Empty<RoiGroupState>();
    private RoiDrawRequest? _drawRequest;
    private long _drawGeneration,_groupGeneration;
    private bool _requesting;
    public string DrawingGroup {get;private set;}
    public RoiDrawRequest? ActiveDraw=>_drawRequest;
    public RoiEditStamp Stamp=>_stamp??throw new InvalidOperationException("区域范围尚未装载。");
    public ImageFrameAddress Frame {get;}
    public CoordinateFrame Coordinates {get;}
    public int ImageWidth {get;}
    public int ImageHeight {get;}
    public IReadOnlyImage Image {get;}
    public IReadOnlyList<NativeRoiProjection> Items=>_rows;
    public IReadOnlyList<RoiGroupState> Groups=>_groups;
    public NativeRoiProjection? CurROIData=>_selected;
    public bool IsHaveData=>_selected is not null&&Owns(_selected);
    public bool IsCurrent
    {
        get
        {
            if(_disposed||_invalid||_checkingCurrent)return false;
            _checkingCurrent=true;try{return _isCurrent()&&!_disposed&&!_invalid;}finally{_checkingCurrent=false;}
        }
    }
    private bool SelectedInDrawingGroup=>IsHaveData&&_selected!.Group==DrawingGroup;
    public bool BoxEnable_ModuleEnableA=>SelectedInDrawingGroup&&_selected is NativeBoxRoiProjection;
    public bool SectorEnable_ModuleEnableA=>SelectedInDrawingGroup&&_selected is NativeSectorRoiProjection;
    public bool BoxA {get=>ActiveDraw is {} draw?draw.Kind=="rectangle":BoxEnable_ModuleEnableA;set{}}
    public bool SectorA {get=>ActiveDraw is {} draw?draw.Kind=="sector":SectorEnable_ModuleEnableA;set{}}
    public bool SectorRingEnable_ModuleEnableA=>SelectedInDrawingGroup&&_selected is NativeRingRoiProjection;
    public bool SectorRingA {get=>ActiveDraw is {} draw?draw.Kind=="annulus":SectorRingEnable_ModuleEnableA;set{}}
    public bool PolygonEnable_ModuleEnableA=>SelectedInDrawingGroup&&_selected is NativePolygonRoiProjection;
    public bool PolygonA {get=>ActiveDraw is {} draw?draw.Kind=="polygon":PolygonEnable_ModuleEnableA;set{}}
    public bool FullScreenEnableA=>CanSetGroupCoverage(DrawingGroup)&&_groups.Single(group=>group.Group==DrawingGroup).EmptyCoverage!=RoiCoverage.Explicit;
    public bool FullScreen_ModuleEnableA=>IsCurrent&&_groups.Any(group=>group.Group==DrawingGroup&&group.EmptyCoverage!=RoiCoverage.Explicit&&group.Coverage==group.EmptyCoverage);
    public bool FullScreenA {get=>FullScreen_ModuleEnableA&&ActiveDraw is null;set{}}
    public bool IsAvoid {get;}
    public bool Is3DROI=>false;
    public string ROISelectionA=>"Box SectorRing Sector Polygon";
    public string DisplayNameA=>"ROI形状";
    public string DescriptionA=>DisplayNameA;
    public string Error {get;private set;}="";
    public ICommand ROIValueChangedCommand {get;}
    public ICommand ValueChangedCommand {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;
    /// <summary>Attachment retirement boundary: observers may record pending state only, never acquire another interaction.</summary>
    public event Action? InteractionRetiring;
    private bool _notifyingRetirement;
    internal void NotifyInteractionRetiring()
    {
        if(_disposed||_notifyingRetirement)return;_notifyingRetirement=true;
        try{NativeRoiCallbacks.Run((InteractionRetiring?.GetInvocationList()??Array.Empty<Delegate>()).Select(handler=>(Action)(()=>((Action)handler)())).ToArray());}
        finally{_notifyingRetirement=false;}
    }

    public event Action<RoiDrawRequest>? DrawRequested;
    public event Action<RoiDrawRequest>? DrawCancelled;
    public NativeRoiSession(IReadOnlyImage image,Func<RoiEditSnapshot> read,
        Func<RoiEditStamp,RoiEditProposal,RoiEditAcceptance> accept,Func<bool> isCurrent,Func<RoiHistoryAvailability> history,string drawingGroup,bool isAvoid=false,
        Func<string,bool>? canDraw=null,Func<string,bool>? canSetCoverage=null)
    {
        ResourceContractRules.ValidateImage(image);
        if(string.IsNullOrWhiteSpace(drawingGroup))throw new ArgumentException("绘制组必须由宿主明确提供。",nameof(drawingGroup));
        DrawingGroup=drawingGroup;_canDraw=canDraw;_canSetCoverage=canSetCoverage;
        IsAvoid=isAvoid;Image=image;Frame=image.Frame;Coordinates=image.EffectiveDomain.Frame;ImageWidth=image.Width;ImageHeight=image.Height;
        _read=read;_accept=accept;_isCurrent=isCurrent;_history=history;
        ROIValueChangedCommand=new EditCommand(_=>{if(_selected is {} selected)TryCommit(selected);});
        ValueChangedCommand=new EditCommand(value=>
        {
            if(value is Native.Dependencies.Regions.RoiType.FullScreen)
            {
                var group=_groups.SingleOrDefault(state=>state.Group==DrawingGroup);
                if(group is not null&&group.EmptyCoverage!=RoiCoverage.Explicit)TrySetCoverage(group.EmptyCoverage);
                return;
            }
            var kind=value switch
            {
                Native.Dependencies.Regions.RoiType.Box=>"rectangle",
                Native.Dependencies.Regions.RoiType.SectorRing=>"annulus",
                Native.Dependencies.Regions.RoiType.Sector=>"sector",
                Native.Dependencies.Regions.RoiType.Polygon=>"polygon",
                _=>throw new NotSupportedException("该原绘制类型尚未接入。")
            };
            BeginDraw(kind);
        });
        Refresh();
    }
    private RoiEditSnapshot Capture(RoiEditSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);var stamp=snapshot.Stamp;ArgumentNullException.ThrowIfNull(stamp);
        if(stamp.ScopeId==Guid.Empty||stamp.Revision<0||stamp.Frame!=Frame||
            (_stamp is {} old&&(old.ScopeId!=stamp.ScopeId||stamp.Revision<old.Revision)))
        {
            _invalid=true;
            Error="区域编辑范围、版本或物理帧已失效。";
            NativeRoiCallbacks.Run([CancelPending,Notify]);
            throw new InvalidOperationException(Error);
        }
        ArgumentNullException.ThrowIfNull(snapshot.Groups);
        var groups=snapshot.Groups.ToArray();var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var group in groups)
            if(group is null||string.IsNullOrWhiteSpace(group.Group)||!Enum.IsDefined(group.Coverage)||!Enum.IsDefined(group.EmptyCoverage)||!names.Add(group.Group))
                throw new ArgumentException("ROI覆盖组必须具有唯一名称和明确的有效状态。");
        if(_stamp==stamp&&!SameGroups(_groups,groups))
        {
            _invalid=true;Error="宿主修改ROI覆盖状态时必须递增编辑版本。";
            NativeRoiCallbacks.Run([CancelPending,Notify]);
            throw new InvalidOperationException(Error);
        }
        var rows=snapshot.Items.ToArray();var keys=new HashSet<(string,Guid)>();
        foreach(var row in rows)
        {
            if(string.IsNullOrWhiteSpace(row.Group)||row.Id==Guid.Empty||!keys.Add((row.Group,row.Id)))
                throw new ArgumentException("ROI组与稳定ID必须非空且唯一。");
            if(!groups.Any(group=>group.Group==row.Group&&group.Coverage==RoiCoverage.Explicit))
                throw new ArgumentException("显式ROI只能归属已声明为Explicit的组；None/FullDomain不得夹带几何对象。");
            ValidateNativeGeometry(row.Geometry,row.Enabled);
        }
        return new(stamp,Array.AsReadOnly(rows),Array.AsReadOnly(groups));
    }
    private static void ValidateNativeGeometry(RecordValue geometry,bool editable=true)
    {
        RoiGeometryEditing.Validate(geometry);
        var kind=geometry.Get<string>("kind");
        if(kind is not ("rectangle" or "sector" or "annulus" or "polygon"))
            throw new NotSupportedException("当前原组合接纳矩形、完整圆环、扇形及多边形。");
        // Partial annuli retain their exact identity and use the six-field Sector projection.
        // Full annuli still use the original four-field SectorRing; inherited annuli remain read-only.
    }
    public void Refresh()
    {
        if(_refreshing||_committing||_reading)return;
        if(!IsCurrent){NativeRoiCallbacks.Run([CancelPending,Notify]);return;}
        var snapshot=ReadCurrent();if(!IsCurrent)return;
        Install(Capture(snapshot));
    }
    private RoiEditSnapshot ReadCurrent()
    {
        if(_reading)throw new InvalidOperationException("区域快照读取不可重入。");
        _reading=true;try{return _read();}finally{_reading=false;}
    }
    private void Install(RoiEditSnapshot snapshot,RoiEditSelection? selection=null)
    {
        if(!IsCurrent||_refreshing)return;
        _refreshing=true;
        try
        {
            var changedVersion=_stamp!=snapshot.Stamp;
            var cancellation=changedVersion?InvalidateDraw():Array.Empty<Action>();
            if(!changedVersion&&(!SameGroups(_groups,snapshot.Groups)||_items.Count!=snapshot.Items.Count||snapshot.Items.Any(row=>
                !_items.TryGetValue((row.Group,row.Id),out var old)||!old.Matches(row))))
                throw new InvalidOperationException("宿主修改区域内容时必须递增编辑版本。");
            if(!changedVersion)return; // A display refresh must not cancel a live gesture or typed raw input.
            var next=new Dictionary<(string,Guid),NativeRoiProjection>();
            foreach(var row in snapshot.Items)
            {
                var key=(row.Group,row.Id);
                var item=_items.TryGetValue(key,out var previous)&&previous.GetType()==NativeRoiProjection.ProjectionType(row)
                    ?previous:NativeRoiProjection.Create(this,row);
                next.Add(key,item);
            }
            var notifications=new List<Action>(cancellation);
            foreach(var old in _items.Values.Where(old=>!next.Values.Contains(old)))old.Detach();
            _items=next;_stamp=snapshot.Stamp;_groups=snapshot.Groups;
            foreach(var row in snapshot.Items)
            {
                var item=next[(row.Group,row.Id)];item.Install(row,changedVersion);
                if(changedVersion)notifications.Add(item.Notify);
            }
            var target=selection switch
            {
                RoiEditSelection.Clear=>null,
                RoiEditSelection.Select select=>select.Target,
                _=>_selected is {} current?new RoiEditTarget(current.Group,current.Id):null
            };
            _selected=target is not null&&next.TryGetValue((target.Group,target.Id),out var selected)&&selected.Enabled&&selected.Visible?selected:null;
            var nextRows=snapshot.Items.Select(row=>next[(row.Group,row.Id)]).ToArray();
            if(!_rows.SequenceEqual(nextRows)){_rows.Install(nextRows);notifications.Insert(0,_rows.NotifyReset);}
            notifications.Add(Notify);
            NativeRoiCallbacks.Run(notifications);
        }
        finally{_refreshing=false;}
    }
    internal bool Owns(NativeRoiProjection item)=>IsCurrent&&_items.TryGetValue((item.Group,item.Id),out var current)
        &&ReferenceEquals(current,item)&&item.Enabled&&item.Visible;
    internal IDisposable RegisterEditorCommit(Func<bool> commit)
    {
        if(_editorCommit is not null)throw new InvalidOperationException("一个ROI编辑会话只允许一份参数编辑器。");
        _editorCommit=commit;return new Registration(()=>{if(ReferenceEquals(_editorCommit,commit))_editorCommit=null;});
    }
    internal bool PrepareEditor()=>_editorCommit?.Invoke()??IsCurrent;
    private sealed class Registration(Action release):IDisposable
    {private Action? _release=release;public void Dispose(){var action=_release;_release=null;action?.Invoke();}}
    internal bool CanEdit(NativeRoiProjection item)=>!_refreshing&&!_reading&&!_committing&&Owns(item)&&ReferenceEquals(item,_selected);
    public bool Select(string group,Guid? id)
    {
        if(!IsCurrent||_refreshing||_reading||_selecting||_committing)return false;
        NativeRoiProjection? next=null;
        if(id is {} key&&(!_items.TryGetValue((group,key),out next)||!Owns(next)))return false;
        if(ReferenceEquals(_selected,next))return true;
        _selecting=true;
        try
        {
            if(!PrepareEditor()||!IsCurrent)return false;
            if(id is {} target&&(!_items.TryGetValue((group,target),out next)||!Owns(next)))return false;
            var stamp=Stamp;var groupGeneration=_groupGeneration;CancelPending();
            if(!IsCurrent||Stamp!=stamp||groupGeneration!=_groupGeneration)return false;
            if(id is {} after&&(!_items.TryGetValue((group,after),out next)||!Owns(next)))return false;
            _selected=next;
            if(next is not null&&DrawingGroup!=next.Group){DrawingGroup=next.Group;++_groupGeneration;}
            Error="";Notify();return true;
        }
        finally{_selecting=false;}
    }
    public void CancelPending()
    {
        var stamp=_stamp;var groups=_groupGeneration;var generation=_drawGeneration;
        var rows=_rows.Select(item=>(Item:item,Pending:item.PendingStamp)).ToArray();
        NativeRoiCallbacks.Run(new Action[]{CancelDraw}.Concat(rows.Select(row=>(Action)(()=>
        {if(_drawGeneration==generation+1&&stamp==_stamp&&groups==_groupGeneration&&_rows.Any(item=>ReferenceEquals(item,row.Item))&&row.Item.PendingStamp==row.Pending)row.Item.Cancel();}))));
    }
    internal void CancelPending(Func<bool> stillOwns)
    {
        var request=ActiveDraw;var generation=_drawGeneration;var stamp=_stamp;var groups=_groupGeneration;var rows=_rows.Select(item=>(Item:item,Pending:item.PendingStamp)).ToArray();
        NativeRoiCallbacks.Run(new Action[]{()=>{if(stillOwns()&&ReferenceEquals(ActiveDraw,request))CancelDraw();}}
            .Concat(rows.Select(row=>(Action)(()=>{if(_drawGeneration==generation+1&&stamp==_stamp&&groups==_groupGeneration&&stillOwns()&&_rows.Any(item=>ReferenceEquals(item,row.Item))&&row.Item.PendingStamp==row.Pending)row.Item.Cancel();}))));
    }
    public void SetDrawingGroup(string group)
    {
        if(string.IsNullOrWhiteSpace(group))throw new ArgumentException("绘制组必须非空。",nameof(group));
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(group==DrawingGroup)return;
        DrawingGroup=group;++_groupGeneration;NativeRoiCallbacks.Run([CancelDraw,Notify]);
    }
    private Action[] InvalidateDraw()
    {
        var request=_drawRequest;_drawRequest=null;++_drawGeneration;
        return request is null?[]:(DrawCancelled?.GetInvocationList().Cast<Action<RoiDrawRequest>>()??[]).Select(handler=>(Action)(()=>handler(request))).Append(NotifyDrawState).ToArray();
    }
    private void NotifyDrawState()=>NativeRoiCallbacks.Run((PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>()??[])
        .Select(handler=>(Action)(()=>handler(this,new(nameof(ActiveDraw))))));
    public void CancelDraw()
    {
        if(InteractionRetiring is null){NativeRoiCallbacks.Run(InvalidateDraw());return;}
        var request=_drawRequest;var generation=_drawGeneration;var stamp=_stamp;var groups=_groupGeneration;var expired=true;
        bool Same()=>generation==_drawGeneration&&ReferenceEquals(request,_drawRequest)&&stamp==_stamp&&groups==_groupGeneration;
        NativeRoiCallbacks.Run([
            ()=>expired=!IsCurrent,
            ()=>{if(expired&&Same())NotifyInteractionRetiring();},
            ()=>{if(Same())NativeRoiCallbacks.Run(InvalidateDraw());}]);
    }
    private bool CanDrawGroup(string group)
    {
        if(_queryingDraw||!IsCurrent||!_groups.Any(state=>state.Group==group))return false;
        var stamp=_stamp;var groups=_groupGeneration;var draws=_drawGeneration;
        _queryingDraw=true;
        try
        {
            var allowed=_canDraw?.Invoke(group)!=false;
            return allowed&&IsCurrent&&stamp==_stamp&&groups==_groupGeneration&&draws==_drawGeneration&&DrawingGroup==group;
        }
        finally{_queryingDraw=false;}
    }
    public RoiDrawRequest? BeginDraw(string kind)
    {
        if(!IsCurrent||_committing||_refreshing||_reading||_selecting||_requesting)return null;
        if(kind is not ("rectangle" or "annulus" or "sector" or "polygon"))throw new NotSupportedException("需要该形状的原绘制入口。");
        var handlers=DrawRequested?.GetInvocationList().Cast<Action<RoiDrawRequest>>().ToArray()??[];
        if(handlers.Length!=1){Error="绘制需要一份明确的视口接收器。";Notify();return null;}
        _requesting=true;var group=DrawingGroup;var groupGeneration=_groupGeneration;
        try
        {
            if(!CanDrawGroup(group)||groupGeneration!=_groupGeneration)return null;
            if(!PrepareEditor()||!IsCurrent||groupGeneration!=_groupGeneration||!CanDrawGroup(group))return null;
            var stamp=Stamp;var generation=_drawGeneration+1;CancelPending();
            if(!IsCurrent||Stamp!=stamp||generation!=_drawGeneration||groupGeneration!=_groupGeneration||!CanDrawGroup(group))return null;
            var snapshot=Capture(ReadCurrent());
            if(!IsCurrent||generation!=_drawGeneration||groupGeneration!=_groupGeneration||!CanDrawGroup(group))return null;
            if(snapshot.Stamp!=stamp){Error="区域根已改变，请在当前范围重新绘制。";Install(snapshot);return null;}
            handlers=DrawRequested?.GetInvocationList().Cast<Action<RoiDrawRequest>>().ToArray()??[];
            if(handlers.Length!=1){Error="绘制需要一份明确的视口接收器。";Notify();return null;}
            var request=new RoiDrawRequest(stamp,group,kind);_drawRequest=request;Error="";
            try{handlers[0](request);NotifyDrawState();}
            catch(Exception error)
            {
                NativeRoiCallbacks.Run(new Action[]{()=>System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw(),()=>{if(ReferenceEquals(_drawRequest,request))CancelDraw();}});
                throw;
            }
            return IsCurrent&&ReferenceEquals(_drawRequest,request)?request:null;
        }
        finally{_requesting=false;}
    }
    internal void DrawStatus(RoiDrawRequest request,string message,bool reject)
    {
        if(!IsCurrent||!ReferenceEquals(_drawRequest,request))return;
        Error=message;
        NativeRoiCallbacks.Run(reject?new Action[]{CancelDraw,Notify}:new Action[]{Notify});
    }
    public bool CompleteDraw(RoiDrawRequest request,RecordValue geometry)
    {
        if(!IsCurrent||_committing||_refreshing||_reading||_selecting||!ReferenceEquals(_drawRequest,request))return false;
        _drawRequest=null;var generation=++_drawGeneration;_committing=true;
        try
        {
            bool Current()=>IsCurrent&&generation==_drawGeneration&&Stamp==request.Stamp;
            if(!Current()||!CanDrawGroup(request.Group)||!Current())return false;
            ValidateNativeGeometry(geometry);
            if(geometry.Get<string>("kind")!=request.Kind)throw new ArgumentException("完成形状与原绘制请求不符。",nameof(geometry));
            var current=Capture(ReadCurrent());if(!Current())return false;
            if(current.Stamp!=request.Stamp){Error="绘制期间区域根已改变。";InstallAfterCommit(current);return false;}
            if(!CanDrawGroup(request.Group)||!Current())return false;
            var proposal=new RoiEditProposal.Create(request.Group,geometry);
            var result=_accept(current.Stamp,proposal);
            return PublishAcceptance(result,()=>
            {
                if(!Current())return;
                var installed=ValidateAcceptance(current,proposal,result);
                Error=result.Accepted?"":result.Error??"宿主拒绝新区域。";
                InstallAfterCommit(installed,result.Selection);
                if(!result.Accepted&&installed.Stamp==current.Stamp)Notify();
            });
        }
        finally{_committing=false;}
    }
    private static bool SameGeometry(RecordValue left,RecordValue right)
    {
        bool Same(object? a,object? b)=>a switch
        {
            PointGeometry point=>b is PointGeometry other&&point.Frame==other.Frame&&point.Point==other.Point,
            SequenceValue sequence=>b is SequenceValue other&&sequence.Declaration==other.Declaration&&sequence.Count==other.Count&&sequence.Zip(other).All(pair=>Same(pair.First,pair.Second)),
            _=>Equals(a,b)
        };
        if(left.Declaration!=right.Declaration)return false;
        var a=left.CopyFields();var b=right.CopyFields();
        return a.Count==b.Count&&a.All(pair=>b.TryGetValue(pair.Key,out var value)&&Same(pair.Value,value));
    }
    private RoiEditSnapshot ValidateAcceptance(RoiEditSnapshot current,RoiEditProposal proposal,RoiEditAcceptance result)
    {
        try{return ValidateAcceptanceCore(current,proposal,result);}
        catch
        {
            // The owner may already have installed a root. Retire this lease rather than inventing rollback.
            _invalid=true;Error="宿主区域回执违反接纳合同，编辑租约已停止。";
            NativeRoiCallbacks.Run([CancelPending,Notify]);throw;
        }
    }
    private RoiEditSnapshot ValidateAcceptanceCore(RoiEditSnapshot current,RoiEditProposal proposal,RoiEditAcceptance result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var snapshot=Capture(result.Snapshot);
        if(snapshot.Stamp.ScopeId!=current.Stamp.ScopeId||snapshot.Stamp.Frame!=current.Stamp.Frame||snapshot.Stamp.Revision<current.Stamp.Revision)
            throw new InvalidOperationException("回执不得改变范围、物理帧或倒退本次读取的版本。");
        if(snapshot.Stamp.Revision==current.Stamp.Revision&&(!SameItems(snapshot.Items,current.Items)||!SameGroups(snapshot.Groups,current.Groups)))
            throw new InvalidOperationException("同版回执不得改变区域内容。");
        if(result.Affected is null||result.Affected.Any(target=>target is null||string.IsNullOrWhiteSpace(target.Group)||target.Id==Guid.Empty)
            ||result.Affected.Distinct().Count()!=result.Affected.Count||result.Selection is null)
            throw new InvalidOperationException("回执必须明确唯一有效的受影响对象和选择指令。");
        if(!result.Accepted)
        {
            if(result.Affected.Count!=0||result.Selection is not RoiEditSelection.Keep)throw new InvalidOperationException("拒绝回执不得分配身份或改变选择。");
            return snapshot;
        }
        if(snapshot.Stamp.Revision<=current.Stamp.Revision)throw new InvalidOperationException("宿主接纳必须递增版本。");
        if(snapshot.Groups.Count!=current.Groups.Count||current.Groups.Any(group=>!snapshot.Groups.Any(next=>next.Group==group.Group&&next.EmptyCoverage==group.EmptyCoverage)))
            throw new InvalidOperationException("区域接纳不得更改组定义或宿主空集合策略。");
        if(proposal is not (RoiEditProposal.Undo or RoiEditProposal.Redo))
        {
            var expected=current.Groups.Select(group=>proposal switch
            {
                RoiEditProposal.SetCoverage coverage when group.Group==coverage.Group=>group with{Coverage=coverage.Coverage},
                RoiEditProposal.Create create when group.Group==create.Group=>group with{Coverage=RoiCoverage.Explicit},
                RoiEditProposal.Delete delete when delete.Targets.Any(target=>target.Group==group.Group)
                    &&!snapshot.Items.Any(row=>row.Group==group.Group)=>group with{Coverage=group.EmptyCoverage},
                _=>group
            }).ToArray();
            if(!SameGroups(snapshot.Groups,expected))throw new InvalidOperationException("区域提案只能改变明确目标组的覆盖，且不得改变宿主空集合策略。");
        }
        bool Has(RoiEditTarget target)=>snapshot.Items.Any(row=>row.Group==target.Group&&row.Id==target.Id&&row.Enabled&&row.Visible);
        var selected=result.Selection switch
        {
            RoiEditSelection.Select select=>select.Target,
            RoiEditSelection.Keep=>_selected is {} item?new RoiEditTarget(item.Group,item.Id):null,
            RoiEditSelection.Clear=>null,
            _=>throw new InvalidOperationException("未知选择指令。")
        };
        if(selected is not null&&!Has(selected))throw new InvalidOperationException("选择回执必须指向新快照中可编辑可见对象；删除选择须明确清除。");
        if(proposal is RoiEditProposal.SetCoverage coverage)
        {
            if(!current.Groups.Any(group=>group.Group==coverage.Group)||snapshot.Items.Any(row=>row.Group==coverage.Group)
                ||!SameItems(current.Items.Where(row=>row.Group!=coverage.Group).ToArray(),snapshot.Items)
                ||!result.Affected.ToHashSet().SetEquals(current.Items.Where(row=>row.Group==coverage.Group).Select(row=>new RoiEditTarget(row.Group,row.Id))))
                throw new InvalidOperationException("覆盖切换必须只清空目标组并精确回传被移除的对象，不得改动其他组。");
        }
        else if(proposal is RoiEditProposal.Delete delete)
        {
            if(!result.Affected.ToHashSet().SetEquals(delete.Targets)||delete.Targets.Any(target=>snapshot.Items.Any(row=>row.Group==target.Group&&row.Id==target.Id)))
                throw new InvalidOperationException("删除回执必须精确列出全部已移除对象。");
            var remaining=current.Items.Where(row=>!delete.Targets.Contains(new(row.Group,row.Id))).ToArray();
            if(!SameItems(remaining,snapshot.Items))throw new InvalidOperationException("删除不得改动其他对象或顺序。");
        }
        else if(proposal is RoiEditProposal.Create or RoiEditProposal.Replace)
        {
            if(result.Affected.Count!=1)throw new InvalidOperationException("单对象提案必须回传一个受影响对象。");
            var target=result.Affected[0];
            var group=proposal is RoiEditProposal.Create create?create.Group:((RoiEditProposal.Replace)proposal).Target.Group;
            var geometry=proposal is RoiEditProposal.Create created?created.Geometry:((RoiEditProposal.Replace)proposal).Geometry;
            if(target.Group!=group)throw new InvalidOperationException("接纳对象所属组不符。");
            if(proposal is RoiEditProposal.Replace replace&&target!=replace.Target)throw new InvalidOperationException("替换不得更改对象身份。");
            if(proposal is RoiEditProposal.Create&&current.Items.Any(row=>row.Id==target.Id))throw new InvalidOperationException("创建必须分配全新身份。");
            var row=snapshot.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);
            if(row is null||!row.Enabled||!row.Visible||!SameGeometry(row.Geometry,geometry))throw new InvalidOperationException("接纳回执未安装完整可编辑候选几何。");
            var before=current.Items.Where(row=>proposal is not RoiEditProposal.Replace replaced||row.Group!=replaced.Target.Group||row.Id!=replaced.Target.Id).ToArray();
            var after=snapshot.Items.Where(row=>row.Group!=target.Group||row.Id!=target.Id).ToArray();
            if(!SameItems(before,after))throw new InvalidOperationException("单对象编辑不得改动其他区域。");
        }
        else if(!result.Affected.ToHashSet().SetEquals(RoiEditAcceptance.ChangedTargets(current,snapshot)))
            throw new InvalidOperationException("历史回执必须精确列出本次改变的区域身份。");
        return snapshot;
    }
    internal static bool SameGroups(IReadOnlyList<RoiGroupState> left,IReadOnlyList<RoiGroupState> right)=>left.Count==right.Count&&left.All(group=>right.Contains(group));
    internal static bool SameItems(IReadOnlyList<RoiEditItem> left,IReadOnlyList<RoiEditItem> right)=>left.Count==right.Count&&left.Zip(right).All(pair=>
        pair.First.Group==pair.Second.Group&&pair.First.Id==pair.Second.Id&&pair.First.Name==pair.Second.Name&&pair.First.Enabled==pair.Second.Enabled
        &&pair.First.Visible==pair.Second.Visible&&SameGeometry(pair.First.Geometry,pair.Second.Geometry));
    public bool TryCommit(NativeRoiProjection item)
    {
        if(_committing||!CanEdit(item))return false;
        if(!item.HasPending)return true;
        _committing=true;
        try
        {
            var read=ReadCurrent();if(!IsCurrent)return false;
            var current=Capture(read);if(!IsCurrent)return false;
            if(item.PendingStamp!=current.Stamp)
            {Error="区域根已改变，旧编辑已取消。";InstallAfterCommit(current);return false;}
            var row=current.Items.SingleOrDefault(row=>row.Group==item.Group&&row.Id==item.Id);
            if(row is null||!row.Enabled||!row.Visible||!item.Matches(row))
                throw new InvalidOperationException("区域内容已变化而版本未更新。");
            RecordValue candidate;
            try{candidate=item.Prepare();}
            catch(Exception error)when(error is ArgumentException or OverflowException)
            {Error=error.Message;NativeRoiCallbacks.Run([item.Cancel,Notify]);return false;}
            if(!Owns(item)||!ReferenceEquals(item,_selected))return false;
            if(item.MatchesGeometry(row.Geometry,candidate))
            {Error="";NativeRoiCallbacks.Run([item.Cancel,Notify]);return true;}
            // Deliberately outside a rejection catch: host/observer exceptions are not false acceptance.
            var proposal=new RoiEditProposal.Replace(new(item.Group,item.Id),candidate);
            var result=_accept(current.Stamp,proposal);
            return PublishAcceptance(result,()=>
            {
                if(!IsCurrent)return;
                var installed=ValidateAcceptance(current,proposal,result);
                Error=result.Accepted?"":result.Error??"宿主拒绝区域几何候选。";
                var unchanged=installed.Stamp==_stamp;
                NativeRoiCallbacks.Run([()=>{if(!result.Accepted)item.Cancel();},()=>InstallAfterCommit(installed,result.Selection),()=>{if(unchanged)Notify();}]);
            });
        }
        finally{_committing=false;}
    }
    private static bool PublishAcceptance(RoiEditAcceptance result,Action install)
    {
        ArgumentNullException.ThrowIfNull(result);
        // Accepted owner state must always complete its publication/release, including stale display leases
        // and exceptions from validation or observers. Publication is never interpreted as rejection.
        NativeRoiCallbacks.Run([install,()=>result.Publish?.Invoke()]);
        return result.Accepted;
    }
    private void InstallAfterCommit(RoiEditSnapshot snapshot,RoiEditSelection? selection=null)
    {var committing=_committing;_committing=false;try{Install(snapshot,selection);}finally{_committing=committing;}}
    private void Notify()
    {
        var callbacks=new List<Action>();
        var handlers=PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>().ToArray()??[];
        foreach(var name in new[]{nameof(Groups),nameof(FullScreenEnableA),nameof(FullScreen_ModuleEnableA),nameof(FullScreenA),nameof(CurROIData),nameof(IsHaveData),nameof(BoxEnable_ModuleEnableA),nameof(SectorEnable_ModuleEnableA),nameof(BoxA),nameof(SectorA),nameof(SectorRingEnable_ModuleEnableA),nameof(SectorRingA),nameof(PolygonEnable_ModuleEnableA),nameof(PolygonA),nameof(Error)})
            foreach(var handler in handlers)callbacks.Add(()=>handler(this,new(name)));
        foreach(var handler in Changed?.GetInvocationList().Cast<Action>().ToArray()??[])callbacks.Add(handler);
        NativeRoiCallbacks.Run(callbacks);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var item in _rows)item.Detach();_items.Clear();_selected=null;_editorCommit=null;_rows.Install([]);
        try{NativeRoiCallbacks.Run([CancelDraw,_rows.NotifyReset,Notify]);}
        finally{PropertyChanged=null;Changed=null;InteractionRetiring=null;DrawRequested=null;DrawCancelled=null;}
    }
    private sealed class EditCommand(Action<object?> action):ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>action(parameter);
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
    private sealed class ProjectionRows<T>:IReadOnlyList<T>,IList,INotifyCollectionChanged,INotifyPropertyChanged where T:class
    {
        private T[] _values=[];
        public int Count=>_values.Length;
        public T this[int index]=>_values[index];
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Install(IEnumerable<T> rows)=>_values=rows.ToArray();
        internal void NotifyReset()
        {
            var callbacks=new List<Action>();
            foreach(var name in new[]{nameof(Count),"Item[]"})
                foreach(var observer in PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>().ToArray()??[])
                    callbacks.Add(()=>observer(this,new(name)));
            foreach(var observer in CollectionChanged?.GetInvocationList().Cast<NotifyCollectionChangedEventHandler>().ToArray()??[])
                callbacks.Add(()=>observer(this,new(NotifyCollectionChangedAction.Reset)));
            NativeRoiCallbacks.Run(callbacks);
        }
        public IEnumerator<T> GetEnumerator()=>((IEnumerable<T>)_values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        bool IList.IsReadOnly=>true;bool IList.IsFixedSize=>true;bool ICollection.IsSynchronized=>false;object ICollection.SyncRoot=>this;
        object? IList.this[int index]{get=>this[index];set=>throw new NotSupportedException();}
        int IList.Add(object? value)=>throw new NotSupportedException();void IList.Clear()=>throw new NotSupportedException();
        bool IList.Contains(object? value)=>value is T item&&_values.Contains(item);
        int IList.IndexOf(object? value)=>value is T item?Array.IndexOf(_values,item):-1;
        void IList.Insert(int index,object? value)=>throw new NotSupportedException();void IList.Remove(object? value)=>throw new NotSupportedException();
        void IList.RemoveAt(int index)=>throw new NotSupportedException();void ICollection.CopyTo(Array array,int index)=>_values.CopyTo(array,index);
    }
}

internal static class NativeRoiCallbacks
{
    internal static void Run(IEnumerable<Action> callbacks)
    {
        List<Exception>? errors=null;
        foreach(var callback in callbacks)try{callback();}catch(Exception error){(errors??=[]).Add(error);}
        if(errors?.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors?.Count>1)throw new AggregateException(errors);
    }
}
