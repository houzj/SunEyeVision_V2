using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Execution.Tests;

/// <summary>控制流语义：分支工具按序号声明走哪一条出边，被选中的目标执行，未命中的目标标记
/// Skipped（不是失败），跳过不产生结果也不影响整轮合格判定。工具不接触节点身份，与画布连线
/// 只表达节点对关系保持一致。</summary>
public sealed class BranchControlFlowTests
{
    private static RuntimeDirectorySnapshot PrepareDirectory()
    {
        var metadata = new ToolComponentMetadata("branch-probe", "BranchProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor);
        var plain = new ToolComponentMetadata("plain-probe", "PlainProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor);
        return (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot)
            .GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[]
            {
                new[] { new ToolComponentRegistration(metadata, typeof(BranchProbeDefinition)),
                        new ToolComponentRegistration(plain, typeof(PlainProbeDefinition)) }
            })!;
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
    [InlineData(0)]
    [InlineData(1)]
    public void Selected_branch_runs_and_the_other_branch_is_skipped(int selected)
    {
        BranchProbeDefinition.Selected = selected;
        using var session = DocumentSession.CreateNew(PrepareDirectory());
        var flow = session.GetWorkflows().Single();
        var edit = session.PrepareGraphEdit(flow.Id);
        var source = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 0));
        var first = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 100));
        var second = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 200));
        edit.AddConnection(new ConnectionDefinition("edge-0", source.Id, first.Id, "RightPort", "LeftPort"));
        edit.AddConnection(new ConnectionDefinition("edge-1", source.Id, second.Id, "RightPort", "LeftPort"));
        session.CommitGraphEdit(edit, "建立两条分支");

        using var result = Run(session, flow.Id);
        var workflow = result.Workflows[flow.Id];
        Assert.True(workflow.Success, string.Join(";", workflow.Errors));
        var byNode = workflow.DeclaredOutputs.ToDictionary(output => output.Identity.NodeId, StringComparer.Ordinal);
        // Every node keeps a result slot so the canvas can show its state; the branch that was
        // not taken must report Skipped and must not report a value.
        Assert.Equal(3, byNode.Count);
        Assert.Equal(ExecutionStatus.Success, byNode[source.Id].Status);
        var taken = selected == 0 ? first.Id : second.Id;
        var skipped = selected == 0 ? second.Id : first.Id;
        Assert.Equal(ExecutionStatus.Success, byNode[taken].Status);
        Assert.Equal(ExecutionStatus.Skipped, byNode[skipped].Status);
        Assert.Empty(byNode[skipped].SelectedBranches);
    }

    [Fact]
    public void Skipped_branch_does_not_fail_the_run()
    {
        BranchProbeDefinition.Selected = 0;
        using var session = DocumentSession.CreateNew(PrepareDirectory());
        var flow = session.GetWorkflows().Single();
        var edit = session.PrepareGraphEdit(flow.Id);
        var source = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 0));
        var first = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 100));
        var second = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 200));
        edit.AddConnection(new ConnectionDefinition("edge-0", source.Id, first.Id, "RightPort", "LeftPort"));
        edit.AddConnection(new ConnectionDefinition("edge-1", source.Id, second.Id, "RightPort", "LeftPort"));
        session.CommitGraphEdit(edit, "建立两条分支");

        using var result = Run(session, flow.Id);
        var workflow = result.Workflows[flow.Id];
        // A branch that was not taken is a normal outcome: no errors are raised for it and the run
        // stays successful.
        Assert.True(workflow.Success, string.Join(";", workflow.Errors));
        Assert.Empty(workflow.Errors);
        var byNode = workflow.DeclaredOutputs.ToDictionary(output => output.Identity.NodeId, StringComparer.Ordinal);
        Assert.Equal(ExecutionStatus.Skipped, byNode[second.Id].Status);
        Assert.Equal(ExecutionStatus.Success, byNode[first.Id].Status);
    }

    [Fact]
    public void Data_flow_from_a_branch_source_still_runs_every_node()
    {
        BranchProbeDefinition.Selected = 0;
        using var session = DocumentSession.CreateNew(PrepareDirectory());
        var flow = session.GetWorkflows().Single();
        var edit = session.PrepareGraphEdit(flow.Id);
        var source = session.CreateNode(edit, "BranchProbe", new CanvasPoint(0, 0));
        var plain = session.CreateNode(edit, "PlainProbe", new CanvasPoint(0, 100));
        edit.AddConnection(new ConnectionDefinition("edge-data", source.Id, plain.Id, "RightPort", "LeftPort"));
        session.CommitGraphEdit(edit, "数据边串联");

        using var result = Run(session, flow.Id);
        var workflow = result.Workflows[flow.Id];
        Assert.True(workflow.Success, string.Join(";", workflow.Errors));
        // A plain data edge still executes the target regardless of branch selection: only a
        // branch source's own outgoing edges are subject to the selected indexes.
        Assert.Equal(2, workflow.DeclaredOutputs.Count);
    }
}

public sealed record BranchProbeSettings : IToolSettings<BranchProbeSettings> { public BranchProbeSettings Freeze() => this with { }; }

public sealed class BranchProbeOutput : IToolOutput
{
    public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?> { ["number"] = 1L };
}

/// <summary>Test tool declaring two branch outputs; reports which indexes it took.</summary>
public sealed class BranchProbeDefinition : IToolDefinition<BranchProbeSettings, BranchProbeOutput>, IToolSettingsFields, IToolCatalogProvider
{
    public static int Selected;
    private static readonly ToolSchema Schema = new(Array.Empty<Port>(),
        new[] { new Port("number", TypeRef.ScalarOf(ScalarKind.Int64)) },
        controlOutputs: new[] { new ControlOutput("first", "第一分支"), new ControlOutput("second", "第二分支") });
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
            { SelectedBranches = new[] { Selected } });
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Tool without branch outputs, used to prove plain data edges keep executing.</summary>
public sealed class PlainProbeDefinition : IToolDefinition<BranchProbeSettings, BranchProbeOutput>, IToolSettingsFields, IToolCatalogProvider
{
    private static readonly ToolSchema Schema = new(Array.Empty<Port>(),
        new[] { new Port("number", TypeRef.ScalarOf(ScalarKind.Int64)) });
    public string ToolId => "PlainProbe";
    public ToolCatalogDescriptor Catalog { get; } = new("普通测试工具", "验证数据边", "generic", "测试", 0);
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
                BusinessJudgment.Pass, Array.Empty<Diagnostic>()));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
