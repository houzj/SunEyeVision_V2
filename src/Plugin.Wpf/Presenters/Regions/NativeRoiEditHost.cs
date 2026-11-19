using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;
public sealed partial class NativeRoiEditHost : IDisposable
{
    private readonly DeclaredInputFieldsView _inputs;
    private readonly NativeRoiUiDraftControls _controls;
    private readonly NativeRoiFrameSource? _source;
    private readonly SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow _imageField;
    private readonly Dictionary<string,NativeRoiEditor> _editors=new();
    private readonly Dictionary<string,NativeRoiSession> _sessions=new();
    private readonly Dictionary<string,Guid> _selectedRoiIds=new();
    private readonly Dictionary<NativeRoiSession,Action> _retirementHandlers=new();
    private bool _recordingRetirement;
    private readonly Dictionary<string,Guid> _scopes;
    private readonly INativeRoiEditDocument _document;
    private readonly string _primaryGroup,_secondaryGroup,_localMode;
    private readonly int _primaryMaximum,_secondaryMaximum;
    private readonly Dictionary<string,NativeRoiPendingInput> _pending=new();
    private string? _unrestoredInput;
    private NativeRoiFrameLease? _frame;
    private INativeRoiEditLease? _interaction;
    private string? _active;
    private string? _pendingActivate;
    private RecordValue _observed;
    private bool _disposed,_accepting,_refreshing,_activating;
    private bool _synchronizingSelection;
    private readonly Func<string?>? _drawBlockReason;
    private bool _localInspection,_localMask;
    private string _observedPreviewMode="";
    private long _generation,_projectionRevision;
    public long Revision {get;private set;}
    public string Error {get;private set;}="";
    private bool UnacceptedGesture=>_sessions.Values.Any(session=>session.ActiveDraw is not null||session.Items.Any(item=>item.HasPending))||_interaction?.HasPendingGesture==true;
    public bool HasPending=>_unrestoredInput is not null||_pending.Count>0||_editors.Values.Any(editor=>editor.HasPendingEdits)||UnacceptedGesture;
    public event Action? Changed;
    public NativeRoiEditHost(DeclaredInputFieldsView inputs,NativeRoiUiDraftControls controls,NativeRoiFrameSource? source,INativeRoiEditDocument document,Func<string?>? drawBlockReason=null)
    {
        _inputs=inputs;_controls=controls;_source=source;_drawBlockReason=drawBlockReason;_document=document;_observed=Read();
        _primaryGroup=document.Groups[0].Name;_secondaryGroup=document.Groups[1].Name;_localMode=document.LocalModeValue;
        _primaryMaximum=document.Groups[0].MaximumItems;_secondaryMaximum=document.Groups[1].MaximumItems;
        _scopes=new(){{_primaryGroup,Guid.NewGuid()},{_secondaryGroup,Guid.NewGuid()}};
        _imageField=inputs.Fields.Single(field=>field.FieldId==_document.ImageInputFieldId);
        try
        {
            foreach(var group in new[]{_primaryGroup,_secondaryGroup})
            {
                var editor=new NativeRoiEditor{Title=group==_primaryGroup?"形状":"屏蔽区",PolygonOnly=group==_secondaryGroup,
                    ToolTip="先运行图像来源，再编辑区域。"};
                editor.PrepareInteraction=()=>Activate(group);editor.InteractionFailureText=()=>_drawBlockReason?.Invoke()??(string.IsNullOrWhiteSpace(Error)?null:Error);editor.AllowUnboundFullDomain=group==_primaryGroup;editor.UnboundFullDomain=_document.Coverage(_observed,group)==RoiDocumentValue.FullDomain;editor.PendingChanged+=Notify;_editors.Add(group,editor);editor.Bind(null);
            }
            controls.BindLocalEditors(_editors[_primaryGroup],_editors[_secondaryGroup]);
            controls.Changed+=OnModesChanged;
            _imageField.Changed+=RefreshFrame;
            if(source is not null){source.Changed+=RefreshFrame;source.FrameReplaced+=RebindCurrentFrame;}
            if(source is not null)source.InputViewportClicked+=OnInputViewportClicked;
            _localInspection=_controls.IsLocalDrawing(_primaryGroup);_localMask=_controls.IsLocalDrawing(_secondaryGroup);
            _observedPreviewMode=_document.PreviewMode();InitializePreview();
        }
        catch(Exception failure)
        {
            try{Dispose();}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }

    private void Notify(){if(!_disposed)Changed?.Invoke();}
    private RecordValue Read()=>_document.Read();
    private RoiEditSnapshot Snapshot(string editableGroup)
    {
        var root=Read();
        if(!_document.Same(root,_observed))throw new InvalidOperationException("本地区域已被其他编辑替换，请重新打开窗口。");
        return Project(root,editableGroup,_projectionRevision,_frame!.Image.Frame);
    }
    private RoiEditSnapshot Project(RecordValue root,string editableGroup,long revision,ImageFrameAddress frame)
        => RoiDocumentProjection.Project(_document.View(root),editableGroup,_scopes[editableGroup],revision,frame,
            _controls.IsLocalDrawing,_document.ToNative);
    private bool FrameCurrent(long generation)
    {
        // VM keeps the ROI objects attached to the module result view while
        // parameter sub-tabs are switched. The basic-page control may be
        // unloaded or hidden during that navigation, but the physical frame
        // and ROI session remain valid until the input frame or editor scope
        // actually changes.
        var frame=_frame;
        return !_disposed&&generation==_generation&&frame is not null&&frame.IsCurrent&&ReferenceEquals(frame,_frame);
    }
    private void AcquireFrame(bool interactive)
    {
        if(_disposed||_refreshing||_accepting||_activating||!_controls.IsLoaded||!_controls.IsVisible)return;
        _refreshing=true;
        try
        {
            if(FrameCurrent(_generation))return;
            RetireFrame();if(_disposed)return;var generation=_generation;
            var frame=_source?.Borrow(_document.ImageInputFieldId,interactive);
            if(frame is null){Error=_drawBlockReason?.Invoke()??"请先运行并选择图像来源。";Notify();return;}
            if(_disposed||generation!=_generation||!frame.IsCurrent){frame.Dispose();return;}_frame=frame;
            foreach(var group in new[]{_primaryGroup,_secondaryGroup})
            {
                NativeRoiSession? session=null;
                session=new NativeRoiSession(frame.Image,()=>Snapshot(group),(stamp,proposal)=>Accept(ProposalGroup(proposal,session!.DrawingGroup),stamp,proposal),()=>FrameCurrent(generation),
                    ()=>new(_document.CanUndo,_document.CanRedo),group,isAvoid:group==_secondaryGroup,
                    canDraw:target=>target==group&&_controls.IsLocalDrawing(group),canSetCoverage:target=>target==group&&_controls.IsLocalDrawing(group),
                    maximumDrawItems:group==_primaryGroup?_primaryMaximum:_secondaryMaximum);
                _sessions.Add(group,session);
                var observedSession=session;
                observedSession.Changed+=()=>SyncSelection(observedSession);
                Action retiring=()=>RecordInteractionRetirement(group,session,generation);
                session.InteractionRetiring+=retiring;_retirementHandlers.Add(session,retiring);
                if(_pending.TryGetValue(group,out var pending)&&_controls.IsLocalDrawing(group))session.Select(group,pending.Id);
                _editors[group].Bind(session);
                if(pending is not null&&_controls.IsLocalDrawing(group))
                {
                    if(_editors[group].RestorePendingInput(pending))_pending.Remove(group);
                    else _unrestoredInput="区域对象已改变，未提交原文不能应用；请取消窗口后重新编辑。";
                }
                if(_selectedRoiIds.TryGetValue(group,out var selectedId)&&_controls.IsLocalDrawing(group))
                    session.Select(group,selectedId);
                if(!_selectedRoiIds.ContainsKey(group)&&_controls.IsLocalDrawing(group))
                {
                    var initial=session.Items.FirstOrDefault(item=>item.Enabled&&item.Visible);
                    if(initial is not null&&session.Select(group,initial.Id))
                    {
                        _selectedRoiIds[group]=initial.Id;
                        _pendingActivate ??= group;
                    }
                }
            }
            Error="";Notify();
            CompletePendingActivation();
        }
        catch(Exception error)
        {
            Error=error.Message;
            try{RetireFrame();}catch(Exception cleanup){throw new AggregateException(error,cleanup);}
            Notify();
        }
        finally{_refreshing=false;}
    }
    private static string ProposalGroup(RoiEditProposal proposal,string fallback)
        =>proposal switch
        {
            RoiEditProposal.Create create=>create.Group,
            RoiEditProposal.Replace replace=>replace.Target.Group,
            RoiEditProposal.SetCoverage coverage=>coverage.Group,
            RoiEditProposal.Delete delete=>delete.Targets[0].Group,
            _=>fallback
        };
    private void SyncSelection(NativeRoiSession source)
    {
        if(_disposed||_refreshing||_synchronizingSelection||source.CurROIData is not { } selected)return;
        _selectedRoiIds[selected.Group]=selected.Id;
        _synchronizingSelection=true;
        try
        {
            foreach(var pair in _sessions)
                if(!ReferenceEquals(pair.Value,source)&&pair.Value.IsCurrent&&
                    (pair.Value.CurROIData?.Group!=selected.Group||pair.Value.CurROIData?.Id!=selected.Id))
                    pair.Value.Select(selected.Group,selected.Id);
        }
        finally{_synchronizingSelection=false;}
    }
    public void RefreshFrame()
    {
        if(_disposed||_refreshing||_accepting||_activating)return;
        // 原件语义（SetRoiVisible + UpdateROI，ModuleResultView.xaml.cs.pending:2252/:604-608）：
        // 形状=参数对象，页签/窗口可见性变化只隐藏不销毁；仅图像帧身份变化才退役。
        // 退役后（如切换可编辑图像）立即按当前来源重建会话，既有形状随参数值恢复显示，
        // 不依赖用户再点绘制按钮。
        if(_frame is not {} frame||!frame.IsCurrent)
        {
            if(_active is {} active)_pendingActivate=active;
            _refreshing=true;
            try{RetireFrame();Notify();}
            finally{_refreshing=false;}
        }
        if(_controls.IsVisible&&_controls.IsLoaded&&_sessions.Count==0
            &&(_controls.IsLocalDrawing(_primaryGroup)||_controls.IsLocalDrawing(_secondaryGroup)))
            AcquireFrame(interactive:false);
    }
    private void RebindCurrentFrame()
    {
        if(_disposed||_refreshing||_accepting||_activating)return;
        if(!_controls.IsLoaded||!_controls.IsVisible)return;
        var reacquire=_controls.IsLocalDrawing(_primaryGroup)||_controls.IsLocalDrawing(_secondaryGroup);
        if(_active is {} active)_pendingActivate=active;
        _refreshing=true;
        try
        {
            RetireFrame();
        }
        finally{_refreshing=false;}
        if(!_disposed&&reacquire)AcquireFrame(interactive:false);
    }
    private bool ActivateCore(string group)
    {
        if(!FrameCurrent(_generation)||!_sessions.TryGetValue(group,out var session))return false;
        if(_active==group&&_interaction?.IsCurrent==true)
        {
            // A free hit may have changed the selected object's group while
            // keeping the same viewport lease. Selecting a new-draw button
            // must restore that session's creation group before BeginDraw.
            if(session.DrawingGroup!=group)session.SetDrawingGroup(group);
            return true;
        }
        if(_interaction?.HasPendingGesture==true){Error="请先完成或取消当前绘制。";return false;}
        _activating=true;var generation=_generation;
        try
        {
            if(_active is {} previous&&!_editors[previous].TryCommitPending()){Error=_editors[previous].Error;return false;}
            var old=_interaction;_interaction=null;_active=null;old?.Dispose();
            if(!FrameCurrent(generation))return false;
            INativeRoiEditLease candidate;
            try
            {
                candidate=_frame!.BeginEditing(session);
            }
            catch(InvalidOperationException) when(!FrameCurrent(generation)||!session.IsCurrent)
            {
                // The result viewport can complete a frame replacement between the
                // focus event and BeginEditing. VM reprojects after that replacement.
                // Retire this stale lease and let Activate reacquire the new receipt.
                RetireFrame();
                return false;
            }
            if(!FrameCurrent(generation)){candidate.Dispose();return false;}
            _interaction=candidate;_active=group;_pendingActivate=null;_editors[group].DeferInteractionFeedback=false;Error="";return true;
        }
        finally{_activating=false;}
    }
    private bool Activate(string group)
    {
        if(_disposed||_activating||_accepting||!_controls.IsLoaded||!_controls.IsVisible||!_controls.IsLocalDrawing(group))return false;
        if(!FrameCurrent(_generation))AcquireFrame(interactive:true);
        if(ActivateCore(group))return true;
        // A frame may have changed after the first borrow but before the
        // viewport accepted the edit lease. Reborrow once on the new receipt.
        if(!FrameCurrent(_generation))
        {
            AcquireFrame(interactive:true);
            if(ActivateCore(group))return true;
        }
        // 原厂两段式（用户实机截图）：显示图不是输入图时点形状按钮只选中不弹框，
        // 提示延迟到图像落笔（OnInputViewportClicked 重试仍阻断时弹出）。
        // 待激活记录兜底：用户切回输入图后自动重建即武装。
        _editors[group].DeferInteractionFeedback=_drawBlockReason?.Invoke() is not null;
        _pendingActivate=group;
        return false;
    }
    private void OnInputViewportClicked()
    {
        if(_disposed||_refreshing||_accepting||_activating)return;
        if(_active is not null)return;
        var group=_controls.IsLocalDrawing(_primaryGroup)?_primaryGroup
            :_controls.IsLocalDrawing(_secondaryGroup)?_secondaryGroup:null;
        if(group is null)return;
        if(!Activate(group)&&_drawBlockReason?.Invoke() is not null)
            _editors[group].ShowInteractionFeedback();
    }
    private void CompletePendingActivation()
    {
        if(_disposed||_pendingActivate is not {} group)return;
        if(!_controls.IsLocalDrawing(group)){_pendingActivate=null;return;}
        ActivateCore(group);
    }
    private RoiEditAcceptance Accept(string group,RoiEditStamp expected,RoiEditProposal proposal)
    {
        var before=Snapshot(group);
        if(_disposed||_accepting||expected!=before.Stamp||_frame?.IsCurrent!=true)return RoiEditAcceptance.Reject(before,"区域编辑范围或物理帧已失效。");
        if(!_controls.IsLocalDrawing(group))return RoiEditAcceptance.Reject(before,"当前区域模式尚未接入执行。");
        if(_editors.Where(pair=>pair.Key!=group).Any(pair=>pair.Value.HasPendingEdits))return RoiEditAcceptance.Reject(before,"请先提交另一组区域数值。");
        var old=Read();
        if(!_document.TryApply(old,group,proposal,out var next,out var selection,out var documentError))
        {Error=documentError;return RoiEditAcceptance.Reject(before,documentError);}
        _accepting=true;
        try
        {
            _document.Write(next);
            _observed=next;++Revision;++_projectionRevision;var after=Project(next,group,_projectionRevision,expected.Frame);
            var generation=_generation;
            var actions=_sessions.Where(pair=>pair.Key!=group).Select(pair=>(Action)(()=>
            {
                if(FrameCurrent(generation)&&_sessions.TryGetValue(pair.Key,out var current)&&ReferenceEquals(current,pair.Value))current.Refresh();
            })).Append(()=>{if(!_disposed&&generation==_generation)Notify();}).ToArray();
            return new(true,after,Affected(before,after),selection,Publish:()=>Run(actions));
        }
        finally{_accepting=false;}
    }
    private IReadOnlyList<RoiEditTarget> Affected(RoiEditSnapshot before,RoiEditSnapshot after)
    {
        var targets=before.Items.Concat(after.Items).Select(row=>new RoiEditTarget(row.Group,row.Id)).Distinct();
        return targets.Where(target=>
        {
            var a=before.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);var b=after.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);
            return a is null||b is null||a.Name!=b.Name||a.Enabled!=b.Enabled||a.Visible!=b.Visible
                ||!_document.Same(_document.FromNative(a.Geometry,null),_document.FromNative(b.Geometry,null));
        }).ToArray();
    }
    public bool TryCapture(out string error)
    {
        if(_disposed){error="区域编辑会话已关闭。";return false;}
        RefreshFrame();
        if(_unrestoredInput is not null||_pending.Count>0){error=_unrestoredInput??"图像来源暂不可用，区域原文已保留；请恢复图像后修正或取消窗口。";return false;}
        if(UnacceptedGesture){error="请先完成或取消当前绘制。";return false;}
        foreach(var editor in _editors.Values.Where(editor=>editor.HasPendingEdits))if(!editor.TryCommitPending()){error=editor.Error;return false;}
        try{_=Read();error="";return true;}catch(Exception failure){error=failure.Message;return false;}
    }
    private void OnModesChanged()
    {
        if(_disposed)return;
        // The ROI control creates the inherited parameter rows when their mode first becomes visible.
        TrackPreviewSources();
        if(_accepting||_activating||_refreshing)return;
        var inspection=_controls.IsLocalDrawing(_primaryGroup);var mask=_controls.IsLocalDrawing(_secondaryGroup);
        var mode=_document.PreviewMode();var previewChanged=inspection!=_localInspection||mask!=_localMask||mode!=_observedPreviewMode;_observedPreviewMode=mode;
        if(inspection==_localInspection&&mask==_localMask){if(previewChanged)QueuePreview();Notify();return;}
        _localInspection=inspection;_localMask=mask;
        _pendingActivate=null;
        CapturePendingEditors();
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
        try{CapturePendingEditors(group);}
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
        if(!_disposed)CapturePendingEditors();
        foreach(var pair in _sessions)
        {
            if(pair.Value.CurROIData is { } selected)_selectedRoiIds[pair.Key]=selected.Id;
            else _selectedRoiIds.Remove(pair.Key);
        }
        ClearPreview();
        ++_generation;var interaction=_interaction;_interaction=null;_active=null;var frame=_frame;_frame=null;
        var sessions=_sessions.Values.ToArray();_sessions.Clear();
        var handlers=sessions.Where(session=>_retirementHandlers.ContainsKey(session)).Select(session=>(Session:session,Handler:_retirementHandlers[session])).ToArray();
        foreach(var item in handlers)_retirementHandlers.Remove(item.Session);
        var coverage=_observed;
        Run(handlers.Select(item=>(Action)(()=>item.Session.InteractionRetiring-=item.Handler)).Concat(_editors.Select(pair=>(Action)(()=>{pair.Value.UnboundFullDomain=_document.Coverage(coverage,pair.Key)==RoiDocumentValue.FullDomain;pair.Value.Bind(null);})).ToArray()).Concat(new Action[]{()=>interaction?.Dispose()})
            .Concat(sessions.Select(session=>(Action)session.Dispose)).Append(()=>frame?.Dispose()));
    }
    private static void Run(IEnumerable<Action> actions)
    {
        var errors=new List<Exception>();foreach(var action in actions.ToArray())try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException(errors);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_pendingActivate=null;_controls.Changed-=OnModesChanged;
        _imageField.Changed-=RefreshFrame;if(_source is not null){_source.Changed-=RefreshFrame;_source.FrameReplaced-=RebindCurrentFrame;_source.InputViewportClicked-=OnInputViewportClicked;}Changed=null;
        Run(new Action[]{DisposePreview,RetireFrame}.Concat(_editors.Values.Select(editor=>(Action)(()=>{editor.PendingChanged-=Notify;editor.Dispose();}))));
    }
}

public sealed partial class NativeRoiEditHost
{
    private NativeRoiInputLease? _previewInputs;
    private NativeResultViewport.ReadOnlyRoiLayer? _previewLayer;
    private DispatcherOperation? _previewRefresh;
    private DispatcherOperation? _frameRefresh;
    private long _previewRequest,_previewRevision;
    private bool _previewing,_previewAgain;
    private readonly HashSet<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow> _previewFields=new();
    internal string PreviewError {get;private set;}="";
    private void InitializePreview()
    {
        _controls.Loaded+=PreviewLoaded;_controls.Unloaded+=PreviewUnloaded;_controls.IsVisibleChanged+=PreviewVisibilityChanged;
        TrackPreviewSources();
        _controls.Changed+=QueuePreview;
        if(_source is not null)_source.Changed+=QueuePreview;
    }
    private void TrackPreviewSources()
    {
        foreach(var field in _inputs.Fields)
        {
            var id=field.FieldId;
            if(_document.IsPreviewSource(id)&&_previewFields.Add(field))
                field.Changed+=QueuePreview;
        }
    }
    private void PreviewLoaded(object sender,RoutedEventArgs args)
    {
        QueuePreview();
        QueueFrameRefresh();
    }
    private void PreviewUnloaded(object sender,RoutedEventArgs args)
    {
        _frameRefresh?.Abort();_frameRefresh=null;
    }
    private void PreviewVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)
    {
        if(_controls.IsVisible){QueuePreview();QueueFrameRefresh();}
        else{_frameRefresh?.Abort();_frameRefresh=null;}
    }
    private void QueueFrameRefresh()
    {
        if(_disposed||!_controls.IsLoaded||!_controls.IsVisible)return;
        _frameRefresh?.Abort();
        _frameRefresh=_controls.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(() =>
        {
            _frameRefresh=null;
            if(!_disposed&&_controls.IsLoaded&&_controls.IsVisible)RefreshFrame();
        }));
    }
    
    private void QueuePreview()
    {
        if(_disposed)return;
        // Retire only this read-only layer. Mask session revision, raw inputs and gestures are untouched.
        ClearPreview();
        if(_previewing){_previewAgain=true;return;}
        var request=_previewRequest;
        _previewRefresh=_controls.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>
        {
            if(_disposed||request!=_previewRequest)return;_previewRefresh=null;RefreshPreview(request);
        }));
    }
    private void RefreshPreview(long request)
    {
        if(_disposed||_previewing||!_controls.IsLoaded||!_controls.IsVisible)return;
        _previewing=true;NativeRoiInputLease? inputs=null;NativeResultViewport.ReadOnlyRoiLayer? layer=null;
        try
        {
            var mode=_document.PreviewMode();var maskInherited=_controls.IsInheritedMask;
            if(mode==_localMode&&!maskInherited)return;
            if(!FrameCurrent(_generation)){AcquireFrame(interactive:false);if(request!=_previewRequest){if(FrameCurrent(_generation))QueuePreview();return;}}
            var frame=_frame;var generation=_generation;
            bool OwnerCurrent(){var valid=!_disposed&&request==_previewRequest&&generation==_generation&&ReferenceEquals(frame,_frame)&&frame is not null&&FrameCurrent(generation)&&_controls.IsLoaded&&_controls.IsVisible;return valid&&!_disposed&&request==_previewRequest&&generation==_generation&&ReferenceEquals(frame,_frame);}
            if(!OwnerCurrent())throw new InvalidOperationException("请选择与输入一致的已发布图像后预览。");
            var ids=_document.PreviewInputIds(mode,maskInherited).Concat(new[]{_document.ImageInputFieldId,_document.ModeFieldId}).ToArray();
            inputs=_source?.CaptureInputs(ids)??throw new InvalidOperationException("继承来源预览尚未就绪。");
            if(!OwnerCurrent()||!inputs.IsCurrent||!OwnerCurrent())return;
            var image=inputs.Inputs.Get<IReadOnlyImage>(_document.ImageInputFieldId);
            if(image.Frame!=frame!.Image.Frame||image.EffectiveDomain.Frame!=frame.Image.EffectiveDomain.Frame)
                throw new InvalidOperationException("继承输入与可见图像不是同一物理帧和坐标域。");
            var root=_document.MaterializePreview(inputs.Inputs,Read());
            var rows=(mode==_localMode?Enumerable.Empty<RoiEditItem>():Items(_primaryGroup))
                .Concat(maskInherited?Items(_secondaryGroup):Enumerable.Empty<RoiEditItem>()).ToArray();
            IEnumerable<RoiEditItem> Items(string group)=>_document.View(root).Items(group).Select(item=>new RoiEditItem(group,item.Id,item.Name,_document.ToNative(item.Geometry),Enabled:false,Visible:true));
            if(!OwnerCurrent()||!inputs.IsCurrent||!OwnerCurrent())return;
            var captured=inputs;layer=frame.CreateReadOnlyLayer();
            bool Current()=>OwnerCurrent()&&captured.IsCurrent&&OwnerCurrent();
            if(!layer.Present(frame.Receipt,++_previewRevision,rows,Current)||!Current())return;
            _previewInputs=inputs;inputs=null;_previewLayer=layer;layer=null;
            if(_interaction is null&&_controls.IsLocalDrawing(_secondaryGroup)&&_document.View(Read()).Items(_secondaryGroup).Any(item=>item.Enabled&&item.Visible))Activate(_secondaryGroup);
        }
        catch(Exception failure)
        {
            if(!_disposed&&request==_previewRequest){PreviewError=failure.Message;_controls.ToolTip=failure.Message;}
        }
        finally
        {
            _previewing=false;
            try{Run(new Action[]{()=>layer?.Dispose(),()=>inputs?.Dispose()});}
            finally{if(_previewAgain&&!_disposed){_previewAgain=false;QueuePreview();}}
        }
    }
    private void ClearPreview()
    {
        ++_previewRequest;var pending=_previewRefresh;_previewRefresh=null;
        var layer=_previewLayer;_previewLayer=null;var inputs=_previewInputs;_previewInputs=null;
        if (PreviewError.Length != 0)
        {
            PreviewError = "";
            _controls.ClearValue(FrameworkElement.ToolTipProperty);
        }
        Run(new Action[]{()=>pending?.Abort(),()=>layer?.Dispose(),()=>inputs?.Dispose()});
    }
    private void DisposePreview()
    {
        _controls.Loaded-=PreviewLoaded;_controls.Unloaded-=PreviewUnloaded;_controls.IsVisibleChanged-=PreviewVisibilityChanged;
        _frameRefresh?.Abort();_frameRefresh=null;
        foreach(var field in _previewFields)field.Changed-=QueuePreview;_previewFields.Clear();
        _controls.Changed-=QueuePreview;
        if(_source is not null)_source.Changed-=QueuePreview;ClearPreview();
    }
}
