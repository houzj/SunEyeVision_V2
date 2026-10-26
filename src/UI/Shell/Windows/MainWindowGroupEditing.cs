using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.UI.Services.Editing;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    internal ProductionEditorContext CreateGroupEditorContext(string parentId,string nodeId,string childId,
        Action<SubroutineParameters> bindDraft,out DataSourceQueryService provider)
    {
        var session=DocumentManager.Session??throw new InvalidOperationException("没有当前方案编辑会话。");
        var parent=session.Document.Workflows.Single(w=>w.Id==parentId);
        var node=parent.Nodes.Single(n=>n.Id==nodeId);
        var child=session.Document.Workflows.Single(w=>w.Id==childId);
        if(node.SubroutineId!=childId||node.Parameters is not SubroutineParameters)throw new InvalidOperationException("Group拥有者或参数已经失效。");
        var host=GetProductionEditorHost(session);var query=CreateDataSourceQueryService(parentId);provider=query;
        var context=new ProductionEditorContext(host,parentId,node,
            ()=>ReferenceEquals(DocumentManager.Session,session)&&session.Document.Workflows.Contains(parent)&&session.Document.Workflows.Contains(child)
                &&parent.Nodes.Contains(node)&&node.SubroutineId==childId,
            draft=>bindDraft((SubroutineParameters)draft.Parameters),
            publication=>
            {
                RefreshProductionPublication(session,publication,query);
                if(node.Parameters is IOutputContractProvider outputs)
                    UpdateModuleResults(new ContractToolResults(outputs.GetOutputContract()),node.Name,node.ToolType,node.Id,node.Parameters);
            },session.Editing,allowRelated:(workflowId,related)=>WorkflowOwnership.IsInternalNode(session.Document,child,parentId,workflowId,related));
        // 绑定选择器确认只校验当前候选，确定/保存由文档宿主统一发布。
        query.CommitOutputConfiguration=id=>
        {
            if(id!=nodeId)throw new InvalidOperationException("Group绑定作用域不一致。");
            if(!context.TryValidateEditor(out var error))throw new InvalidOperationException(error);
        };
        return context;
    }
    internal void AttachGroupEditor(DefaultDebugWindow window,ProductionEditorContext context)
    {
        var control=window.ParameterControl;
        if(control is not ISubroutineParameterEditor groupEditor)throw new InvalidOperationException("Group控件未实现候选参数重绑定合同。");
        var session=DocumentManager.Session??throw new InvalidOperationException("Group编辑文档已经关闭。");
        var child=session.Document.Workflows.Single(w=>w.Id==((WorkflowNodeBase)context.FormalNode).SubroutineId);
        var page=new GroupRunParametersControl(new GroupRunParameterSession(context,session.Document,
            (id,node)=>WorkflowOwnership.IsInternalNode(session.Document,child,context.Identity.WorkflowId,id,node)),()=>GetMainImageControl?.Invoke());
        groupEditor.SetRunParameterEditor(page);
        ToolEditorCommit.AttachDraftContext(control,context);
        context.PrepareView=()=>!ToolEditorCommit.TryPrepare(control,out var error)?error:page.TryPrepare(out error)?null:error;
        context.HasPendingViewEdits=()=>ToolEditorCommit.HasPendingEdits(control)||page.HasPendingConfigurationEdits;
        context.Deactivated+=()=>{control.IsEnabled=false;page.Deactivate();};
        TrackProductionEditor(_productionEditors,context,window,ClearProductionEditorHost);
    }
}
