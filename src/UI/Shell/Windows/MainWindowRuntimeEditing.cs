using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.UI.Services.Editing;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private bool InitializeRuntimeEditor(object target,Shared.WorkflowNode node)
    {
        if(node.RuntimeConfiguration is null)return false;
        if(target is not DefaultDebugWindow window||window.FindName("ContentHost") is not ContentControl {Content:RuntimeNodeConfigurationControl control})
            throw new InvalidOperationException("Runtime参数窗口缺少中立配置视图。");
        var session=DocumentManager.Session??throw new InvalidOperationException("当前没有方案编辑会话。");
        var flow=session.Document.Workflows.Single(w=>w.Nodes.Any(n=>ReferenceEquals(n,node)));
        var host=GetProductionEditorHost(session);var query=CreateDataSourceQueryService(flow.Id);
        control.SetDataProvider(query);
        var context=new ProductionEditorContext(host,flow.Id,node,
            ()=>ReferenceEquals(DocumentManager.Session,session)&&session.Document.Workflows.Any(w=>ReferenceEquals(w,flow)&&w.Nodes.Any(n=>ReferenceEquals(n,node))),
            draft=>control.BindDraft(draft),publication=>RefreshProductionPublication(session,publication,query),session.Editing);
        context.PrepareView=()=>control.TryCapture(out var error)?null:error;
        context.HasPendingViewEdits=()=>control.HasPendingEdits;
        ToolEditorCommit.AttachDraftContext(control,context);
        context.Deactivated+=control.Dispose;
        try
        {
            control.BindDraft(context.DraftNode);
            TrackProductionEditor(_productionEditors,context,window,ClearProductionEditorHost);
            _nodeDebugWindows[node.Id]=window;
            window.Closed+=(_,_)=>_nodeDebugWindows.Remove(node.Id);
            window.Show();return true;
        }
        catch{context.Discard();throw;}
    }
}
