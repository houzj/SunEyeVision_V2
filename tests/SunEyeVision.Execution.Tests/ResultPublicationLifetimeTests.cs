using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Execution.Tests;

public sealed class ResultPublicationLifetimeTests
{
    [Fact]
    public void Deleting_result_node_releases_session_reader_outside_lock_and_undo_does_not_restore_result()
    {
        var registration = new ToolComponentRegistration(new ToolComponentMetadata("result-lifetime", "ResultLifetime",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(ResultLifetimeDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new[] { registration } })!;
        using var session = DocumentSession.CreateNew(directory);
        var flow = session.GetWorkflows().Single();
        var add = session.PrepareGraphEdit(flow.Id);
        var node = session.CreateNode(add, "ResultLifetime", new CanvasPoint(0, 0));
        session.CommitGraphEdit(add, "添加生命周期测试节点");
        ResultLifetimeDefinition.LastOutput = null;
        using (var batch = session.CaptureRunBatch(new[] { flow.Id }))
        using (var outcome = batch.RunAsync(_ => { }).GetAwaiter().GetResult())
            Assert.True(outcome.Workflows[flow.Id].Success);
        var output = Assert.IsType<ResultLifetimeOutput>(ResultLifetimeDefinition.LastOutput);
        output.OnDispose = () => Assert.True(Task.Run(() => session.GetWorkflows().Count).Wait(TimeSpan.FromSeconds(2)),
            "结果释放不允许持有文档锁执行插件回调。");
        var borrowed = session.AcquirePublishedResult(flow.Id, node.Id);
        Assert.NotNull(borrowed);
        var delete = session.PrepareGraphEdit(flow.Id);
        delete.DeleteNodesAndConnections(new[] { node }, Array.Empty<ConnectionDefinition>());
        session.CommitGraphEdit(delete, "删除已发布结果节点");
        Assert.Null(session.AcquirePublishedResult(flow.Id, node.Id));
        Assert.Equal(0, output.Disposals);
        borrowed!.Dispose();
        Assert.Equal(1, output.Disposals);
        Assert.Null(session.LastRuntimeReleaseError);
        Assert.True(session.Undo());
        Assert.Same(node, session.GetNodes(flow.Id).Single());
        Assert.Null(session.AcquirePublishedResult(flow.Id, node.Id));
        Assert.Equal(1, output.Disposals);
        using (var secondBatch = session.CaptureRunBatch(new[] { flow.Id }))
        using (var secondOutcome = secondBatch.RunAsync(_ => { }).GetAwaiter().GetResult())
            Assert.True(secondOutcome.Workflows[flow.Id].Success);
        var unborrowed = Assert.IsType<ResultLifetimeOutput>(ResultLifetimeDefinition.LastOutput);
        unborrowed.OnDispose = () => Assert.True(Task.Run(() => session.GetWorkflows().Count).Wait(TimeSpan.FromSeconds(2)),
            "最后一个会话 reader 释放必须在锁外执行。");
        var deleteAgain = session.PrepareGraphEdit(flow.Id);
        deleteAgain.DeleteNodesAndConnections(new[] { node }, Array.Empty<ConnectionDefinition>());
        session.CommitGraphEdit(deleteAgain, "删除无外部借用的结果节点");
        Assert.Equal(1, unborrowed.Disposals);
        Assert.Null(session.LastRuntimeReleaseError);
    }
}

public sealed record ResultLifetimeSettings : IToolSettings<ResultLifetimeSettings>
{ public ResultLifetimeSettings Freeze() => this with { }; }
public sealed class ResultLifetimeOutput : IToolOutput, IDisposable
{
    public int Disposals;
    public Action? OnDispose;
    public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?> { ["number"] = 42L };
    public void Dispose() { Interlocked.Increment(ref Disposals); OnDispose?.Invoke(); }
}
public sealed class ResultLifetimeDefinition : IToolDefinition<ResultLifetimeSettings, ResultLifetimeOutput>, IToolSettingsFields, IToolCatalogProvider
{
    public static ResultLifetimeOutput? LastOutput;
    private static readonly ToolSchema Schema = new(Array.Empty<Port>(), new[] { new Port("number", TypeRef.ScalarOf(ScalarKind.Int64)) });
    public string ToolId => "ResultLifetime";
    public ToolCatalogDescriptor Catalog { get; } = new("生命周期测试", "核验结果释放", "generic", "测试", 0);
    public ToolSchema Describe() => Schema;
    public ToolSchema SettingsSchema { get; } = new(Array.Empty<Port>(), Array.Empty<Port>());
    public InputValues ReadSettings(object settings) => new(SettingsSchema.Inputs, new Dictionary<string, object?>());
    public object MaterializeSettings(InputValues values) => new ResultLifetimeSettings();
    public IReadOnlyList<Diagnostic> Validate(ResultLifetimeSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<ResultLifetimeSettings, ResultLifetimeOutput> CreateSession() => new Invocation();
    private sealed class Invocation : IToolSession<ResultLifetimeSettings, ResultLifetimeOutput>
    {
        public ValueTask<ToolOutcome<ResultLifetimeOutput>> ExecuteAsync(ToolInvocation<ResultLifetimeSettings> invocation)
            => ValueTask.FromResult(ToolOutcome<ResultLifetimeOutput>.Completed(LastOutput = new ResultLifetimeOutput()));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}