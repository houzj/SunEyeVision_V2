using System.Reflection;
using SunEyeVision.Application;
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
        public ValueTask<ToolOutcome<NeutralOutput>> ExecuteAsync(ToolInvocation<NeutralSettings> invocation)
            => ValueTask.FromResult(ToolOutcome<NeutralOutput>.Completed(new NeutralOutput()));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
