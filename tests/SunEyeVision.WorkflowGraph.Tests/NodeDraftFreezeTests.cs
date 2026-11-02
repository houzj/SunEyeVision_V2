using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
public sealed class NodeDraftFreezeTests
{
    [Fact]
    public void Real_draft_freezes_and_commits_with_admitted_directory()
    {
        var manager = new SolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, Guid.NewGuid().ToString("N")), RootCanvasFixture.Directory);
        var document = manager.CreateUntitledSolution();
        var flow = document.Workflows[0];
        var node = RootCanvasFixture.Node("source");
        flow.Nodes.Add(node);
        var host = new NodeConfigurationEditHost(document);
        var draft = host.BeginEdit(flow.Id, node.Id);
        RootCanvasFixture.SetValue(draft.Node, 9);
        Assert.NotEqual(9, RootCanvasFixture.Value(node));
        Assert.True(host.HasPendingChanges(draft));
        var frozen = host.Freeze(draft);
        Assert.Equal(9, RootCanvasFixture.Value(frozen.Node));
        host.Commit(frozen);
        Assert.Equal(9, RootCanvasFixture.Value(node));
        host.Undo();
        Assert.NotEqual(9, RootCanvasFixture.Value(node));
        host.Redo();
        Assert.Equal(9, RootCanvasFixture.Value(node));
    }

    [Fact]
    public void Frozen_draft_rejects_closed_document_and_foreign_generation()
    {
        var manager = new SolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, Guid.NewGuid().ToString("N")), RootCanvasFixture.Directory);
        var document = manager.CreateUntitledSolution();
        var flow = document.Workflows[0];
        flow.Nodes.Add(RootCanvasFixture.Node("source"));
        var host = new NodeConfigurationEditHost(document);
        var draft = host.BeginEdit(flow.Id, "source");
        var variableDraft = host.BeginVariableEdit();
        var foreign = new ReleasePayloadAuthority(Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD")!).PrepareRuntimeDirectory();
        draft.Node.RuntimeConfiguration = RuntimeToolConfiguration.Create(foreign, "ImageLoad");
        Assert.Throws<InvalidOperationException>(() => host.Freeze(draft));
        draft = host.BeginEdit(flow.Id, "source");
        document.ExecutionOwner.Retire();
        Assert.Throws<InvalidOperationException>(() => host.Freeze(draft));
        Assert.Throws<InvalidOperationException>(() => host.Freeze(variableDraft));
    }
}
