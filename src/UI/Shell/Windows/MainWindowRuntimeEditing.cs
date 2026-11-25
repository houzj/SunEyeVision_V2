using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Features.Camera;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Plugin.Contracts.Acquisition;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    internal Func<string,bool,Func<bool>,SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameLease?>? RuntimeRoiFrameBorrow {get;set;}
    internal event Action? RoiFrameAvailabilityChanged;
    internal void NotifyRoiFrameAvailabilityChanged() => RoiFrameAvailabilityChanged?.Invoke();
    /// <summary>用户在显示图像视口上落笔（原件两段式：此时才检查绘制资格并弹阻断提示）。</summary>
    internal event Action? RoiInputViewportClicked;
    internal void NotifyRoiInputViewportClicked() => RoiInputViewportClicked?.Invoke();
    private bool OwnsSelectedDocumentNode(NodeDefinition node)
    {
        var session = DocumentManager.CurrentSession;
        var workflowId = WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
        // 外部设值仍要求"当前实例"：陈旧实例必须被拒绝（外部契约不变）。
        // 提交后重建实例的场景由内部按 Id 重绑定（FindCurrentNode）解决，不放松这里。
        try{return session is not null && workflowId is not null && session.GetNodes(workflowId).Any(actual => ReferenceEquals(actual,node));}
        catch(InvalidOperationException){return false;}
    }
    /// <summary>按稳定 Id 找回当前节点实例；提交后用于把选择重新绑定到新实例。</summary>
    private NodeDefinition? FindCurrentNode(string nodeId)
    {
        var session = DocumentManager.CurrentSession;
        if(session is null)return null;
        try
        {
            // 优先当前流程；提交后 CurrentWorkflowId 可能瞬时变化，故回退到整方案检索，避免误判"节点已消失"。
            var workflowId = WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
            if(workflowId is not null)
            {
                var current = session.GetNodes(workflowId).FirstOrDefault(actual=>string.Equals(actual.Id,nodeId,StringComparison.Ordinal));
                if(current is not null)return current;
            }
            foreach(var flow in session.GetWorkflows())
                if(session.GetNodes(flow.Id).FirstOrDefault(actual=>string.Equals(actual.Id,nodeId,StringComparison.Ordinal)) is {} found)
                    return found;
            return null;
        }
        catch(InvalidOperationException){return null;}
    }
    private void RefreshFormalNodeSelection()
    {
        var node=SelectedNode;
        var workflowId=WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
        // Rebinding a committed draft can replace the NodeDefinition instance
        // while keeping the same workflow/node identity. VM keeps the module
        // result view and its last result in that case. Preserve the retained
        // publication through this selection pass so the editor rebind does not
        // become an implicit result reset.
        var preserveStablePresentation=node is not null&&workflowId is not null
            &&_retainedEditorDebugSnapshot is { IsComplete:true } retained
            &&retained.Identity.NodeId==node.Id
            &&retained.SourceWorkflowId==workflowId;
        foreach(var editor in _productionEditors.ToArray())
        {
            // The debug editor owns the selected node's private draft. During a
            // committed rebind the selection may be temporarily empty or point to
            // the replacement instance; do not discard that context from this
            // transient formal-refresh pass. The editor window close path remains
            // the authoritative owner for its lifetime.
            if(ReferenceEquals(editor,_editorDebugContext))continue;
            if(!editor.IsValid)editor.Discard();
        }
        // 提交会重建节点实例：先把选择重新绑定到当前实例，再走后续引用判定，避免选择被误判失效。
        if (SelectedNode is {} stale && FindCurrentNode(stale.Id) is {} rebound && !ReferenceEquals(rebound, stale))
        { SelectedNode = rebound; return; }
        ReleaseActiveResultView();
        if(!preserveStablePresentation)ClearResultDisplay();
        if (node is null) { HasDataAnalysisControl=false; SelectedNodeDataAnalysisControl=null; ClearFormalImageSourceSelection(); NotifyResultHistoryChanged(); return; }
        if (!OwnsSelectedDocumentNode(node)) { SelectedNode=null; return; }
        UpdateDataAnalysisControl(node).GetAwaiter().GetResult();
        RefreshFormalPublishedResult();
        RefreshFormalImageSourceSelection();
         // The image-source binding may be created after a workflow run when
         // the user selects the node afterwards. Consume the still-current
         // formal read receipts now so ImageInputSource.CurrentIndex and the
         // thumbnail selection catch up with the completed execution.
         if(DocumentManager.CurrentSession is {} currentSession)
             RefreshFormalImageReadReceipts(currentSession);
        NotifyResultDisplayChanged();
    }
    private long _editorOpenGeneration;
    private bool CanOpenFormalEditor => !_documentHostReleased && !IsRunning
        && !_solutionTransitionPrepared && !_transitionPreparationInProgress;

    private async System.Threading.Tasks.Task OpenFormalNodeEditor(NodeDefinition node)
    {
        // PROBE-KEEP：窗口打开分段计时，仅超阈值时输出（见 PerfProbe）。
        using var perf=SunEyeVision.UI.Services.Performance.PerfProbe.Begin("open",node.Name);
        if(!CanOpenFormalEditor)
            throw new InvalidOperationException("当前主窗正在运行、切换或关闭，不能打开参数页。");
        if (!OwnsSelectedDocumentNode(node)) throw new InvalidOperationException("节点已退出当前实际流程。");
        // Opening the private editor rebuilds the node draft and may retire the
        // formal publication. VM keeps the last completed result visible while
        // the draft is being edited, so retain it before creating the context.
        if(_resultPublicationSnapshot is { IsComplete:true } published&&SelectedNode?.Id==node.Id)
        {
            CaptureFormalSnapshotBeforeEditor(published);
            RetainEditorDebugSnapshot(published);
        }
        else CaptureFormalSnapshotBeforeEditor(null);
        if (!node.IsEnabled) return;
        var session=DocumentManager.CurrentSession!;
        var workflowId=WorkflowTabViewModel.SelectedTab!.CurrentWorkflowId;
        _=session.AdmitPresentation(workflowId,node.Id);
        var opening=++_editorOpenGeneration;
        bool RequestCurrent()=>opening==_editorOpenGeneration&&CanOpenFormalEditor;
        if (_openDebugWindow is {} previous)
        {
            if(previous is RuntimeToolEditorWindow actual)
            {if(!await actual.CloseAndDrainAsync()){if(RequestCurrent())previous.Activate();return;}}
            else {previous.Close();if(previous.IsVisible){previous.Activate();return;}}
        }
        if(!RequestCurrent())return;
        bool OwnsTarget()=>ReferenceEquals(DocumentManager.CurrentSession,session)
            &&WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId==workflowId&&OwnsSelectedDocumentNode(node);
        if (!OwnsTarget())
            throw new InvalidOperationException("关闭旧窗口期间方案或节点已变化。");
        if (!node.IsEnabled) return;
        SelectedNode=node;
        // Selection observers may open a different node synchronously.
        bool SelectionCurrent() => RequestCurrent() && ReferenceEquals(SelectedNode, node);
        if (!SelectionCurrent() || !OwnsTarget()) return;
        if (WorkflowTabViewModel.SelectedTab?.DocumentCanvas is { } canvas)
        {
            foreach (var projection in canvas.Nodes.ToArray())
            {
                if (!SelectionCurrent() || !ReferenceEquals(WorkflowTabViewModel.SelectedTab?.DocumentCanvas, canvas)) return;
                projection.IsSelected = ReferenceEquals(projection.Definition, node);
            }
        }
        if (!SelectionCurrent() || !OwnsTarget() || !node.IsEnabled) return;
        perf.Mark("前置");
        var control=new RuntimeNodeConfigurationControl();
        var window=new RuntimeToolEditorWindow(control,node.ToolType) {Title=node.Name};
        window.BeforeClose=CaptureEditorSnapshotForClose;
        if (ResolveSolutionDialogOwner(null) is {} owner) window.Owner=owner;
        _openDebugWindow=window;
        perf.Mark("构造");
        window.Loaded += (_, _) =>
        {
            if (ReferenceEquals(_openDebugWindow, window)) NotifyParameterWindowStateChanged();
        };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openDebugWindow, window)) _openDebugWindow = null;
            if (ReferenceEquals(_editorDebugControl, control))
            {
                var formalSnapshot=TakeFormalSnapshotBeforeEditor();
                _editorDebugControl = null;
                _editorDebugContext = null;
                ClearRetainedEditorDebugSnapshot();
                RetireResultPublication();
                if(formalSnapshot is {IsComplete:true}&&SelectedNode?.Id==formalSnapshot.Identity.NodeId
                    &&formalSnapshot.SourceWorkflowId==WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId)
                {
                    RetainEditorDebugSnapshot(formalSnapshot);
                    RefreshFormalPublishedResult();
                }
                else RefreshFormalPublishedResult();
            }
            else if (_editorDebugContext is null)
            {
                // RuntimeToolEditorWindow raises its Closed event after the
                // editor context has drained.  In that order the context
                // callback may already have detached _editorDebugControl and
                // consumed the saved formal snapshot, so the branch above is
                // skipped.  The final window-close boundary must still
                // re-present the current formal publication; otherwise the
                // private debug retirement leaves the shared ViewBox empty.
                RetireResultPublication();
                RefreshFormalPublishedResult();
            }
            SetEditorRelatedImage(null);ResultWorkspaceChanged?.Invoke();
            NotifyParameterWindowStateChanged();
        };
        try { InitializeRuntimeEditor(window,node,session,workflowId,opening); perf.Mark("初始化"); }
        catch { control.Dispose(); window.Close(); if(ReferenceEquals(_openDebugWindow,window))_openDebugWindow=null; throw; }
    }
    private void InitializeRuntimeEditor(RuntimeToolEditorWindow window,NodeDefinition node,DocumentSession session,string workflowId,long opening)
    {
        var control=window.Editor;
        ProductionEditorContext? context=null;
            // A committed draft can briefly clear/rebind the canvas selection before
            // the replacement node is installed. Keep this editor alive across that
            // transient gap; the session, workflow and final node identity still gate
            // every callback once the selection settles.
            bool Current()
            {
                var currentWorkflow=WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
                return ReferenceEquals(DocumentManager.CurrentSession,session)
                    &&(SelectedNode is null||SelectedNode.Id==node.Id)
                    &&(currentWorkflow is null||currentWorkflow==workflowId);
            }
        context=new ProductionEditorContext(session,workflowId,node,Current,control.BindDraft,
            receipt=>RefreshProductionPublication(session,receipt));
        try
        {
            ToolEditorCommit.AttachDraftContext(control,context);
            context.PrepareView=()=>control.TryCapture(out var error)?null:error;
            context.HasPendingViewEdits=()=>control.HasPendingEdits;
            context.PreserveViewAfterRebase=control.AdoptRebasedDraft;
            control.ReadCameraStatus = id =>
            {
                if (!Current()) return null;
                var device = session.Document.Devices.FirstOrDefault(item => item.Type == SunEyeVision.Domain.Solutions.DeviceType.Camera
                    && StringComparer.OrdinalIgnoreCase.Equals(CameraManagerViewModel.RuntimeDeviceId(item), id));
                if (device is null) return "设备已移除";
                if (!device.IsEnabled) return "已禁用";
                return ServiceInitializer.ReadCameraState(CameraManagerViewModel.RuntimeDeviceId(device)) switch
                {
                    CameraState.Connected => "已连接",
                    CameraState.Capturing => "采集中",
                    CameraState.Connecting or CameraState.Reconnecting => "连接中",
                    CameraState.StartingCapture or CameraState.StoppingCapture => "采集状态切换中",
                    CameraState.Faulted => "设备异常",
                    _ => "未连接"
                };
            };
            control.OpenCameraSettings = id =>
            {
                if (!Current() || !context.IsValid || !window.IsCurrent || !window.IsVisible)
                    throw new InvalidOperationException("相机设置所属调试窗口已失效。");
                ShowCameraManager(window, id);
            };
            context.Deactivated+=control.Dispose;
            context.Deactivated+=()=>{if(window.IsLoaded)window.Close();};
            _editorDebugContext=context;_editorDebugControl=control;
            window.AfterCloseDrain=()=>{if(context.IsActive)context.Discard();};
            // Normal window closure is also a context ownership boundary. The VM
            // editor retires its parameter session when the dialog closes.
            window.Closed += (_, _) => { if (context.IsActive) context.Discard(); };
            control.ConfigureDebug(context,()=>FormalImageInputs,
                ()=>Current()&&!IsRunning&&!_solutionTransitionPrepared&&!_transitionPreparationInProgress,
                message=>control.Dispatcher.BeginInvoke(new Action(()=>AddLog(message))),
                (field,interactive)=>
                {
                    if(!window.CanCompleteAssembly||!context.IsValid||!control.TryGetResultDisplayBinding(out var owner,out var binding)
                        ||owner.Id!=node.Id)return null;
                    var draft=context.PreparedDraft;
                    var configuration=draft.Configuration;
                    _activeRoiImageVariable=configuration.Fields.Read(field) as VariableSetting;
                    _activeRoiImageScope=context.ScopeId;
                    var key=GetWorkspaceImageSourceKey(configuration.Fields.Read(field),draft.WorkflowId);
                    if(_activeRoiImageVariable is null)
                    {
                        try{_activeRoiImageVariable=new VariableSetting(ParameterSettingCanonical.DeserializeVariable(key!));}
                        catch(Exception){_activeRoiImageVariable=null;}
                    }
                    if(key is null)return null;
                    bool SourceCurrent()
                    {
                        if(!window.CanCompleteAssembly||!context.IsValid||!ReferenceEquals(context.PreparedDraft,draft)
                            ||!control.IsResultDisplayBinding(node,binding))return false;
                        var latest=draft.Configuration;
                        if(ReferenceEquals(configuration,latest))return true;
                        if(GetWorkspaceImageSourceKey(latest.Fields.Read(field),draft.WorkflowId)!=key)return false;
                        configuration=latest;
                        return true;
                    }
                    return RuntimeRoiFrameBorrow?.Invoke(key,interactive,SourceCurrent);
                },()=>RoiDrawBlockReason,RefreshEditorDebugResult);
            EventHandler<RuntimeToolExecutionEventArgs> retainDebugResult=(_,args)=>
            {
                if(args.Result is {IsComplete:true} result&&ReferenceEquals(DocumentManager.CurrentSession,session)&&SelectedNode?.Id==node.Id)
                {
                    RetainEditorDebugSnapshot(result);
                    // VM 更新结果后在视口完成当前帧装载时重投影 ROI。结果来源
                    // 计划可能保持同一输入图而跳过工作区重建，因此执行完成事件
                    // 必须单独补一次编辑层接帧。
                    try
                    {
                        control.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(() =>
                        {
                            if(context.IsValid&&ReferenceEquals(_editorDebugControl,control)&&SelectedNode?.Id==node.Id)
                                NotifyRoiFrameAvailabilityChanged();
                        }));
                    }
                    catch(InvalidOperationException) when(control.Dispatcher.HasShutdownStarted||control.Dispatcher.HasShutdownFinished) { }
                }
            };
            control.ExecutionCompleted+=retainDebugResult;
            context.Deactivated+=()=>control.ExecutionCompleted-=retainDebugResult;
            context.Deactivated+=() =>
            {
                // 调试快照只在编辑窗口生命周期内保活。正式结果若已被主窗口
                // 保留，则继续由正式 publication 持有；关闭时只撤销当前调试快照。
                var currentDebug=control.CurrentDebugSnapshot;
                if(currentDebug is not null&&ReferenceEquals(_retainedEditorDebugSnapshot,currentDebug))
                    ClearRetainedEditorDebugSnapshot();
            };
            RoiFrameAvailabilityChanged+=control.RefreshRoiFrame;
            RoiInputViewportClicked+=control.NotifyRoiDrawRequested;
            context.Deactivated+=()=>
            {
                var formalSnapshot=TakeFormalSnapshotBeforeEditor();
                RoiFrameAvailabilityChanged-=control.RefreshRoiFrame;
                RoiInputViewportClicked-=control.NotifyRoiDrawRequested;
                if(ReferenceEquals(_editorDebugControl,control))
                {
                    _editorDebugControl=null;_editorDebugContext=null;
                    // The debug publication is owned by the editor context. Retire it
                    // before restoring the formal result. RefreshEditorDebugResult used
                    // to leave the old publication installed when the formal session
                    // had no published result, keeping the private debug snapshot alive
                    // after the editor was closed.
                    RefreshEditorDebugResult();
                }
            // The editor window is the ownership boundary even if the context
            // callback cleared the control reference earlier in the close turn.
            // Always retire the publication before exposing the closed state.
            RetireResultPublication();
            if(formalSnapshot is {IsComplete:true}&&SelectedNode?.Id==formalSnapshot.Identity.NodeId
                &&formalSnapshot.SourceWorkflowId==WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId)
            {
                RetainEditorDebugSnapshot(formalSnapshot);
                RefreshFormalPublishedResult();
            }
            else RefreshFormalPublishedResult();
            };
            window.SetRunAvailability(()=>Current()&&!IsRunning&&!_solutionTransitionPrepared&&!_transitionPreparationInProgress);
            TrackProductionEditor(_productionEditors,context,window);
            window.SetAssemblyPending();
            bool AssemblyCurrent()=>opening==_editorOpenGeneration&&CanOpenFormalEditor&&ReferenceEquals(_openDebugWindow,window)
                &&window.CanCompleteAssembly&&context.IsValid;
            void RetireOpening()
            {
                if(!window.CanCompleteAssembly)return; // Closing owns context release after drain.
                context.Discard();
                if(window.CanCompleteAssembly)window.Close();
            }
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Func<Task>(async () =>
            {
                try
                {
                    if(!AssemblyCurrent()){RetireOpening();return;}
                    control.BindDraft(context.PreparedDraft);
                    await control.WaitForViewReadyAsync();
                    if(!AssemblyCurrent()){RetireOpening();return;}
                    var preview=InstallResultDisplayPreview(session,node,context,control);
                    if(!AssemblyCurrent())
                    {
                        preview.Dispose();
                        RetireOpening();
                        return;
                    }
                    context.Deactivated+=preview.Dispose;
                    window.SetAssemblyReady();
                    if(!AssemblyCurrent()){RetireOpening();return;}
                    // VM 先完成首个可见参数页的组装，再把窗口交给桌面，
                    // 避免用户看到空白窗口后等待内容补挂。
                    window.Show();
                }
                catch (Exception error)
                {
                    if(!AssemblyCurrent()){RetireOpening();return;}
                    Foundation.Logging.PluginLogger.Error(error.Message,nameof(MainWindowViewModel),error);
                    try
                    {
                        window.SetAssemblyFailure(error);
                        if(AssemblyCurrent())
                        {
                            StatusText="参数页组装失败："+error.Message;
                            AddLog(StatusText);
                        }
                    }
                    finally{RetireOpening();}
                }
            }));
        }
        catch { context.Discard(); throw; }
    }
}
