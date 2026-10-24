using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class OutputContractTreeSelectionTests
{
    [Fact]
    public void Declared_run_uses_frozen_values_pages_and_releases_owner_on_selection() => Sta(() =>
    {
        using var f = new Fixture();
        var schema = new SunEyeVision.Plugin.Contracts.ToolSchema(Array.Empty<SunEyeVision.Plugin.Contracts.Port>(),
            new[] { new SunEyeVision.Plugin.Contracts.Port("items", SunEyeVision.Plugin.Contracts.DataType.NumberArray) });
        var request = new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(new ProbeParameters())
            { NodeId = "a", BindingOutputSchema = schema };
        int released = 0;
        var result = new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new ParameterResolver()).Run(new FrozenProbe(() => released++), request);
        DeclaredOutputSnapshot.Publish(result, request.GetInvocationIdentity(),
            new Dictionary<string, object?> { ["items"] = new double[2_000_000] });
        var before = GC.GetAllocatedBytesForCurrentThread();
        f.Model.UpdateModuleResults(result, "A", nodeId: "a");
        Assert.True(f.Model.HasModuleResults); Assert.Empty(f.Model.ModuleResultTree);
        Assert.Equal(2, f.Model.FlatResultRows.Count);
        f.Model.FlatResultRows[1].IsExpanded = true;
        Assert.InRange(f.Model.FlatResultRows.Count, 3, 131);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024);
        result.Resources!.Dispose(); Assert.Equal(0, released);
        var retired = f.Model.FlatResultRows[1];
        f.Select(f.B); Assert.Equal(1, released); Assert.Empty(f.Model.FlatResultRows);
        retired.IsExpanded = false; retired.IsExpanded = true;
        Assert.Empty(f.Model.FlatResultRows);
        f.Model.CloseResultPresentation(); Assert.Equal(1, released);
    });

    private sealed class FrozenResults : ToolResults
    {
        public FrozenResults() { }
        public FrozenResults(Action release) { Resources = new(release); SetSuccess(); }
        public override IReadOnlyList<OutputVariable> GetOutputVariables() => throw new InvalidOperationException("Frozen run must not reflect result properties again.");
    }
    private sealed class FrozenProbe(Action release) : SunEyeVision.Plugin.SDK.Core.IToolPlugin<ProbeParameters, FrozenResults>
    { public FrozenResults Run(ProbeParameters parameters) => new(release); }

    [Fact]
    public void Current_empty_contract_clears_previous_declarations() => Sta(() =>
    {
        using var f = new Fixture();
        f.Parameters.Raise(); Drain();
        Assert.NotEmpty(f.Model.ModuleResultTree);
        f.Parameters.Empty = true; f.Parameters.Raise(); Drain();
        Assert.Empty(f.Model.ModuleResultTree);
        Assert.Empty(f.Model.FlatResultRows);
    });

    [Fact]
    public void Selected_live_editor_refreshes_the_real_tree_and_coalesces_latest_notifications() => Sta(() =>
    {
        using var f = new Fixture();
        f.Parameters.Name = "superseded"; f.Parameters.Raise();
        f.Parameters.Name = "latest"; Task.Run(f.Parameters.Raise).GetAwaiter().GetResult();
        Assert.Equal(0, f.Parameters.ReadCount); // The production subscription really queued Background work.
        Drain();
        Assert.Equal(1, f.Parameters.ReadCount);
        var root = Assert.Single(f.Model.ModuleResultTree);
        Assert.Equal("A", root.DisplayName);
        Assert.Contains(f.Model.FlatResultRows, row => row.SourcePropertyName == "latest" && row.SourceNodeId == f.A.Id);
    });

    [Fact]
    public void Open_A_editor_cannot_replace_B_tree_when_A_was_not_selected_at_notification() => Sta(() =>
    {
        using var f = new Fixture(); f.Select(f.B); var sentinel = f.Sentinel("B");
        f.Parameters.Raise(); Drain();
        f.Unchanged(sentinel);
    });

    [Fact]
    public void Queued_A_notification_is_invalid_after_switching_to_B() => Sta(() =>
    {
        using var f = new Fixture(); f.Parameters.Raise(); f.Select(f.B); var sentinel = f.Sentinel("B");
        Drain(); f.Unchanged(sentinel);
    });

    [Fact]
    public void A_B_A_selection_epoch_rejects_the_old_queue_but_a_new_edit_refreshes() => Sta(() =>
    {
        using var f = new Fixture(); var notifications = new List<string?>();
        f.Model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        f.Parameters.Raise(); f.Select(f.B); f.Select(f.A); var sentinel = f.Sentinel("A reselected");
        Drain(); f.Unchanged(sentinel);
        Assert.Equal(new[] { nameof(MainWindowViewModel.SelectedNode), nameof(MainWindowViewModel.SelectedNode) }, notifications);
        f.Parameters.Raise(); Drain(); Assert.Equal(1, f.Parameters.ReadCount);
        Assert.Equal("A", Assert.Single(f.Model.ModuleResultTree).DisplayName);
    });

    [Fact]
    public void Closing_the_real_window_invalidates_queued_work_and_unsubscribes() => Sta(() =>
    {
        using var f = new Fixture(); f.Parameters.Raise(); var sentinel = f.Sentinel("closed");
        f.Window.Close(); Assert.Equal(0, f.Parameters.SubscriberCount);
        f.Parameters.Raise(); Drain(); f.Unchanged(sentinel);
    });

    [Fact]
    public void Replacing_parameters_rejects_old_work_and_close_unsubscribes_the_original_instance() => Sta(() =>
    {
        using var f = new Fixture(); f.Parameters.Raise();
        var replacement = new ProbeParameters(); f.A.Parameters = replacement; var sentinel = f.Sentinel("replaced parameters");
        Drain(); f.Unchanged(sentinel); Assert.Equal(0, replacement.ReadCount);
        f.Window.Close(); Assert.Equal(0, f.Parameters.SubscriberCount); Assert.Equal(0, replacement.SubscriberCount);
    });

    [Fact]
    public void Delete_recreate_with_the_same_ID_cannot_validate_the_old_node_instance() => Sta(() =>
    {
        using var f = new Fixture(); f.Parameters.Raise();
        f.Flow.Nodes.Remove(f.A);
        f.Flow.Nodes.Add(new SunEyeVision.UI.Shared.WorkflowNode(f.A.Id, "A replacement", "A", "") { Parameters = f.Parameters });
        // Keep the stale SelectedNode reference deliberately: collection membership must also be checked.
        var sentinel = f.Sentinel("recreated"); Drain(); f.Unchanged(sentinel);
    });

    [Fact]
    public void A_new_session_for_the_same_document_invalidates_the_old_editor_queue() => Sta(() =>
    {
        using var f = new Fixture(); var session = f.Manager.Session; f.Parameters.Raise();
        f.A.Parameters = null!; // Deliberate fixture-only absence while the real session captures its baseline.
        try { f.Manager.SetCurrentSolution(f.Document, registerInCatalog: false); }
        finally { f.A.Parameters = f.Parameters; }
        Assert.NotSame(session, f.Manager.Session);
        var sentinel = f.Sentinel("new session"); Drain(); f.Unchanged(sentinel);
    });

    [Fact]
    public void Removing_the_workflow_from_the_document_rejects_a_stale_tab_collection() => Sta(() =>
    {
        using var f = new Fixture(); f.Parameters.Raise(); f.Document.Workflows.Remove(f.Flow);
        var sentinel = f.Sentinel("removed workflow"); Drain(); f.Unchanged(sentinel);
    });

    [Fact]
    public void Replacing_the_displayed_collection_or_window_rejects_queued_work() => Sta(() =>
    {
        using var f = new Fixture(); f.Parameters.Raise();
        f.Tab.WorkflowNodes = new ObservableCollection<WorkflowNodeBase>(f.Flow.Nodes);
        var sentinel = f.Sentinel("different collection"); Drain(); f.Unchanged(sentinel);
        f.Tab.WorkflowNodes = f.Flow.Nodes;
        f.Parameters.Raise(); Set(f.Model, "_openDebugWindow", new Window());
        Drain(); f.Unchanged(sentinel);
    });

    [Fact]
    public void Selection_change_during_provider_read_cannot_publish_after_the_first_guard() => Sta(() =>
    {
        using var f = new Fixture(); ModuleResultNode? sentinel = null;
        f.Parameters.BeforeRead = () => { f.Select(f.B); sentinel = f.Sentinel("B during read"); };
        f.Parameters.Raise(); Drain();
        Assert.Equal(1, f.Parameters.ReadCount);
        Assert.Same(sentinel, Assert.Single(f.Model.ModuleResultTree));
    });

    [Fact]
    public void A_notification_during_provider_read_supersedes_that_read_before_tree_publication() => Sta(() =>
    {
        using var f = new Fixture(); var published = 0;
        f.Model.ModuleResultTree.CollectionChanged += (_, args) => { if (args.NewItems?.Count > 0) published++; };
        f.Parameters.Name = "old";
        f.Parameters.BeforeRead = () => { f.Parameters.BeforeRead = null; f.Parameters.Name = "new"; f.Parameters.Raise(); };
        f.Parameters.Raise(); Drain();
        Assert.Equal(2, f.Parameters.ReadCount); Assert.Equal(1, published);
        Assert.Contains(f.Model.FlatResultRows, row => row.SourcePropertyName == "new");
        Assert.DoesNotContain(f.Model.FlatResultRows, row => row.SourcePropertyName == "old");
    });

    private sealed class ProbeParameters : ToolParameters, INotifyOutputContractChanged, IOutputContractProvider
    {
        private Action? _changed;
        public event Action? OutputContractChanged { add => _changed += value; remove => _changed -= value; }
        public string Name { get; set; } = "value";
        public bool Empty { get; set; }
        [JsonIgnore] public int ReadCount { get; private set; }
        [JsonIgnore] public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        [JsonIgnore] public Action? BeforeRead { get; set; }
        public void Raise() => _changed?.Invoke();
        public IReadOnlyList<OutputVariable> GetOutputContract()
        {
            ReadCount++;
            var name = Name;
            BeforeRead?.Invoke();
            if (Empty) return Array.Empty<OutputVariable>();
            return new[] { new OutputVariable { Name = name, DisplayName = name, VariableType = "Double", Group = "结果", IsCollection = false, FixedCount = 1 } };
        }
    }

    // Avoid MainWindowViewModel's device/communication/application constructor. Exercise its
    // exact production subscription, selection-identity core and tree writer with real UI objects.
    private sealed class Fixture : IDisposable
    {
        private readonly FieldInfo _managerField = typeof(ServiceInitializer).GetField("_solutionManager", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousManager;
        public MainWindowViewModel Model { get; } = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
        public ProbeParameters Parameters { get; } = new();
        public SunEyeVision.UI.Shared.WorkflowNode A { get; }
        public SunEyeVision.UI.Shared.WorkflowNode B { get; } = new("b", "B", "B", "");
        public SunEyeVision.Workflow.Workflow Flow { get; } = new("flow", "flow");
        public Solution Document { get; }
        public SolutionManager Manager { get; }
        public WorkflowTabViewModel Tab { get; }
        public Window Window { get; } = new() { Width = 100, Height = 100, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        public Fixture()
        {
            A = new("a", "A", "A", "");
            Flow.Nodes.Add(A); Flow.Nodes.Add(B);
            Document = new Solution { Workflows = new List<SunEyeVision.Workflow.Workflow> { Flow } };
            var directory = Path.Combine(AppContext.BaseDirectory, "g03-fixtures", Guid.NewGuid().ToString("N"));
            Manager = new SolutionManager(directory);
            Manager.SetCurrentSolution(Document, registerInCatalog: false);
            A.Parameters = Parameters; // The probe is not a registered persisted tool subtype; persistence is outside this fixture.
            _previousManager = _managerField.GetValue(null); _managerField.SetValue(null, Manager);
            var tabs = new WorkflowTabControlViewModel(); Tab = tabs.SelectedTab!; Tab.WorkflowNodes = Flow.Nodes;
            var flat = new ObservableCollection<FlatResultRow>();
            var image = new ImageResultViewModel(Model); image.AttachCollections(new ObservableCollection<AvailableDataSource>(), flat);
            Set(Model, "<WorkflowTabViewModel>k__BackingField", tabs);
            Set(Model, "<ModuleResultTree>k__BackingField", new ObservableCollection<ModuleResultNode>());
            Set(Model, "_flatResultRows", flat); Set(Model, "_imageResult", image); Set(Model, "_openDebugWindow", Window);
            Select(A); Window.Show(); Drain(); Assert.True(Window.IsLoaded);
            Model.SubscribeOutputContractRefresh(Window, A);
            Assert.Equal(1, Parameters.SubscriberCount);
        }
        public void Select(WorkflowNodeBase node) => Assert.True(Model.SetSelectedNodeIdentity(node));
        public ModuleResultNode Sentinel(string name)
        {
            var root = new ModuleResultNode { DisplayName = name };
            Model.ModuleResultTree.Clear(); Model.ModuleResultTree.Add(root); return root;
        }
        public void Unchanged(ModuleResultNode root)
        { Assert.Equal(0, Parameters.ReadCount); Assert.Same(root, Assert.Single(Model.ModuleResultTree)); }
        public void Dispose()
        {
            Window.Close(); Drain(); _managerField.SetValue(null, _previousManager);
        }
    }
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); } catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Production Dispatcher test exceeded 15 seconds");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
