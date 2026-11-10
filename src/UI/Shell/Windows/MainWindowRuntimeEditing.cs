using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.UI.Services.Editing;

namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    internal Func<SunEyeVision.Plugin.Contracts.OutputReference,Func<bool>,SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameLease?>? RuntimeRoiFrameBorrow {get;set;}
    internal event Action? RoiFrameAvailabilityChanged;
    internal void NotifyRoiFrameAvailabilityChanged() => RoiFrameAvailabilityChanged?.Invoke();
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
        foreach(var editor in _productionEditors.ToArray())if(!editor.IsValid)editor.Discard();
        // 提交会重建节点实例：先把选择重新绑定到当前实例，再走后续引用判定，避免选择被误判失效。
        if (SelectedNode is {} stale && FindCurrentNode(stale.Id) is {} rebound && !ReferenceEquals(rebound, stale))
        { SelectedNode = rebound; return; }
        var node = SelectedNode;
        ReleaseActiveResultView();
        ClearResultDisplay();
        if (node is null) { HasDataAnalysisControl=false; SelectedNodeDataAnalysisControl=null; ClearFormalImageSourceSelection(); return; }
        if (!OwnsSelectedDocumentNode(node)) { SelectedNode=null; return; }
        UpdateDataAnalysisControl(node).GetAwaiter().GetResult();
        RefreshFormalPublishedResult();
        RefreshFormalImageSourceSelection();
        NotifyResultDisplayChanged();
    }
    private long _editorOpenGeneration;
    private async System.Threading.Tasks.Task OpenFormalNodeEditor(NodeDefinition node)
    {
        // PROBE-KEEP：窗口打开分段计时，仅超阈值时输出（见 PerfProbe）。
        using var perf=SunEyeVision.UI.Services.Performance.PerfProbe.Begin("open",node.Name);
        var opening=++_editorOpenGeneration;
        if(_documentHostReleased||IsRunning||_solutionTransitionPrepared||_transitionPreparationInProgress)
            throw new InvalidOperationException("当前主窗正在运行、切换或关闭，不能打开参数页。");
        if (!OwnsSelectedDocumentNode(node)) throw new InvalidOperationException("节点已退出当前实际流程。");
        var session=DocumentManager.CurrentSession!;
        var workflowId=WorkflowTabViewModel.SelectedTab!.CurrentWorkflowId;
        _=session.AdmitPresentation(workflowId,node.Id);
        if (_openDebugWindow is {} previous)
        {
            if(previous is RuntimeToolEditorWindow actual)
            {if(!await actual.CloseAndDrainAsync()){previous.Activate();return;}}
            else {previous.Close();if(previous.IsVisible){previous.Activate();return;}}
        }
        if(opening!=_editorOpenGeneration||_documentHostReleased||_solutionTransitionPrepared||_transitionPreparationInProgress)return;
        if (!ReferenceEquals(DocumentManager.CurrentSession,session) || !OwnsSelectedDocumentNode(node))
            throw new InvalidOperationException("关闭旧窗口期间方案或节点已变化。");
        SelectedNode=node;
        perf.Mark("前置");
        var control=new RuntimeNodeConfigurationControl();
        var window=new RuntimeToolEditorWindow(control) {Title=node.Name};
        if (ResolveSolutionDialogOwner(null) is {} owner) window.Owner=owner;
        _openDebugWindow=window;
        perf.Mark("构造");
        window.Closed+=(_,_)=>{if(ReferenceEquals(_openDebugWindow,window))_openDebugWindow=null;};
        try { InitializeRuntimeEditor(window,node); perf.Mark("初始化"); }
        catch { control.Dispose(); window.Close(); if(ReferenceEquals(_openDebugWindow,window))_openDebugWindow=null; throw; }
    }
    private bool InitializeRuntimeEditor(object target,NodeDefinition node)
    {
        if(target is not RuntimeToolEditorWindow window) return false;
        var session=DocumentManager.CurrentSession??throw new InvalidOperationException("当前没有方案。");
        var workflowId=WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId??throw new InvalidOperationException("当前没有流程。");
        var control=window.Editor;
        ProductionEditorContext? context=null;
        bool Current()=>ReferenceEquals(DocumentManager.CurrentSession,session) && ReferenceEquals(SelectedNode,node) && WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId==workflowId;
        context=new ProductionEditorContext(session,workflowId,node,Current,control.BindDraft,
            receipt=>RefreshProductionPublication(session,receipt));
        try
        {
            ToolEditorCommit.AttachDraftContext(control,context);
            context.PrepareView=()=>control.TryCapture(out var error)?null:error;
            context.HasPendingViewEdits=()=>control.HasPendingEdits;
            context.PreserveViewAfterRebase=control.AdoptRebasedDraft;
            context.Deactivated+=control.Dispose;
            context.Deactivated+=()=>{if(window.IsLoaded)window.Close();};
            _editorDebugContext=context;_editorDebugControl=control;
            control.ConfigureDebug(context,()=>FormalImageInputs,
                ()=>Current()&&!IsRunning&&!_solutionTransitionPrepared&&!_transitionPreparationInProgress,
                message=>control.Dispatcher.BeginInvoke(new Action(()=>AddLog(message))),
                field=>
                {
                    if(!context.IsValid||control.HasPendingEdits)return null;
                    var setting=context.PreparedDraft.Configuration.Fields.Read(field) as SunEyeVision.Plugin.Contracts.ReferenceSetting;
                    return setting is null?null:RuntimeRoiFrameBorrow?.Invoke(setting.Reference,()=>context.IsValid&&!control.HasPendingEdits);
                },RefreshEditorDebugResult);
            RoiFrameAvailabilityChanged+=control.RefreshRoiFrame;
            context.Deactivated+=()=>
            {
                RoiFrameAvailabilityChanged-=control.RefreshRoiFrame;
                if(ReferenceEquals(_editorDebugControl,control)){_editorDebugControl=null;_editorDebugContext=null;RefreshEditorDebugResult();}
            };
            window.SetRunAvailability(()=>Current()&&!IsRunning&&!_solutionTransitionPrepared&&!_transitionPreparationInProgress);
            TrackProductionEditor(_productionEditors,context,window);
            window.SetAssemblyPending();
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Func<Task>(async () =>
            {
                if (!context.IsValid || !window.IsCurrent)
                {
                    context.Discard();
                    window.Close();
                    return;
                }
                try
                {
                    control.BindDraft(context.PreparedDraft);
                    await control.WaitForViewReadyAsync();
                    if (!context.IsValid || !window.IsCurrent)
                    {
                        context.Discard();
                        window.Close();
                        return;
                    }
                    var preview=InstallResultDisplayPreview(session,node,context,control);context.Deactivated+=preview.Dispose;
                    window.SetAssemblyReady();
                    // VM 先完成首个可见参数页的组装，再把窗口交给桌面，
                    // 避免用户看到空白窗口后等待内容补挂。
                    window.Show();
                }
                catch (Exception error)
                {
                    Foundation.Logging.PluginLogger.Error(error.Message,nameof(MainWindowViewModel),error);
                    window.SetAssemblyFailure(error);
                    window.Show();
                    context.Discard();
                }
            }));
            return true;
        }
        catch { context.Discard(); throw; }
    }
}
