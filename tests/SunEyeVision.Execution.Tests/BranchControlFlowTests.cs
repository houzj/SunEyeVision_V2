using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Execution.Tests;

/// <summary>控制流语义：选中的分支执行，未命中的分支标记 Skipped（不是失败），
/// 跳过不产生结果也不影响整轮合格判定。</summary>
public sealed class BranchControlFlowTests
{
    private static RuntimeDirectorySnapshot PrepareDirectory()
    {
        var registration = new ToolComponentRegistration(new ToolComponentMetadata("branch-probe", "BranchProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(BranchProbeDefinition));
        return (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot)
            .GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new[] { registration } })!;
    }

    private static DocumentRunResult Run(DocumentSession session, string workflowId)
    {
        var context = SynchronizationContext.Current;
        DocumentRunBatch batch;
        try { SynchronizationContext.SetSynchronizationContext(null); batch = session.CaptureRunBatch(new[] { workflowId }); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
        using (batch) return batch.RunAsync(_ => { }).GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("ng")]
    public void Selected_branch_runs_and_the_other_branch_is_skipped(string selected)
    {
        BranchProbeDefinition.Selected = selected;
        using var session = DocumentSession.CreateNew(PrepareDirectory());
        var flow = session.GetWorkflows().Single();
        var edit = session.PrepareGraphEdit(flow.Id);
        var source = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 0));
        var okNode = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 100));
        var ngNode = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 200));
        edit.AddConnection(new ConnectionDefinition("edge-ok", source.Id, okNode.Id, "control.ok", ""));
        edit.AddConnection(new ConnectionDefinition("edge-ng", source.Id, ngNode.Id, "control.ng", ""));
        session.CommitGraphEdit(edit, "建立两条分支");

        using var result = Run(session, flow.Id);
        var workflow = result.Workflows[flow.Id];
        Assert.True(workflow.Success, string.Join(";", workflow.Errors));
        var byNode = workflow.DeclaredOutputs.ToDictionary(output => output.Identity.NodeId, StringComparer.Ordinal);
        // Every node keeps a result slot so the canvas can show its state; the branch that was
        // not taken must report Skipped and must not report a value.
        Assert.Equal(3, byNode.Count);
        Assert.Equal(ExecutionStatus.Success, byNode[source.Id].Status);
        Assert.Equal(ExecutionStatus.Success, byNode[selected == "ok" ? okNode.Id : ngNode.Id].Status);
        var skipped = byNode[selected == "ok" ? ngNode.Id : okNode.Id];
        Assert.Equal(ExecutionStatus.Skipped, skipped.Status);
        Assert.Empty(skipped.SelectedControlOutputs);
    }

    [Fact]
    public void Skipped_branch_does_not_fail_the_run_nor_produce_results()
    {
        BranchProbeDefinition.Selected = "ok";
        using var session = DocumentSession.CreateNew(PrepareDirectory());
        var flow = session.GetWorkflows().Single();
        var edit = session.PrepareGraphEdit(flow.Id);
        var source = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 0));
        var okNode = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 100));
        var ngNode = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 200));
        edit.AddConnection(new ConnectionDefinition("edge-ok", source.Id, okNode.Id, "control.ok", ""));
        edit.AddConnection(new ConnectionDefinition("edge-ng", source.Id, ngNode.Id, "control.ng", ""));
        session.CommitGraphEdit(edit, "建立两条分支");

        using var result = Run(session, flow.Id);
        var workflow = result.Workflows[flow.Id];
        // A branch that was not taken is a normal outcome: no errors are raised for it, the run
        // stays successful, and only its own result slot records Skipped.
        Assert.True(workflow.Success, string.Join(";", workflow.Errors));
        Assert.Empty(workflow.Errors);
        var byNode = workflow.DeclaredOutputs.ToDictionary(output => output.Identity.NodeId, StringComparer.Ordinal);
        Assert.Equal(ExecutionStatus.Skipped, byNode[ngNode.Id].Status);
        Assert.Equal(ExecutionStatus.Success, byNode[okNode.Id].Status);
    }

    [Fact]
    public void Data_flow_without_control_edges_still_runs_every_node()
    {
        BranchProbeDefinition.Selected = "ok";
        using var session = DocumentSession.CreateNew(PrepareDirectory());
        var flow = session.GetWorkflows().Single();
        var edit = session.PrepareGraphEdit(flow.Id);
        var first = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 0));
        var second = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 100));
        edit.AddConnection(new ConnectionDefinition("edge-data", first.Id, second.Id, "number", ""));
        session.CommitGraphEdit(edit, "数据边串联");

        using var result = Run(session, flow.Id);
        var workflow = result.Workflows[flow.Id];
        Assert.True(workflow.Success, string.Join(";", workflow.Errors));
        // A plain data edge still executes the target regardless of any control selection.
        Assert.Equal(2, workflow.DeclaredOutputs.Count);
    }
}

public sealed record BranchProbeSettings : IToolSettings<BranchProbeSettings> { public BranchProbeSettings Freeze() => this with { }; }

public sealed class BranchProbeOutput : IToolOutput
{
    public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?> { ["number"] = 1L };
}

/// <summary>Test tool that declares two control outputs and reports the selected one.</summary>
public sealed class BranchProbeDefinition : IToolDefinition<BranchProbeSettings, BranchProbeOutput>, IToolSettingsFields, IToolCatalogProvider
{
    public static string Selected = "ok";
    private static readonly ToolSchema Schema = new(Array.Empty<Port>(),
        new[] { new Port("number", TypeRef.ScalarOf(ScalarKind.Int64)) },
        controlOutputs: new[] { new ControlOutput("ok", "OK 分支"), new ControlOutput("ng", "NG 分支") });
    public string ToolId => "BranchProbe";
    public ToolCatalogDescriptor Catalog { get; } = new("分支测试工具", "验证控制流择一", "generic", "测试", 0);
    public ToolSchema Describe() => Schema;
    public ToolSchema SettingsSchema { get; } = new(Array.Empty<Port>(), Array.Empty<Port>());
    public InputValues ReadSettings(object settings) => new(SettingsSchema.Inputs, new Dictionary<string, object?>());
    public object MaterializeSettings(InputValues values) => new BranchProbeSettings();
    public IReadOnlyList<Diagnostic> Validate(BranchProbeSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<BranchProbeSettings, BranchProbeOutput> CreateSession() => new Session();
    private sealed class Session : IToolSession<BranchProbeSettings, BranchProbeOutput>
    {
        public ValueTask<ToolOutcome<BranchProbeOutput>> ExecuteAsync(ToolInvocation<BranchProbeSettings> invocation)
            => ValueTask.FromResult(new ToolOutcome<BranchProbeOutput>(true, new BranchProbeOutput(),
                BusinessJudgment.Pass, Array.Empty<Diagnostic>())
            { SelectedControlOutputs = new[] { Selected } });
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
