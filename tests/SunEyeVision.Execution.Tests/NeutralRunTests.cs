using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Execution.Tests;

// Exercises the actual Application/Execution ownership path with a neutral in-process test tool.
// It is not evidence that a packaged ImageLoad or Blob tool has passed.
public sealed class NeutralRunTests
{
    [Fact]
    public void Worker_accepts_second_value_while_owner_observes_first_change_without_deadlock()
    {
        var registration = new ToolComponentRegistration(new ToolComponentMetadata("neutral-run", "NeutralRun",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(NeutralDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new[] { registration } })!;
        using var session = DocumentSession.CreateNew(directory);
        var flow = session.GetWorkflows().Single(); var graph = session.PrepareGraphEdit(flow.Id);
        var first = session.CreateNode(graph, "NeutralRun", new CanvasPoint(0, 0));
        var second = session.CreateNode(graph, "NeutralRun", new CanvasPoint(20, 0));
        session.CommitGraphEdit(graph, "添加两个测试节点");
        using var draft = session.Editing.Host.BeginVariableEdit();
        GlobalVariable MakeVariable(string name, string nodeId) => new(name, 1L, "Integer")
        { ProducerBinding = new ProducerBinding { ReferenceExpression = ParameterSettingCanonical.Serialize(
            new OutputReference(nodeId, "number", TypeRef.ScalarOf(ScalarKind.Int64), scopeId: flow.Id)) } };
        draft.Variables.Add(MakeVariable("第一变量", first.Id));
        draft.Variables.Add(MakeVariable("第二变量", second.Id));
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, variables: draft));
        var one = session.Document.GlobalVariables.Single(variable => variable.Name == "第一变量");
        var two = session.Document.GlobalVariables.Single(variable => variable.Name == "第二变量");
        var changes = new List<long>(); var reentryRejected = false; var secondArrivedDuringCallback = false;
        NeutralDefinition.DelayedNodeId = second.Id;
        NeutralDefinition.DelayedStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NeutralDefinition.ReleaseDelay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, change) =>
        {
            if (change.Origin != DocumentChangeOrigin.RuntimeCurrentValues) return;
            changes.Add(change.Revision);
            if (changes.Count != 1) return;
            try { session.PrepareGraphEdit(flow.Id); } catch (InvalidOperationException) { reentryRejected = true; }
            NeutralDefinition.ReleaseDelay!.TrySetResult();
            secondArrivedDuringCallback = SpinWait.SpinUntil(() => Equals(two.Value, 42L), TimeSpan.FromSeconds(5));
        };
        var context = SynchronizationContext.Current; DocumentRunBatch batch;
        try { SynchronizationContext.SetSynchronizationContext(null); batch = session.CaptureRunBatch(new[] { flow.Id }); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
        using (batch)
        {
            try
            {
                var running = batch.RunAsync(_ => { });
                Assert.True(NeutralDefinition.DelayedStarted.Task.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(SpinWait.SpinUntil(() => Equals(one.Value, 42L), TimeSpan.FromSeconds(5)));
                session.DrainRuntimeChanges();
                using var result = running.GetAwaiter().GetResult();
                Assert.True(result.Workflows[flow.Id].Success);
                session.DrainRuntimeChanges();
                Assert.True(reentryRejected);
                Assert.True(secondArrivedDuringCallback);
                Assert.Equal(2, changes.Count);
                Assert.Equal(changes[0] + 1, changes[1]);
            }
            finally
            {
                NeutralDefinition.ReleaseDelay.TrySetResult();
                NeutralDefinition.DelayedNodeId = null;
                NeutralDefinition.DelayedStarted = null;
                NeutralDefinition.ReleaseDelay = null;
            }
        }
    }

    [Fact]
    public void Producer_value_is_accepted_through_actual_session_without_creating_history()
    {
        var registration = new ToolComponentRegistration(new ToolComponentMetadata("neutral-run", "NeutralRun",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(NeutralDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new[] { registration } })!;
        using var session = DocumentSession.CreateNew(directory);
        var flow = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(flow.Id);
        var node = session.CreateNode(graph, "NeutralRun", new CanvasPoint(0, 0));
        session.CommitGraphEdit(graph, "添加测试节点");
        var reference = new OutputReference(node.Id, "number", TypeRef.ScalarOf(ScalarKind.Int64), scopeId: flow.Id);
        using var variables = session.Editing.Host.BeginVariableEdit();
        variables.Variables.Add(new GlobalVariable("计数", 1L, "Integer")
        { ProducerBinding = new ProducerBinding { ReferenceExpression = ParameterSettingCanonical.Serialize(reference) } });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, variables: variables));
        var actual = Assert.Single(session.Document.GlobalVariables);
        var historyCount = session.HistoryEntries.Count;
        var changes = new List<DocumentChangeSet>();
        session.Changed += (_, change) => changes.Add(change);
        var context = SynchronizationContext.Current;
        DocumentRunBatch run;
        try { SynchronizationContext.SetSynchronizationContext(null); run = session.CaptureRunBatch(new[] { flow.Id }); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
        using (var batch = run)
        using (var result = batch.RunAsync(_ => { }).GetAwaiter().GetResult())
        {
            Assert.True(result.Workflows[flow.Id].Success);
            Assert.Equal(42L, actual.Value);
            Assert.Equal(historyCount, session.HistoryEntries.Count);
        }
        var move = session.PrepareGraphEdit(flow.Id);
        move.MoveNode(node, new CanvasPoint(10, 20));
        session.CommitGraphEdit(move, "移动测试节点");
        Assert.Equal(new[] { DocumentChangeOrigin.RuntimeCurrentValues, DocumentChangeOrigin.Edit }, changes.Select(change => change.Origin));
        Assert.Equal(changes[0].Revision + 1, changes[1].Revision);
    }

    [Fact]
    public void Run_publishes_real_identity_and_retains_output_until_session_close()
    {
        NeutralOutput.Disposals = 0;
        var registration = new ToolComponentRegistration(new ToolComponentMetadata("neutral-run", "NeutralRun",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(NeutralDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new[] { registration } })!;
        var session = DocumentSession.CreateNew(directory);
        var flow = session.GetWorkflows().Single();
        var edit = session.PrepareGraphEdit(flow.Id);
        var node = session.CreateNode(edit, "NeutralRun", new CanvasPoint(0, 0));
        session.CommitGraphEdit(edit, "添加测试节点");
        var notifications = 0;
        session.RuntimeResultAvailable += (_, _) => notifications++;
        var context = SynchronizationContext.Current;
        DocumentRunBatch batch;
        try { SynchronizationContext.SetSynchronizationContext(null); batch = session.CaptureRunBatch(new[] { flow.Id }); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
        using (batch)
        {
        using (var result = batch.RunAsync(_ => { }).GetAwaiter().GetResult())
        {
            Assert.True(result.Workflows[flow.Id].Success);
            using var reader = session.AcquirePublishedResult(flow.Id, node.Id);
            Assert.NotNull(reader);
            Assert.Equal(session.ResultOwnerId, reader!.Snapshot.Identity.DocumentSessionId);
            Assert.Equal(42L, reader.Values!.Get<long>("number"));
        }
        Assert.Equal(0, notifications);
        session.DrainRuntimeChanges();
        Assert.Equal(1, notifications);
        Assert.Equal(0, NeutralOutput.Disposals);
        batch.Dispose();
        }
        session.Dispose();
        Assert.Equal(1, NeutralOutput.Disposals);
    }
}

public sealed record NeutralSettings : IToolSettings<NeutralSettings> { public NeutralSettings Freeze() => this with { }; }
public sealed class NeutralOutput : IToolOutput, IDisposable
{
    public static int Disposals;
    public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?> { ["number"] = 42L };
    public void Dispose() => Interlocked.Increment(ref Disposals);
}
public sealed class NeutralDefinition : IToolDefinition<NeutralSettings, NeutralOutput>, IToolSettingsFields, IToolCatalogProvider
{
    public static string? DelayedNodeId;
    public static TaskCompletionSource? DelayedStarted;
    public static TaskCompletionSource? ReleaseDelay;
    private static readonly ToolSchema Schema = new(Array.Empty<Port>(), new[] { new Port("number", TypeRef.ScalarOf(ScalarKind.Int64)) });
    public string ToolId => "NeutralRun";
    public ToolCatalogDescriptor Catalog { get; } = new("中立测试工具", "验证运行资源所有权", "generic", "测试", 0);
    public ToolSchema Describe() => Schema;
    public ToolSchema SettingsSchema { get; } = new(Array.Empty<Port>(), Array.Empty<Port>());
    public InputValues ReadSettings(object settings) => new(SettingsSchema.Inputs, new Dictionary<string, object?>());
    public object MaterializeSettings(InputValues values) => new NeutralSettings();
    public IReadOnlyList<Diagnostic> Validate(NeutralSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<NeutralSettings, NeutralOutput> CreateSession() => new NeutralSession();
    private sealed class NeutralSession : IToolSession<NeutralSettings, NeutralOutput>
    {
        public async ValueTask<ToolOutcome<NeutralOutput>> ExecuteAsync(ToolInvocation<NeutralSettings> invocation)
        {
            if (invocation.Context.Identity.NodeId == DelayedNodeId)
            {
                DelayedStarted?.TrySetResult();
                await (ReleaseDelay?.Task ?? Task.CompletedTask).WaitAsync(invocation.Context.Cancellation);
            }
            return ToolOutcome<NeutralOutput>.Completed(new NeutralOutput());
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
