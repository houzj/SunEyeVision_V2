using System.Collections.Immutable;
using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

internal static class RootWorkspaceScenario
{
    internal static void Run(string dataRoot)
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Reject(Action action)
        {
            try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; }
            throw new Exception("应拒绝该工作区操作。");
        }
        var component = new ToolComponentRegistration(new ToolComponentMetadata("workspace-transaction", "TransactionProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(TransactionProbeDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { new[] { component } })!;
        using var manager = new DocumentSessionManager(directory);
        var session = manager.CreateNew(); var flow = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(flow.Id); var node = session.CreateNode(graph, "TransactionProbe", new CanvasPoint(0, 0));
        session.CommitGraphEdit(graph, "工作区测试节点");
        var displayReference = ParameterSettingCanonical.Serialize(new OutputReference(node.Id, "image", TypeRef.ResourceOf(ResourceKind.Image), scopeId: flow.Id));
        var beforeNavigationHistory = session.HistoryEntries.Count;
        var beforeNavigationRevision = session.ChangeRevision;
        session.SetNodeDisplayReference(flow.Id, node, displayReference);
        Check(node.SelectedDisplayReference == displayReference && session.HistoryEntries.Count == beforeNavigationHistory &&
            session.ChangeRevision == beforeNavigationRevision, "Display selection must not mutate parameter history or execution identity.");
        Reject(() => session.SetNodeDisplayReference(flow.Id, node, ParameterSettingCanonical.Serialize(
            new OutputReference(node.Id, "count", DataType.Integer, scopeId: flow.Id))));
        Reject(() => session.SetNodeDisplayReference(flow.Id, node, ParameterSettingCanonical.Serialize(
            new OutputReference(node.Id, "image", TypeRef.ResourceOf(ResourceKind.Image), scopeId: "other-flow"))));
        var navigationSnapshot = node.SelectedDisplayReference;
        EventHandler<NodeDisplayNavigationChange> brokenNavigation = (_, _) =>
        {
            Reject(() => session.SetNodeDisplayReference(flow.Id, node, displayReference));
            Check(Task.Run(() => session.GetWorkflows().Count).Wait(TimeSpan.FromSeconds(2)), "Navigation callback must be outside the session lock.");
            throw new InvalidOperationException("Controlled navigation observer failure");
        };
        session.NodeDisplayNavigationChanged += brokenNavigation;
        session.SetNodeDisplayReference(flow.Id, node, null);
        Check(node.SelectedDisplayReference is null && session.LastObserverError is AggregateException,
            "Observer failure must not roll back accepted display navigation.");
        session.NodeDisplayNavigationChanged -= brokenNavigation;
        session.SetNodeDisplayReference(flow.Id, node, navigationSnapshot);
        var historyBefore = session.HistoryEntries.Count; var changeBefore = session.ChangeRevision;
        var navigationChanges = new List<WorkflowNavigationChange>();
        session.WorkspaceNavigationChanged += (_, change) => navigationChanges.Add(change);
        var root = session.Editing.Host.BeginRootEdit(flow.Id);
        var parameter = session.Editing.Host.BeginEdit(flow.Id, node.Id);
        Check(session.Editing.Host.OwnsRoot(root) && session.Editing.Host.OwnsNode(flow.Id, node) && !session.Editing.Host.HasPendingChanges(root), "新根草稿须继承已有归属和待改查询语义。");
        root.Configuration = new RootWorkflowConfiguration { RunParameters = ImmutableArray.Create(
            new RootParameterProxy(Guid.NewGuid(), node.Id, node.ToolType, "threshold", "threshold", "根运行阈值")) };
        root.WorkspaceSettings = root.WorkspaceSettings with { RunIntervalMs = 250, StopOnNg = true };
        parameter.ApplyFields(new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(12L) });
        var prepared = DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { parameter }, roots: new[] { root });
        session.SetOverviewPosition(flow, new CanvasPoint(100, 200));
        Check(session.HistoryEntries.Count == historyBefore && session.ChangeRevision == changeBefore && navigationChanges.Count == 1,
            "导航不得进入参数历史或运行值通知版本。");
        var receipt = session.CommitEdit(prepared);
        Check(session.HistoryEntries.Count == historyBefore + 1 && flow.WorkspaceSettings.RunIntervalMs == 250 && flow.WorkspaceSettings.StopOnNg &&
            flow.WorkspaceSettings.OverviewX == 100 && flow.WorkspaceSettings.OverviewY == 200,
            "运行设置与根配置/参数须原子接纳并保留最新导航。");
        Check(receipt.Change!.ChangedWorkflows.Single().Aspects == (WorkflowChangedAspect.RootConfiguration | WorkflowChangedAspect.RunSettings),
            "根配置和运行设置须在同一个正式通知中说明。");
        session.SetOverviewPosition(flow, new CanvasPoint(200, 300));
        Check(session.Undo() && flow.WorkspaceSettings.RunIntervalMs == 80 && !flow.WorkspaceSettings.StopOnNg &&
            flow.WorkspaceSettings.OverviewX == 200 && flow.RootConfiguration.RunParameters.Length == 0,
            "撤销不得回放旧导航。");
        Check(node.SelectedDisplayReference == displayReference, "Undo must not replay old display navigation.");
        Check(session.Redo() && flow.WorkspaceSettings.RunIntervalMs == 250 && flow.WorkspaceSettings.OverviewY == 300 &&
            flow.RootConfiguration.RunParameters.Length == 1, "重做运行设置不得回放旧导航。");
        var owner = (ToolExecutionOwner)typeof(DocumentSession).GetProperty("ExecutionOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        var run = Guid.NewGuid(); owner.ActivateRoot(run);
        try
        {
            session.SetOverviewPosition(flow, new CanvasPoint(300, 400));
            var runningDraft = session.Editing.Host.BeginRootEdit(flow.Id);
            runningDraft.WorkspaceSettings = runningDraft.WorkspaceSettings with { RunIntervalMs = 350 };
            Reject(() => session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, roots: new[] { runningDraft })));
        }
        finally { owner.CompleteRoot(run); }
        Check(flow.WorkspaceSettings.RunIntervalMs == 250 && flow.WorkspaceSettings.OverviewX == 300,
            "运行期间只允许导航，不能绕过运行设置事务门禁。");
        var stale = session.Editing.Host.BeginRootEdit(flow.Id);
        var draft = session.Editing.Host.BeginRootEdit(flow.Id);
        draft.WorkspaceSettings = draft.WorkspaceSettings with { RunIntervalMs = 400 };
        var staged = DocumentEditProposal.Prepare(session.Editing.Host, roots: new[] { draft });
        draft.WorkspaceSettings = draft.WorkspaceSettings with { RunIntervalMs = 500 };
        session.CommitEdit(staged);
        var rebased = session.Editing.Host.RebaseAcceptedDraft(draft, staged);
        Check(flow.WorkspaceSettings.RunIntervalMs == 400 && rebased.WorkspaceSettings.RunIntervalMs == 500,
            "回执重基必须保留Prepare后的新运行设置。");
        Reject(() => stale.WorkspaceSettings = stale.WorkspaceSettings with { RunIntervalMs = 600 });
        Reject(() => rebased.WorkspaceSettings = rebased.WorkspaceSettings with { RunIntervalMs = -1 });
        Reject(() => rebased.WorkspaceSettings = rebased.WorkspaceSettings with { OverviewX = 1, OverviewY = 1 });
        var navBefore = session.NavigationRevision;
        Reject(() => session.SetOverviewPosition(flow, new CanvasPoint(4999, 1)));
        Reject(() => session.SetOverviewPosition(new WorkflowDefinition(flow.Id, flow.Name), null));
        Check(session.NavigationRevision == navBefore && flow.WorkspaceSettings.OverviewX == 300, "导航失败须保全旧位置和版本。");
        EventHandler<WorkflowNavigationChange> failingNavigation = (_, _) =>
        {
            Reject(() => session.SetOverviewPosition(flow, new CanvasPoint(1, 2)));
            Check(Task.Run(() => session.GetWorkflows().Count).Wait(TimeSpan.FromSeconds(2)), "导航观察者不能在文档锁内执行。");
            throw new InvalidOperationException("受控导航观察者失败");
        };
        session.WorkspaceNavigationChanged += failingNavigation;
        var beforeObserverHistory = session.HistoryEntries.Count;
        session.SetOverviewPosition(flow, new CanvasPoint(333, 444));
        Check(session.LastObserverError is AggregateException && session.HistoryEntries.Count == beforeObserverHistory &&
            flow.WorkspaceSettings.OverviewX == 333, "导航观察者失败须记录且不回滚成功导航或制造历史。");
        session.WorkspaceNavigationChanged -= failingNavigation;
        var participant = new SaveParticipant("工作区设置", () => session.Editing.Host.HasPendingChanges(rebased),
            host => DocumentEditProposal.Prepare(host, roots: new[] { rebased }),
            (proposal, _) => rebased = session.Editing.Host.RebaseAcceptedDraft(rebased, proposal));
        session.Editing.Register(participant);
        session.SetOverviewPosition(flow, new CanvasPoint(350, 450));
        var path = Path.Combine(dataRoot, "workspace.solution");
        var types = new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>());
        session.SaveAs(path, Path.Combine(dataRoot, "workspace-backup"), dataRoot, types, new System.Text.Json.JsonSerializerOptions(), _ => null);
        Check(flow.WorkspaceSettings.RunIntervalMs == 500 && !participant.HasPendingChanges, "保存前须统一接纳运行设置。");
        Check(!session.HasUnsavedChanges, "Successful SaveAs must establish a clean configuration/navigation baseline.");
        session.SetNodeDisplayReference(flow.Id, node, null);
        Check(session.HasUnsavedChanges, "Persistent display selection must be unsaved.");
        session.SetNodeDisplayReference(flow.Id, node, displayReference);
        Check(!session.HasUnsavedChanges, "Restoring saved display selection must restore clean state.");
        session.SetOverviewPosition(flow, new CanvasPoint(351, 451));
        Check(session.HasUnsavedChanges, "Persistent overview navigation must be unsaved.");
        session.SetOverviewPosition(flow, new CanvasPoint(350, 450));
        Check(!session.HasUnsavedChanges, "Navigation clean state must compare content, not a monotonic revision.");
        var dirtyEdit = session.Editing.Host.BeginRootEdit(flow.Id);
        dirtyEdit.WorkspaceSettings = dirtyEdit.WorkspaceSettings with { RunIntervalMs = 501 };
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, roots: new[] { dirtyEdit }));
        Check(session.HasUnsavedChanges && session.Undo() && !session.HasUnsavedChanges && session.Redo() &&
            session.HasUnsavedChanges && session.Undo() && !session.HasUnsavedChanges,
            "Undo and redo must compare the actual saved history state, not ChangeRevision.");
        for (var index = 0; index < 15; index++)
        {
            var bounded = session.Editing.Host.BeginRootEdit(flow.Id);
            bounded.WorkspaceSettings = bounded.WorkspaceSettings with { RunIntervalMs = 600 + index };
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, roots: new[] { bounded }));
        }
        Check(session.HasUnsavedChanges, "Changed history after trimming must remain dirty.");
        for (var index = 0; index < 15; index++) Check(session.Undo(), "Bounded history undo failed.");
        Check(!session.HasUnsavedChanges && flow.WorkspaceSettings.RunIntervalMs == 500,
            "Undo to a saved state at the trimmed history boundary must be clean.");
        var countBeforeNoop = session.HistoryEntries.Count;
        var noChange = session.Editing.Host.BeginRootEdit(flow.Id);
        Check(session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, roots: new[] { noChange })).Change is null &&
            session.HistoryEntries.Count == countBeforeNoop, "运行设置无变化不能制造历史。");
        var reopened = manager.Open(path, _ => types, _ => null, new System.Text.Json.JsonSerializerOptions());
        var settings = reopened.GetWorkflows().Single().WorkspaceSettings;
        Check(!reopened.HasUnsavedChanges && reopened.GetNodes(flow.Id).Single().SelectedDisplayReference == displayReference,
            "Reopen must restore display navigation and establish the actual clean baseline.");
        Check(settings.RunIntervalMs == 500 && settings.StopOnNg && settings.OverviewX == 350 && settings.OverviewY == 450,
            "保存重开必须保留运行设置与最新导航。");
        Console.WriteLine("PASS 根工作区联合编辑、独立导航、运行门禁、回执保新输入、历史与保存重开");
    }
}