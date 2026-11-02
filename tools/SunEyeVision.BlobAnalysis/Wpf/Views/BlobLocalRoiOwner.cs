using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>One private formal-input owner; the two sessions are read-only projections of that same value.</summary>
internal sealed partial class BlobLocalRoiOwner : IDisposable
{
    private readonly DeclaredInputFieldsView _inputs;
    private readonly NativeRoiUiDraftControls _controls;
    private readonly NativeRoiFrameSource? _source;
    private readonly Dictionary<string,NativeRoiEditor> _editors=new();
    private readonly Dictionary<string,NativeRoiSession> _sessions=new();
    private readonly Dictionary<NativeRoiSession,Action> _retirementHandlers=new();
    private bool _recordingRetirement;
    private readonly Dictionary<string,Guid> _scopes=new(){{BlobLocalRoi.Inspection,Guid.NewGuid()},{BlobLocalRoi.Mask,Guid.NewGuid()}};
    private readonly Stack<RecordValue> _undo=new(),_redo=new();
    private readonly Dictionary<string,NativeRoiPendingInput> _pending=new();
    private string? _unrestoredInput;
    private NativeRoiFrameLease? _frame;
    private INativeRoiEditLease? _interaction;
    private string? _active;
    private RecordValue _observed;
    private bool _disposed,_accepting,_refreshing,_activating;
    private bool _localInspection,_localMask;
    private string _observedPreviewMode=BlobRoiInheritance.Local;
    private long _generation,_projectionRevision;
    public long Revision {get;private set;}
    public string Error {get;private set;}="";
    private bool UnacceptedGesture=>_sessions.Values.Any(session=>session.ActiveDraw is not null||session.Items.Any(item=>item.HasPending))||_interaction?.HasPendingGesture==true;
    public bool HasPending=>_unrestoredInput is not null||_pending.Count>0||_editors.Values.Any(editor=>editor.HasPendingEdits)||UnacceptedGesture;
    public event Action? Changed;
    internal BlobLocalRoiOwner(DeclaredInputFieldsView inputs,NativeRoiUiDraftControls controls,NativeRoiFrameSource? source)
    {
        _inputs=inputs;_controls=controls;_source=source;_observed=Read();
        foreach(var group in new[]{BlobLocalRoi.Inspection,BlobLocalRoi.Mask})
        {
            var editor=new NativeRoiEditor{Title=group==BlobLocalRoi.Inspection?"形状":"屏蔽区",ToolTip=group==BlobLocalRoi.Mask?"屏蔽区支持多形状。":"先运行图像来源，再编辑区域。"};
            editor.PrepareInteraction=()=>Activate(group);editor.AllowUnboundFullDomain=group==BlobLocalRoi.Inspection;editor.UnboundFullDomain=BlobLocalRoi.Coverage(_observed,group)==BlobLocalRoi.FullDomain;editor.PendingChanged+=Notify;_editors.Add(group,editor);editor.Bind(null);
        }
        controls.BindLocalEditors(_editors[BlobLocalRoi.Inspection],_editors[BlobLocalRoi.Mask]);
        controls.Changed+=OnModesChanged;
        inputs.Fields.Single(field=>field.FieldId==BlobTool.ImageInput).Changed+=RefreshFrame;
        if(source is not null)source.Changed+=RefreshFrame;
        _localInspection=_controls.IsLocalDrawing(BlobLocalRoi.Inspection);_localMask=_controls.IsLocalDrawing(BlobLocalRoi.Mask);
        _observedPreviewMode=PreviewMode();InitializePreview();

    }

    private void Notify(){if(!_disposed)Changed?.Invoke();}
    private RecordValue Read()
    {
        var setting=_inputs.ReadUnpresentedInput(BlobLocalRoi.InputId);
        var value=setting switch {UnsetSetting=>BlobLocalRoi.Empty,ConstantSetting {Value:RecordValue record}=>record,_=>throw new ArgumentException("本地区域必须为纯值定义。")};
        BlobLocalRoi.Validate(value);return value;
    }
    private RoiEditSnapshot Snapshot(string editableGroup)
    {
        var root=Read();
        if(!BlobLocalRoi.Same(root,_observed))throw new InvalidOperationException("本地区域已被其他编辑替换，请重新打开窗口。");
        return Project(root,editableGroup,_projectionRevision,_frame!.Image.Frame);
    }
    private RoiEditSnapshot Project(RecordValue root,string editableGroup,long revision,ImageFrameAddress frame)
    {
        var rows=new List<RoiEditItem>();var groups=new List<RoiGroupState>();
        foreach(var group in new[]{BlobLocalRoi.Inspection,BlobLocalRoi.Mask})
        {
            groups.Add(new(group,Enum.Parse<RoiCoverage>(BlobLocalRoi.Coverage(root,group)),group==BlobLocalRoi.Inspection?RoiCoverage.FullDomain:RoiCoverage.None));
            foreach(var item in BlobLocalRoi.Items(root,group))rows.Add(new(group,Guid.ParseExact(item.Get<string>("id"),"N"),item.Get<string>("name"),
                BlobLocalRoiProjection.ToNative(item.Get<RecordValue>("shape")),item.Get<bool>("enabled")&&group==editableGroup&&_controls.IsLocalDrawing(group),item.Get<bool>("visible")&&_controls.IsLocalDrawing(group)));
        }
        return new(new(_scopes[editableGroup],revision,frame),rows.AsReadOnly(),groups.AsReadOnly());
    }
    private bool FrameCurrent(long generation)
    {var frame=_frame;return !_disposed&&_controls.IsLoaded&&_controls.IsVisible&&generation==_generation&&frame is not null&&frame.IsCurrent&&!_disposed&&_controls.IsLoaded&&_controls.IsVisible&&generation==_generation&&ReferenceEquals(frame,_frame);}
    private void AcquireFrame()
    {
        if(_disposed||_refreshing||_accepting||_activating||!_controls.IsLoaded||!_controls.IsVisible)return;
        _refreshing=true;
        try
        {
            if(FrameCurrent(_generation))return;
            RetireFrame();if(_disposed)return;var generation=_generation;
            var frame=_source?.Borrow(BlobTool.ImageInput);
            if(frame is null){Error="请先运行并选择图像来源。";Notify();return;}
            if(_disposed||generation!=_generation||!frame.IsCurrent){frame.Dispose();return;}_frame=frame;
            foreach(var group in new[]{BlobLocalRoi.Inspection,BlobLocalRoi.Mask})
            {
                var session=new NativeRoiSession(frame.Image,()=>Snapshot(group),(stamp,proposal)=>Accept(group,stamp,proposal),()=>FrameCurrent(generation),
                    ()=>new(_undo.Count>0,_redo.Count>0),group,isAvoid:group==BlobLocalRoi.Mask,
                    canDraw:target=>target==group&&_controls.IsLocalDrawing(group),canSetCoverage:target=>target==group&&_controls.IsLocalDrawing(group));
                _sessions.Add(group,session);
                Action retiring=()=>RecordInteractionRetirement(group,session,generation);
                session.InteractionRetiring+=retiring;_retirementHandlers.Add(session,retiring);
                if(_pending.TryGetValue(group,out var pending)&&_controls.IsLocalDrawing(group))session.Select(group,pending.Id);
                _editors[group].Bind(session);
                if(pending is not null&&_controls.IsLocalDrawing(group))
                {
                    if(_editors[group].RestorePendingInput(pending))_pending.Remove(group);
                    else _unrestoredInput="区域对象已改变，未提交原文不能应用；请取消窗口后重新编辑。";
                }
            }
            Error="";Notify();
        }
        catch(Exception error)
        {
            Error=error.Message;
            try{RetireFrame();}catch(Exception cleanup){throw new AggregateException(error,cleanup);}
            Notify();
        }
        finally{_refreshing=false;}
    }
    internal void RefreshFrame()
    {
        if(_disposed||_refreshing||_accepting||_activating||_frame is null)return;
        _refreshing=true;
        try{if(!FrameCurrent(_generation)){RetireFrame();Error="请选择与输入一致的图像后继续编辑。";Notify();}}
        finally{_refreshing=false;}
    }
    private bool Activate(string group)
    {
        if(_disposed||_activating||_accepting||!_controls.IsLoaded||!_controls.IsVisible||!_controls.IsLocalDrawing(group))return false;
        if(!FrameCurrent(_generation))AcquireFrame();
        if(!FrameCurrent(_generation)||!_sessions.TryGetValue(group,out var session))return false;
        if(_active==group&&_interaction?.IsCurrent==true)return true;
        if(_interaction?.HasPendingGesture==true){Error="请先完成或取消当前绘制。";return false;}
        _activating=true;var generation=_generation;
        try
        {
            if(_active is {} previous&&!_editors[previous].TryCommitPending()){Error=_editors[previous].Error;return false;}
            var old=_interaction;_interaction=null;_active=null;old?.Dispose();
            if(!FrameCurrent(generation))return false;
            var candidate=_frame!.BeginEditing(session);
            if(!FrameCurrent(generation)){candidate.Dispose();return false;}
            _interaction=candidate;_active=group;Error="";return true;
        }
        finally{_activating=false;}
    }
    private RoiEditAcceptance Accept(string group,RoiEditStamp expected,RoiEditProposal proposal)
    {
        var before=Snapshot(group);
        if(_disposed||_accepting||expected!=before.Stamp||_frame?.IsCurrent!=true)return RoiEditAcceptance.Reject(before,"区域编辑范围或物理帧已失效。");
        if(!_controls.IsLocalDrawing(group))return RoiEditAcceptance.Reject(before,"当前区域模式尚未接入执行。");
        if(_editors.Where(pair=>pair.Key!=group).Any(pair=>pair.Value.HasPendingEdits))return RoiEditAcceptance.Reject(before,"请先提交另一组区域数值。");
        var old=Read();var next=old;var selection=(RoiEditSelection)new RoiEditSelection.Keep();var history=false;
        var rows=BlobLocalRoi.Items(old,group).ToList();var coverage=BlobLocalRoi.Coverage(old,group);
        try
        {
            switch(proposal)
            {
                case RoiEditProposal.Create create when create.Group==group:
                    var id=Guid.NewGuid();rows.Add(BlobLocalRoi.Item(id,group==BlobLocalRoi.Inspection?"检测区域":"屏蔽区域",BlobLocalRoiProjection.FromNative(create.Geometry)));coverage=BlobLocalRoi.Explicit;selection=new RoiEditSelection.Select(new(group,id));break;
                case RoiEditProposal.Replace replace when replace.Target.Group==group:
                    var index=rows.FindIndex(row=>row.Get<string>("id")==replace.Target.Id.ToString("N"));if(index<0)throw new ArgumentException("区域已不存在。");
                    var item=rows[index];rows[index]=BlobLocalRoi.Item(replace.Target.Id,item.Get<string>("name"),BlobLocalRoiProjection.FromNative(replace.Geometry,item.Get<RecordValue>("shape")),item.Get<bool>("enabled"),item.Get<bool>("visible"));break;
                case RoiEditProposal.Delete delete when delete.Targets.All(target=>target.Group==group):
                    foreach(var target in delete.Targets)if(rows.RemoveAll(row=>row.Get<string>("id")==target.Id.ToString("N"))!=1)throw new ArgumentException("区域已不存在。");
                    if(rows.Count==0)coverage=group==BlobLocalRoi.Inspection?BlobLocalRoi.FullDomain:BlobLocalRoi.None;selection=new RoiEditSelection.Clear();break;
                case RoiEditProposal.SetCoverage set when set.Group==group&&set.Coverage==(group==BlobLocalRoi.Inspection?RoiCoverage.FullDomain:RoiCoverage.None):
                    rows.Clear();coverage=set.Coverage.ToString();selection=new RoiEditSelection.Clear();break;
                case RoiEditProposal.Undo when _undo.Count>0:next=_undo.Peek();history=true;selection=new RoiEditSelection.Clear();break;
                case RoiEditProposal.Redo when _redo.Count>0:next=_redo.Peek();history=true;selection=new RoiEditSelection.Clear();break;
                default:throw new ArgumentException("此区域操作尚未接通。");
            }
            if(!history)next=group==BlobLocalRoi.Inspection?BlobLocalRoi.Root(BlobLocalRoi.Group(coverage,rows),old.Get<RecordValue>(BlobLocalRoi.Mask))
                :BlobLocalRoi.Root(old.Get<RecordValue>(BlobLocalRoi.Inspection),BlobLocalRoi.Group(coverage,rows));
            BlobLocalRoi.Validate(next);if(BlobLocalRoi.Same(old,next))return RoiEditAcceptance.Reject(before,"区域内容没有变化。");
            _accepting=true;
            _inputs.WriteUnpresentedInput(BlobLocalRoi.InputId,new ConstantSetting(next));
            if(proposal is RoiEditProposal.Undo){_undo.Pop();_redo.Push(old);}else if(proposal is RoiEditProposal.Redo){_redo.Pop();_undo.Push(old);}else{_undo.Push(old);_redo.Clear();}
            _observed=next;++Revision;++_projectionRevision;var after=Project(next,group,_projectionRevision,expected.Frame);
            var generation=_generation;
            var actions=_sessions.Where(pair=>pair.Key!=group).Select(pair=>(Action)(()=>
            {
                if(FrameCurrent(generation)&&_sessions.TryGetValue(pair.Key,out var current)&&ReferenceEquals(current,pair.Value))current.Refresh();
            })).Append(()=>{if(!_disposed&&generation==_generation)Notify();}).ToArray();
            return new(true,after,Affected(before,after),selection,Publish:()=>Run(actions));
        }
        catch(Exception error)when(!_accepting&&(error is ArgumentException or InvalidOperationException))
        {Error=error.Message;return RoiEditAcceptance.Reject(before,error.Message);}
        finally{_accepting=false;}
    }
    private static IReadOnlyList<RoiEditTarget> Affected(RoiEditSnapshot before,RoiEditSnapshot after)
    {
        var targets=before.Items.Concat(after.Items).Select(row=>new RoiEditTarget(row.Group,row.Id)).Distinct();
        return targets.Where(target=>
        {
            var a=before.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);var b=after.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);
            return a is null||b is null||a.Name!=b.Name||a.Enabled!=b.Enabled||a.Visible!=b.Visible
                ||!BlobLocalRoi.Same(BlobLocalRoiProjection.FromNative(a.Geometry),BlobLocalRoiProjection.FromNative(b.Geometry));
        }).ToArray();
    }
    internal bool TryCapture(out string error)
    {
        if(_disposed){error="区域编辑会话已关闭。";return false;}
        RefreshFrame();
        if(_unrestoredInput is not null||_pending.Count>0){error=_unrestoredInput??"图像来源暂不可用，区域原文已保留；请恢复图像后修正或取消窗口。";return false;}
        if(_interaction?.HasPendingGesture==true){error="请先完成或取消当前绘制。";return false;}
        foreach(var editor in _editors.Values.Where(editor=>editor.HasPendingEdits))if(!editor.TryCommitPending()){error=editor.Error;return false;}
        try{_=Read();error="";return true;}catch(Exception failure){error=failure.Message;return false;}
    }
    private void OnModesChanged()
    {
        if(_disposed||_accepting||_activating||_refreshing)return;
        var inspection=_controls.IsLocalDrawing(BlobLocalRoi.Inspection);var mask=_controls.IsLocalDrawing(BlobLocalRoi.Mask);
        var mode=PreviewMode();var previewChanged=inspection!=_localInspection||mode!=_observedPreviewMode;_observedPreviewMode=mode;
        if(inspection==_localInspection&&mask==_localMask){if(previewChanged)QueuePreview();Notify();return;}
        _localInspection=inspection;_localMask=mask;
        CapturePendingEditors();
        if(_interaction?.HasPendingGesture==true)_unrestoredInput="区域模式已改变，未完成绘制不能接纳；请取消窗口后重新编辑。";
        if(_active is {} group&&!_controls.IsLocalDrawing(group))
        {var old=_interaction;_interaction=null;_active=null;old?.Dispose();}
        ++_projectionRevision;
        var generation=_generation;
        Run(_sessions.Values.Select(session=>(Action)(()=>{if(FrameCurrent(generation))session.Refresh();})).ToArray());
        RestorePendingEditors();

        if(previewChanged)QueuePreview();Notify();
    }
    private void RecordInteractionRetirement(string group,NativeRoiSession session,long generation)
    {
        if(_disposed||_recordingRetirement||generation!=_generation||!_sessions.TryGetValue(group,out var current)||!ReferenceEquals(current,session))return;
        _recordingRetirement=true;
        try
        {
            CapturePendingEditors(group);
            if(session.ActiveDraw is not null||session.Items.Any(item=>item.Group==group&&item.HasPending))
                _unrestoredInput="区域视口已改变，未完成的绘制不能接纳；请取消窗口后重新编辑。";
        }
        finally{_recordingRetirement=false;}
    }
    private void CapturePendingEditors(string? group=null)
    {
        foreach(var pair in _editors.Where(pair=>(group is null||pair.Key==group)&&pair.Value.HasPendingEdits).ToArray())
        {
            var input=pair.Value.CapturePendingInput();
            if(input is not null)_pending[pair.Key]=input;
            else if(!_pending.ContainsKey(pair.Key))_unrestoredInput="区域原文的对象已失效；请取消窗口后重新编辑。";
        }
    }
    private void RestorePendingEditors()
    {
        foreach(var pair in _pending.ToArray())
        {
            if(!_controls.IsLocalDrawing(pair.Key)||!_sessions.TryGetValue(pair.Key,out var session)||!session.IsCurrent)continue;
            if(session.Select(pair.Key,pair.Value.Id)&&_editors[pair.Key].RestorePendingInput(pair.Value))_pending.Remove(pair.Key);
            else _unrestoredInput="区域对象已改变，未提交原文不能应用；请取消窗口后重新编辑。";
        }
    }
    private void RetireFrame()
    {
        if(!_disposed)
        {
            CapturePendingEditors();
            if(UnacceptedGesture)_unrestoredInput="区域视口已改变，未完成的绘制不能接纳；请取消窗口后重新编辑。";
        }
        ClearPreview();
        ++_generation;var interaction=_interaction;_interaction=null;_active=null;var frame=_frame;_frame=null;
        var sessions=_sessions.Values.ToArray();_sessions.Clear();
        var handlers=sessions.Where(session=>_retirementHandlers.ContainsKey(session)).Select(session=>(Session:session,Handler:_retirementHandlers[session])).ToArray();
        foreach(var item in handlers)_retirementHandlers.Remove(item.Session);
        var coverage=_observed;
        Run(handlers.Select(item=>(Action)(()=>item.Session.InteractionRetiring-=item.Handler)).Concat(_editors.Select(pair=>(Action)(()=>{pair.Value.UnboundFullDomain=BlobLocalRoi.Coverage(coverage,pair.Key)==BlobLocalRoi.FullDomain;pair.Value.Bind(null);})).ToArray()).Concat(new Action[]{()=>interaction?.Dispose()})
            .Concat(sessions.Select(session=>(Action)session.Dispose)).Append(()=>frame?.Dispose()));
    }
    private static void Run(IEnumerable<Action> actions)
    {
        var errors=new List<Exception>();foreach(var action in actions.ToArray())try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException(errors);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_controls.Changed-=OnModesChanged;
        _inputs.Fields.Single(field=>field.FieldId==BlobTool.ImageInput).Changed-=RefreshFrame;if(_source is not null)_source.Changed-=RefreshFrame;Changed=null;
        Run(new Action[]{DisposePreview,RetireFrame}.Concat(_editors.Values.Select(editor=>(Action)(()=>{editor.PendingChanged-=Notify;editor.Dispose();}))));
    }
}
