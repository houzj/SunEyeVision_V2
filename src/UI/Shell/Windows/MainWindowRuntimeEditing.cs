using System;
using System.Linq;
using System.Windows;
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
        return session is not null && workflowId is not null && session.GetNodes(workflowId).Any(actual => ReferenceEquals(actual,node));
    }
    private void RefreshFormalNodeSelection()
    {
        foreach(var editor in _productionEditors.ToArray())if(!editor.IsValid)editor.Discard();
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
    private void OpenFormalNodeEditor(NodeDefinition node)
    {
        if (!OwnsSelectedDocumentNode(node)) throw new InvalidOperationException("节点已退出当前实际流程。");
        var session=DocumentManager.CurrentSession!;
        var workflowId=WorkflowTabViewModel.SelectedTab!.CurrentWorkflowId;
        _=session.AdmitPresentation(workflowId,node.Id);
        if (_openDebugWindow is {} previous)
        {
            var closed=false; void Closed(object? sender,EventArgs args)=>closed=true;
            previous.Closed+=Closed;
            try { previous.Close(); } finally { previous.Closed-=Closed; }
            if (!closed) { previous.Activate(); return; }
        }
        if (!ReferenceEquals(DocumentManager.CurrentSession,session) || !OwnsSelectedDocumentNode(node))
            throw new InvalidOperationException("关闭旧窗口期间方案或节点已变化。");
        SelectedNode=node;
        var control=new RuntimeNodeConfigurationControl();
        var window=new RuntimeToolEditorWindow(control) {Title=node.Name};
        if (ResolveSolutionDialogOwner(null) is {} owner) window.Owner=owner;
        _openDebugWindow=window;
        window.Closed+=(_,_)=>{if(ReferenceEquals(_openDebugWindow,window))_openDebugWindow=null;};
        try { InitializeRuntimeEditor(window,node); }
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
            context.Deactivated+=control.Dispose;
            context.Deactivated+=()=>{if(window.IsLoaded)window.Close();};
            window.SetRunAvailability(()=>false);
            control.BindDraft(context.PreparedDraft);
            TrackProductionEditor(_productionEditors,context,window,ClearProductionEditorHost);
            window.Show();return true;
        }
        catch { context.Discard(); throw; }
    }
}
