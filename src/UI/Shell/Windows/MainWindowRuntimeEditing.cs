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
    private void TryAutoBindRuntimeImageSource(SunEyeVision.Workflow.WorkflowNodeBase node,SunEyeVision.Workflow.WorkflowConnection? connection)
    {
        if(node.RuntimeConfiguration?.Schema.WorkflowImageInputId is not { } target||node.ParamSettings.GetSetting(target) is not null||
            node is not Shared.WorkflowNode uiNode||GetDataProviderForNode(uiNode) is not Plugin.SDK.Execution.Parameters.DataSourceQueryService query)return;
        var candidates=(query.BindingCandidateNodeIds?.Invoke(node.Id)??query.GetAvailableDataSources(node.Id).Select(source=>source.SourceNodeId).Distinct(StringComparer.Ordinal).ToArray()).ToHashSet(StringComparer.Ordinal);
        var incoming=connection is not null?new[]{connection}:_workflowEngine.GetWorkflow(query.WorkflowId!)!.Connections.Where(edge=>edge.TargetNodeId==node.Id).ToArray();
        foreach(var edge in incoming.Where(edge=>candidates.Contains(edge.SourceNodeId)))
        foreach(var id in new[]{edge.SourceNodeId})
        foreach(var output in query.FormalOutputContractReader?.Invoke(id)??Array.Empty<Plugin.SDK.Execution.Results.OutputVariable>())
        {
            if(output.Kind!=Plugin.SDK.Execution.Results.OutputVariableKind.Data)continue;
            Plugin.Contracts.Port port;
            try{port=Plugin.SDK.Execution.Parameters.BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
            if(port.Type.Shape!=Plugin.Contracts.ValueShape.Resource||port.Type.Resource!=Plugin.Contracts.ResourceKind.Image)continue;
            var reference=new Plugin.Contracts.OutputReference(id,port.Id,port.Type,scopeId:query.WorkflowId,storage:port.Storage);
            var setting=new Plugin.SDK.Execution.Parameters.ParamSetting{ParameterName=target,BindingType=Plugin.SDK.Execution.Parameters.BindingType.Binding,
                ReferenceExpression=Plugin.Runtime.BindingReferenceSerializer.Serialize(reference),SourceWorkflowId=query.WorkflowId,
                SourceNodeId=id,SourceProperty=port.Id,AutomaticConnectionId=edge.Id};
            node.ParamSettings.SetSetting(setting);
            AddLog($"自动绑定图像源: {node.Name} → {output.DisplayName??output.Name}");return;
        }
    }
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
            ()=>ReferenceEquals(_openDebugWindow,window)&&ReferenceEquals(DocumentManager.Session,session)
                &&session.Document.Workflows.Any(w=>ReferenceEquals(w,flow)&&w.Nodes.Any(n=>ReferenceEquals(n,node))),
            draft=>control.BindDraft(draft),publication=>RefreshProductionPublication(session,publication,query),session.Editing);
        context.PrepareView=()=>control.TryCapture(out var error)?null:error;
        context.HasPendingViewEdits=()=>control.HasPendingEdits;
        ToolEditorCommit.AttachDraftContext(control,context);
        context.Deactivated+=control.Dispose;
        try
        {
            control.BindDraft(context.DraftNode);
            control.SetDebugCapture(draft=>
            {
                if(!context.IsValid||IsRunning)throw new InvalidOperationException("请在当前方案停止工作流后调试此节点。");
                return SunEyeVision.Workflow.RuntimeDebugInvocation.Capture(flow,draft,context.DebugOwner,host.Capture().Version,
                    id=>query.GetNodeContext(id)?.Result,context.DebugCancellation,publishedOwner:session.Document.ExecutionOwner,
                    resourcePaths:SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(session.Document),
                    cameraService:SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime);
            });
            SubscribeToolExecutionCompleted(window,node,query,context);
            TrackProductionEditor(_productionEditors,context,window,ClearProductionEditorHost);
            _nodeDebugWindows[node.Id]=window;
            window.Closed+=(_,_)=>_nodeDebugWindows.Remove(node.Id);
            window.Show();return true;
        }
        catch{context.Discard();throw;}
    }
}
