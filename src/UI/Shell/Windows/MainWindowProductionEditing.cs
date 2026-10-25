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
    private readonly HashSet<SunEyeVision.UI.Features.GlobalVariableManager.GlobalVariableManagerViewModel> _variableEditors = new();

    private NodeConfigurationEditHost GetProductionEditorHost(SolutionSession session)
    {
        if (!ReferenceEquals(_productionEditorSession, session)) ReleaseProductionEditors();
        if (_productionEditorHost is null)
        {
            _productionEditorHost = session.Editing.Host;
            _productionEditorSession = session;
        }
        return _productionEditorHost;
    }

    private ProductionEditorContext? CreateProductionEditorContext(object target, SunEyeVision.UI.Shared.WorkflowNode node,
        out DataSourceQueryService? provider)
    {
        provider = null;
        // Explicit production consumers only. Other editors remain a follow-up.
        if (!EditorDraftAdapter.Supports(node.Parameters)) return null;
        var control = target switch
        {
            DefaultDebugWindow window => window.EditorControl,
            CustomDebugWindow window => window.EditorControl,
            ToolDebugControlBase editor => editor,
            _ => null
        } ?? throw new InvalidOperationException("生产编辑器缺少草稿提交接缝。");
        var session = DocumentManager.Session ?? throw new InvalidOperationException("当前没有方案编辑会话。");
        var flow = session.Document.Workflows.Single(w => w.Nodes.Any(n => ReferenceEquals(n, node)));
        var host = GetProductionEditorHost(session);
        var query = CreateDataSourceQueryService(flow.Id); // Never change a shared cached provider's live callback.
        var context = new ProductionEditorContext(host, flow.Id, node,
            () => ReferenceEquals(DocumentManager.Session, session) &&
                session.Document.Workflows.Any(w => ReferenceEquals(w, flow) && w.Nodes.Any(n => ReferenceEquals(n, node))),
            draft =>
            {
                control.SetCurrentNode(draft);
                control.SetParameters(draft.Parameters);
                control.SetDataProvider(query);
            }, publication =>
            {
                RefreshProductionPublication(session, publication, query);
            }, session.Editing);
        context.PrepareView = () => ToolEditorCommit.TryPrepare(control, out var error) ? null : error;
        context.HasPendingViewEdits = () => ToolEditorCommit.HasPendingEdits(control);
        query.ExecutionOwner = context.DebugOwner;
        query.EditScopeCancellation = context.DebugCancellation;
        query.CapturePublishedGlobals = () =>
        {
            if (!context.IsValid || !ReferenceEquals(DocumentManager.Session, session))
                throw new InvalidOperationException("调试所属方案或编辑范围已经失效。");
            return CaptureGlobalVariables(session.Document);
        };
        ToolEditorCommit.AttachDraftContext(control, context);
        query.CommitOutputConfiguration = id =>
        {
            if (id != node.Id) throw new InvalidOperationException("编辑节点作用域不一致。");
            if (!ToolEditorCommit.TryCommit(control, out var error)) throw new InvalidOperationException(error);
        };
        TrackProductionEditor(_productionEditors, context, target, ClearProductionEditorHost);
        provider = query;
        return context;
    }
    private void ClearProductionEditorHost()
    {
        if (_productionEditors.Count != 0 || _variableEditors.Count != 0) return;
        // The document, not the last window, owns publication history and registered scopes.
    }
    private void ReleaseProductionEditors()
    {
        _productionEditorSession?.Editing.Close();
        ReleaseProductionEditorContexts(_productionEditors);
        foreach (var model in _variableEditors) model.Discard();
        _variableEditors.Clear();
        _productionEditorHost = null;
        _productionEditorSession = null;
    }
    private void RefreshProductionPublication(SolutionSession session, BindingConfigurationPublication publication,
        DataSourceQueryService? query = null)
    {
        if (!ReferenceEquals(DocumentManager.Session, session)) return;
        SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.OutputBindings.Reset(session.Document);
        var globals = CaptureGlobalVariables(session.Document);
        query?.SetGlobalVariables(globals);
        foreach (var cached in _workflowDsqsCache.Values) cached.SetGlobalVariables(globals);
        ToolEditorCommit.RefreshCommittedBindings();
        if (!IsRunning)
            foreach (var affected in publication.InvalidatedNodes.Where(n => n.WorkflowId == _workflowEngine.CurrentWorkflow?.Id))
                CurrentWorkflowContext?.RemoveNodeResult(affected.NodeId);
        if (SelectedNode is { } selected && selected.Parameters is IOutputContractProvider outputs)
            UpdateModuleResults(new ContractToolResults(outputs.GetOutputContract()), selected.Name, selected.ToolType, selected.Id, selected.Parameters);
        BuildFlatRows();
    }
    private static System.Collections.Generic.Dictionary<string, object?> CaptureGlobalVariables(Solution document)
    {
        var definitions = GlobalVariable.CaptureDefinitions(document.GlobalVariables);
        return definitions.Where(v => string.IsNullOrEmpty(v.RebindingReason) && string.IsNullOrEmpty(v.BindingValidationPending))
            .ToDictionary(v => v.Name, v => v.Value);
    }
    internal static void ReleaseProductionEditorContexts(ISet<ProductionEditorContext> active)
    { foreach (var context in active.ToArray()) context.Discard(); }

    // The same ownership hook is used for Window and embedded-control initialization.
    // Unloaded can be temporary (reparenting/template changes), so only Closed/Discard releases.
    internal static void TrackProductionEditor(ISet<ProductionEditorContext> active, ProductionEditorContext context,
        object target, Action whenEmpty)
    {
        if (!context.IsActive) return;
        if (!active.Add(context)) return;
        var control = target as ToolDebugControlBase;
        Window? owner = null;
        void Closed(object? sender, EventArgs e) => context.Discard();
        void BindOwner()
        {
            var next = target as Window ?? (control is null ? null : Window.GetWindow(control));
            if (ReferenceEquals(next, owner)) return;
            if (owner is not null) owner.Closed -= Closed;
            owner = next;
            if (owner is not null) owner.Closed += Closed;
        }
        void Loaded(object sender, RoutedEventArgs e) => BindOwner();
        void Released()
        {
            context.Deactivated -= Released;
            if (control is not null) control.Loaded -= Loaded;
            if (owner is not null) owner.Closed -= Closed;
            active.Remove(context);
            if (active.Count == 0) whenEmpty();
        }
        context.Deactivated += Released;
        if (control is not null) control.Loaded += Loaded;
        BindOwner();
    }
}
