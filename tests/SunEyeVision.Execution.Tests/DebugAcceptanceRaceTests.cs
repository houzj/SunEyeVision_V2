using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Execution.Tests;

public sealed class DebugAcceptanceRaceTests
{
    [Fact]
    public void Source_republication_during_candidate_preparation_rejects_old_private_values()
    {
        var registrations = new[] {
            new ToolComponentRegistration(new ToolComponentMetadata("race-consumer", "RaceConsumer", ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(RaceDefinition)) };
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { registrations })!;
        using var session = DocumentSession.CreateNew(directory);
        var flow = session.GetWorkflows().Single(); var graph = session.PrepareGraphEdit(flow.Id);
        var source = session.CreateNode(graph, "RaceConsumer", new CanvasPoint(0, 0));
        var consumer = session.CreateNode(graph, "RaceConsumer", new CanvasPoint(100, 0));
        graph.AddConnection(new ConnectionDefinition("wire", source.Id, consumer.Id, "number", "p"));
        session.CommitGraphEdit(graph, "添加来源和消费者");
        var draft = session.Editing.Host.BeginEdit(flow.Id, consumer.Id);
        draft.ApplyFields(new Dictionary<string, ParameterSetting> { ["p"] = new ReferenceSetting(new OutputReference(source.Id, "number", TypeRef.ScalarOf(ScalarKind.Int64), scopeId: flow.Id)) });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { draft }));
        using (var first = session.CaptureRunBatch(new[] { flow.Id }))
        using (var firstResult = first.RunAsync(_ => { }).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult())
            Assert.True(firstResult.Workflows[flow.Id].Success);
        using var sourceReader = session.AcquirePublishedResult(flow.Id, source.Id)!;
        var old = sourceReader.Snapshot.Identity;
        using var editor = session.OpenDebugSession(flow.Id, consumer);
        var privateDraft = session.Editing.Host.BeginEdit(flow.Id, consumer.Id);
        using var debug = editor.Capture(privateDraft, null, _ => { });
        using var output = debug.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.True(output.IsSuccess);
        var originalConfiguration = privateDraft.Configuration;
        var originalRevision = originalConfiguration.CurrentRevision("p");
        RaceDefinition.DelayedNodeId = source.Id;
        RaceDefinition.DelayedStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RaceDefinition.ReleaseDelay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RaceDefinition.OwnerThread = Environment.CurrentManagedThreadId;
        RaceDefinition.Entered = new(); RaceDefinition.Continue = new();
        using var next = session.CaptureRunBatch(new[] { flow.Id });
        try
        {
            var running = next.RunAsync(_ => { });
            Assert.True(RaceDefinition.DelayedStarted.Task.Wait(TimeSpan.FromSeconds(10)));
            var release = Task.Run(() =>
            {
                if (!RaceDefinition.Entered!.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("候选准备未进入阻塞点");
                RaceDefinition.ReleaseDelay!.TrySetResult();
                if (!SpinWait.SpinUntil(() => { using var reader = session.AcquirePublishedResult(flow.Id, source.Id); return reader is not null && reader.Snapshot.Identity != old; }, TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("新来源未真实发布");
                RaceDefinition.Continue!.Set();
            });
            RaceDefinition.BlockOwner = true;
            try { Assert.Throws<InvalidOperationException>(() => debug.AcceptParameters()); }
            finally { RaceDefinition.BlockOwner = false; RaceDefinition.Continue!.Set(); }
            release.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            using var result = running.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            Assert.True(result.Workflows[flow.Id].Success);
            Assert.Same(originalConfiguration, privateDraft.Configuration);
            Assert.Equal(originalRevision, privateDraft.Configuration.CurrentRevision("p"));
            debug.Dispose(); next.Dispose();
            using (var beforeCancel = editor.Capture(privateDraft, null, _ => { }))
            {
                using var canceledOutput = beforeCancel.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                beforeCancel.Cancel();
                Assert.Throws<InvalidOperationException>(() => beforeCancel.AcceptParameters());
                Assert.Same(originalConfiguration, privateDraft.Configuration);
            }
            using (var afterAccept = editor.Capture(privateDraft, null, _ => { }))
            {
                using var acceptedOutput = afterAccept.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                afterAccept.AcceptParameters();
                var acceptedConfiguration = privateDraft.Configuration;
                Assert.True(acceptedConfiguration.CurrentRevision("p") > originalRevision);
                afterAccept.Cancel();
                Assert.Same(acceptedConfiguration, privateDraft.Configuration);
            }
        }
        finally
        {
            RaceDefinition.BlockOwner = false; RaceDefinition.Continue?.Set();
            RaceDefinition.Entered?.Dispose(); RaceDefinition.Continue?.Dispose(); RaceDefinition.Entered = null; RaceDefinition.Continue = null;
            RaceDefinition.ReleaseDelay?.TrySetResult(); RaceDefinition.DelayedNodeId = null; RaceDefinition.DelayedStarted = null; RaceDefinition.ReleaseDelay = null;
        }
    }
}

public sealed record RaceSettings(long P = 0) : IToolSettings<RaceSettings>
{
    public RaceSettings() : this(0) { }
    public RaceSettings Freeze() => this with { };
}
public sealed class RaceOutput : IToolOutput { public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?> { ["number"] = 1L }; }
public sealed class RaceDefinition : IToolDefinition<RaceSettings, RaceOutput>, IToolSettingsFields, IToolCatalogProvider, IToolParameterBindingNames, IToolManualReferenceSources
{
    public static int OwnerThread; public static volatile bool BlockOwner; public static ManualResetEventSlim? Entered, Continue;
    public static string? DelayedNodeId; public static TaskCompletionSource? DelayedStarted, ReleaseDelay;
    private static readonly ToolSchema Schema = new(Array.Empty<Port>(), new[] { new Port("number", TypeRef.ScalarOf(ScalarKind.Int64)) });
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; } = new[] { new ManualReferenceSource("number") };
    public string ToolId => "RaceConsumer";
    public ToolCatalogDescriptor Catalog { get; } = new("竞态测试", "验证私有参数接纳", "generic", "测试", 0);
    public ToolSchema Describe()
    {
        if (BlockOwner && Environment.CurrentManagedThreadId == OwnerThread)
        { Entered!.Set(); if (!Continue!.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("候选准备阻塞未释放"); }
        return Schema;
    }
    public ToolSchema SettingsSchema { get; } = new(new[] { new Port("p", TypeRef.ScalarOf(ScalarKind.Int64), EditModes: FieldEditModes.Constant | FieldEditModes.Reference) }, Array.Empty<Port>());
    public IReadOnlyDictionary<string,string> ParameterBindingNames { get; } = new Dictionary<string,string> { ["p"] = "p" };
    public InputValues ReadSettings(object settings) => new(SettingsSchema.Inputs, new Dictionary<string,object?> { ["p"] = ((RaceSettings)settings).P });
    public object MaterializeSettings(InputValues values) => new RaceSettings(values.Get<long>("p"));
    public IReadOnlyList<Diagnostic> Validate(RaceSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<RaceSettings,RaceOutput> CreateSession() => new Session();
    private sealed class Session : IToolSession<RaceSettings,RaceOutput>
    {
        public async ValueTask<ToolOutcome<RaceOutput>> ExecuteAsync(ToolInvocation<RaceSettings> invocation)
        {
            if (invocation.Context.Identity.NodeId == DelayedNodeId)
            {
                DelayedStarted?.TrySetResult();
                await (ReleaseDelay?.Task ?? Task.CompletedTask).WaitAsync(invocation.Context.Cancellation);
            }
            return ToolOutcome<RaceOutput>.Completed(new RaceOutput());
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
