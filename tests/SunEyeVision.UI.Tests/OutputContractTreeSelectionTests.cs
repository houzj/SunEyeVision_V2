using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Contracts;
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
            { NodeId = "a", BindingOutputSchema = schema, ExecutionOwner=f.Document.ExecutionOwner,WorkflowId=f.Flow.Id };
        int released = 0;
        var result = new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new ParameterResolver()).Run(new FrozenProbe(() => released++), request);
        DeclaredOutputSnapshot.Publish(result, request.GetInvocationIdentity(),
            new Dictionary<string, object?> { ["items"] = new double[2_000_000] },sourceWorkflowId:f.Flow.Id);
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

    [Fact]
    public void Tree_and_table_share_the_same_publication_and_retire_before_releasing_the_last_lease()=>Sta(()=>
    {
        using var f=new Fixture();using var table=new RecordingResultView();Set(f.Model,"_activeResultView",table);
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("items",DataType.NumberArray)});
        var request=new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(new ProbeParameters()){NodeId="a",BindingOutputSchema=schema,ExecutionOwner=f.Document.ExecutionOwner,WorkflowId=f.Flow.Id};
        var releases=0;var result=new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new ParameterResolver()).Run(new FrozenProbe(()=>releases++),request);
        DeclaredOutputSnapshot.Publish(result,request.GetInvocationIdentity(),new Dictionary<string,object?>{["items"]=new[]{1d,2d}},sourceWorkflowId:f.Flow.Id);
        try
        {
            f.Model.UpdateModuleResults(result,"A",nodeId:"a");
            var publication=f.Model.CurrentResultPublication!;var row=Assert.Single(f.Model.ResultTreeRoots);
            Assert.Same(publication,table.Publication);Assert.True(publication.IsCurrent());
            f.Model.UpdateModuleResults(result,"A",nodeId:"a");
            Assert.Same(publication,table.Publication);Assert.Same(row,Assert.Single(f.Model.ResultTreeRoots));
            result.Resources!.Dispose();Assert.Equal(0,releases);
            f.Select(f.B);
            Assert.False(publication.IsCurrent());Assert.Null(f.Model.CurrentResultPublication);Assert.Null(table.Publication);
            Assert.Empty(f.Model.ResultTreeRoots);Assert.Empty(f.Model.FlatResultRows);Assert.Equal(1,releases);
        }
        finally{result.Resources!.Dispose();}
    });

    [Fact]
    public void Table_failure_retires_both_surfaces_and_does_not_leave_a_borrowed_publication()=>Sta(()=>
    {
        using var f=new Fixture();using var table=new RecordingResultView{Fail=true};Set(f.Model,"_activeResultView",table);
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("items",DataType.NumberArray)});
        var request=new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(new ProbeParameters()){NodeId="a",BindingOutputSchema=schema,ExecutionOwner=f.Document.ExecutionOwner,WorkflowId=f.Flow.Id};
        var releases=0;var result=new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new ParameterResolver()).Run(new FrozenProbe(()=>releases++),request);
        DeclaredOutputSnapshot.Publish(result,request.GetInvocationIdentity(),new Dictionary<string,object?>{["items"]=new[]{1d}},sourceWorkflowId:f.Flow.Id);
        try
        {
            Assert.Throws<InvalidOperationException>(()=>f.Model.UpdateModuleResults(result,"A",nodeId:"a"));
            Assert.Null(f.Model.CurrentResultPublication);Assert.Null(table.Publication);
            Assert.Empty(f.Model.ResultTreeRoots);Assert.Empty(f.Model.FlatResultRows);
            result.Resources!.Dispose();Assert.Equal(1,releases);
        }
        finally{result.Resources!.Dispose();}
    });

    [Theory]
    [InlineData("document")] [InlineData("workflow")] [InlineData("instance")]
    public void Snapshot_from_another_owner_scope_or_recreated_node_is_not_borrowed(string mismatch)=>Sta(()=>
    {
        using var f=new Fixture();
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("items",DataType.NumberArray)});
        var owner=mismatch=="document"?new SunEyeVision.Plugin.SDK.Core.ToolExecutionOwner():f.Document.ExecutionOwner;
        var request=new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(new ProbeParameters())
            {NodeId="a",BindingOutputSchema=schema,ExecutionOwner=owner,WorkflowId=f.Flow.Id};
        var releases=0;var result=new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new ParameterResolver()).Run(new FrozenProbe(()=>releases++),request);
        DeclaredOutputSnapshot.Publish(result,request.GetInvocationIdentity(),new Dictionary<string,object?>{["items"]=new[]{1d}},
            sourceWorkflowId:mismatch=="workflow"?"other":f.Flow.Id);
        if(mismatch=="instance")
        {
            f.Flow.Nodes.Remove(f.A);
            f.Flow.Nodes.Add(new SunEyeVision.UI.Shared.WorkflowNode("a","替代节点","替代节点",""));
        }
        try{f.Model.UpdateModuleResults(result,"A",nodeId:"a");Assert.Null(f.Model.CurrentResultPublication);}
        finally{result.Resources!.Dispose();}
        Assert.Equal(1,releases);
    });

    [Fact]
    public void Binding_preparation_failure_releases_the_snapshot_reader_before_presentation()=>Sta(()=>
    {
        using var f=new Fixture();
        var schema=new ToolSchema(Array.Empty<Port>(),new[]{new Port("items",DataType.NumberArray)});
        var request=new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(new ProbeParameters())
            {NodeId="a",BindingOutputSchema=schema,ExecutionOwner=f.Document.ExecutionOwner,WorkflowId=f.Flow.Id};
        var releases=0;var result=new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new ParameterResolver()).Run(new FrozenProbe(()=>releases++),request);
        DeclaredOutputSnapshot.Publish(result,request.GetInvocationIdentity(),new Dictionary<string,object?>{["items"]=new[]{1d}},sourceWorkflowId:f.Flow.Id);
        // Fault injection in the actual binding preparation, after FromSnapshot acquired its reader.
        f.Document.GlobalVariables.Add(null!);
        try
        {
            Assert.Throws<NullReferenceException>(()=>f.Model.UpdateModuleResults(result,"A",nodeId:"a"));
            Assert.Null(f.Model.CurrentResultPublication);
        }
        finally{f.Document.GlobalVariables.Clear();result.Resources!.Dispose();}
        Assert.Equal(1,releases);
    });

    [Fact]
    public void Selection_cleanup_reentry_keeps_the_last_selection_intent()=>Sta(()=>
    {
        using var f=new Fixture();using var table=new RecordingResultView();
        Set(f.Model,"_activeResultView",table);
        table.OnClear=()=>f.Model.SetSelectedNodeIdentity(null);
        Assert.False(f.Model.SetSelectedNodeIdentity(f.B));
        Assert.Null(f.Model.SelectedNode);
    });

    private sealed class RecordingResultView:IToolResultView
    {
        private IResultValueLease? _lease;
        public IResultPublication? Publication {get;private set;}
        public bool Fail {get;set;}
        public Action? OnClear {get;set;}
        public FrameworkElement View {get;}=new System.Windows.Controls.Border();
        public IReadOnlyList<Port> Sources=>Array.Empty<Port>();
        public event EventHandler<ResultSelection>? SelectionHighlightChanged {add{} remove{}}
        public void Present(IResultPublication publication)
        {
            Clear();Publication=publication;_lease=publication.Borrow();
            if(Fail)throw new InvalidOperationException("table prepare failed");
        }
        public void Clear(){Publication=null;var lease=_lease;_lease=null;lease?.Dispose();var callback=OnClear;OnClear=null;callback?.Invoke();}
        public void Dispose()=>Clear();
    }

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
        Assert.NotEmpty(f.Model.ResultTreeRoots);
        f.Parameters.Empty = true; f.Parameters.Raise(); Drain();
        Assert.Empty(f.Model.ModuleResultTree);Assert.Empty(f.Model.ResultTreeRoots);
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
        var root = Assert.Single(f.Model.ResultTreeRoots);
        Assert.Equal("A", root.DisplayName);
        Assert.Contains(f.Model.FlatResultRows, row => row.BindingInfo?.Reference is {PortId:"name:latest"} reference && reference.NodeId==f.A.Id && reference.ScopeId==f.Flow.Id && reference.MemberFieldPath.Count==0);
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
        Assert.Equal("A", Assert.Single(f.Model.ResultTreeRoots).DisplayName);
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
        ((System.Collections.Specialized.INotifyCollectionChanged)f.Model.ResultTreeRoots).CollectionChanged += (_, args) => { if (args.NewItems?.Count > 0) published++; };
        f.Parameters.Name = "old";
        f.Parameters.BeforeRead = () => { f.Parameters.BeforeRead = null; f.Parameters.Name = "new"; f.Parameters.Raise(); };
        f.Parameters.Raise(); Drain();
        Assert.Equal(2, f.Parameters.ReadCount); Assert.Equal(1, published);
        Assert.Contains(f.Model.FlatResultRows, row => row.BindingInfo?.Reference is {PortId:"name:new"} reference && reference.NodeId==f.A.Id && reference.ScopeId==f.Flow.Id && reference.MemberFieldPath.Count==0);
        Assert.DoesNotContain(f.Model.FlatResultRows, row => row.BindingInfo?.Reference.PortId=="name:old");
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
            var tabs = new WorkflowTabControlViewModel(); Tab = tabs.SelectedTab!; Tab.Id=Flow.Id; Tab.WorkflowNodes = Flow.Nodes;
            var flat = new ObservableCollection<FlatResultRow>();
            var image = new ImageResultViewModel(Model); image.AttachCollections(new ObservableCollection<AvailableDataSource>(), flat);
            Set(Model, "<WorkflowTabViewModel>k__BackingField", tabs);
            Set(Model, "<ModuleResultTree>k__BackingField", new ObservableCollection<ModuleResultNode>());
            Set(Model, "_flatResultRows", flat); Set(Model, "_imageResult", image); Set(Model, "_openDebugWindow", Window);
            Set(Model,"_workflowContextService",new SunEyeVision.UI.Services.WorkflowContextService());
            Set(Model,"_productionEditors",new HashSet<SunEyeVision.UI.Services.Editing.ProductionEditorContext>());
            Set(Model,"<CalculationResults>k__BackingField",new ObservableCollection<SunEyeVision.UI.Shell.Windows.ResultItem>());
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
            Model.CloseResultPresentation();Window.Close();Drain();_managerField.SetValue(null,_previousManager);
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
