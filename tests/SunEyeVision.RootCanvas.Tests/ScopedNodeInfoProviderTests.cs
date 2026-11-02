using System.IO;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("VM canvas UI")]
public sealed class ScopedNodeInfoProviderTests
{
    [Fact]
    public void Scope_rejects_removed_flow_missing_configuration_and_foreign_generation()
    {
        var root=Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("缺少隔离测试目录。");
        var manager=new SolutionManager(Path.Combine(root,Guid.NewGuid().ToString("N")),RootCanvasFixture.Directory);
        var document=manager.CreateUntitledSolution();var flow=document.Workflows[0];
        var node=new WorkflowNodeBase("source","source","source","ImageLoad")
            {RuntimeConfiguration=RuntimeToolConfiguration.Create(RootCanvasFixture.Directory,"ImageLoad")};
        flow.Nodes.Add(node);
        var provider=new ScopedNodeInfoProvider(document,flow.Id);
        Assert.Null(provider.GetSchema("missing"));Assert.NotNull(provider.GetSchema(node.Id));
        var original=node.RuntimeConfiguration;node.RuntimeConfiguration=null;
        Assert.Throws<InvalidOperationException>(()=>provider.GetSchema(node.Id));node.RuntimeConfiguration=original;
        var payload=Environment.GetEnvironmentVariable("SUNEYE_ROOT_TEST_PAYLOAD")??throw new InvalidOperationException("缺少封包。");
        var foreign=new ReleasePayloadAuthority(payload).PrepareRuntimeDirectory();
        node.RuntimeConfiguration=RuntimeToolConfiguration.Create(foreign,"ImageLoad");
        Assert.Throws<InvalidOperationException>(()=>provider.GetSchema(node.Id));node.RuntimeConfiguration=original;
        document.Workflows.Remove(flow);
        Assert.Throws<InvalidOperationException>(()=>provider.GetSchema(node.Id));
    }
}
