using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private SolutionSession? _productionEditorSession;
    private NodeConfigurationEditHost? _productionEditorHost;
    private readonly HashSet<ProductionEditorContext> _productionEditors = new();

    private ProductionEditorContext? CreateProductionEditorContext(object target, SunEyeVision.UI.Shared.WorkflowNode node,
        out DataSourceQueryService? provider)
    {
        provider = null;
        // First production consumer only. Other editors remain an explicit follow-up.
        if (node.Parameters?.GetType().FullName != "SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters") return null;
        var control = target switch
        {
            DefaultDebugWindow window => window.EditorControl,
            ToolDebugControlBase editor => editor,
            _ => null
        } ?? throw new InvalidOperationException("ProtocolParse 编辑器缺少草稿提交接缝。");
        var session = DocumentManager.Session ?? throw new InvalidOperationException("当前没有方案编辑会话。");
        var flow = session.Document.Workflows.Single(w => w.Nodes.Any(n => ReferenceEquals(n, node)));
        if (!ReferenceEquals(_productionEditorSession, session) || _productionEditors.Count == 0)
        {
            _productionEditorHost = new NodeConfigurationEditHost(session.Document);
            _productionEditorSession = session;
        }
        var query = CreateDataSourceQueryService(flow.Id); // Never change a shared cached provider's live callback.
        var context = new ProductionEditorContext(_productionEditorHost!, flow.Id, node,
            () => ReferenceEquals(DocumentManager.Session, session) &&
                session.Document.Workflows.Any(w => ReferenceEquals(w, flow) && w.Nodes.Any(n => ReferenceEquals(n, node))),
            draft =>
            {
                control.SetCurrentNode(draft);
                control.SetParameters(draft.Parameters);
                control.SetDataProvider(query);
            }, publication =>
            {
                SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.OutputBindings.Reset(session.Document);
                var globals = session.Document.GlobalVariables.Where(v => string.IsNullOrEmpty(v.RebindingReason) && string.IsNullOrEmpty(v.BindingValidationPending))
                    .ToDictionary(v => v.Name, v => v.Value);
                query.SetGlobalVariables(globals);
                foreach (var cached in _workflowDsqsCache.Values) cached.SetGlobalVariables(globals);
                ToolEditorCommit.RefreshCommittedBindings();
                if (!IsRunning)
                    foreach (var affected in publication.InvalidatedNodes.Where(n => n.WorkflowId == _workflowEngine.CurrentWorkflow?.Id))
                        CurrentWorkflowContext?.RemoveNodeResult(affected.NodeId);
                if (ReferenceEquals(SelectedNode, node) && node.Parameters is IOutputContractProvider outputs)
                    UpdateModuleResults(new ContractToolResults(outputs.GetOutputContract()), node.Name, node.ToolType, node.Id, node.Parameters);
            });
        ToolEditorCommit.AttachDraftContext(control, context);
        query.CommitOutputConfiguration = id =>
        {
            if (id != node.Id) throw new InvalidOperationException("编辑节点作用域不一致。");
            if (!ToolEditorCommit.TryCommit(control, out var error)) throw new InvalidOperationException(error);
        };
        _productionEditors.Add(context);
        if (target is Window owner)
            owner.Closed += (_, _) => { context.Dispose(); _productionEditors.Remove(context); };
        provider = query;
        return context;
    }
}
