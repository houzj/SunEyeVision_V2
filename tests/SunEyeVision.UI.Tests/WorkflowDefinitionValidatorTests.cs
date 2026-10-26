using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Xunit;
using WorkflowDefinition = SunEyeVision.Workflow.Workflow;

namespace SunEyeVision.UI.Tests;

public sealed class WorkflowDefinitionValidatorTests
{
    [Fact]
    public void ValidDefinitionPasses()
    {
        var workflow = new WorkflowDefinition("wf-1", "流程1");
        workflow.AddNode(Node("n1", "ImageLoad"));
        workflow.AddNode(Node("n2", "BlobAnalysis"));
        workflow.AddConnection(new WorkflowConnection("c1", "n1", "n2"));

        Assert.Empty(WorkflowDefinitionValidator.Validate(workflow));
    }

    [Fact]
    public void UnknownConnectionEndpointIsRejected()
    {
        var workflow = new WorkflowDefinition("wf-1", "流程1");
        workflow.AddNode(Node("n1", "ImageLoad"));
        workflow.Connections.Add(new WorkflowConnection("c1", "n1", "missing"));

        var errors = WorkflowDefinitionValidator.Validate(workflow);

        Assert.Contains(errors, error => error.Contains("不存在的节点"));
    }

    [Fact]
    public void UnknownBindingSourceIsRejected()
    {
        var workflow = new WorkflowDefinition("wf-1", "流程1");
        var target = Node("n1", "BlobAnalysis");
        target.ParamSettings.Settings = new List<ParamSetting>
        {
            ParamSetting.CreateBinding("Threshold", "missing", "Value")
        };
        workflow.AddNode(target);

        var errors = WorkflowDefinitionValidator.Validate(workflow);

        Assert.Contains(errors, error => error.Contains("不存在的源节点"));
    }

    [Fact]
    public void MissingSubroutineIsRejected()
    {
        var workflow = new WorkflowDefinition("wf-1", "流程1");
        workflow.AddNode(new WorkflowNodeBase("n1", "子程序1", "子程序1", "Subroutine")
        {
            SubroutineId = "missing-subroutine"
        });

        var errors = WorkflowDefinitionValidator.Validate(workflow, _ => null);

        Assert.Contains(errors, error => error.Contains("不存在的子程序工作流"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void CrossWorkflowCycleIsRejectedBeforeExecution(int length)
    {
        var flows = new Dictionary<string, WorkflowDefinition>();
        for (var i = 0; i < length; i++) flows.Add($"wf-{i}", new WorkflowDefinition($"wf-{i}", $"流程{i}"));
        for (var i = 0; i < length; i++)
            flows[$"wf-{i}"].AddNode(new WorkflowNodeBase($"call-{i}", "调用", "调用", "Subroutine")
            { SubroutineId = $"wf-{(i + 1) % length}" });

        var errors = WorkflowDefinitionValidator.Validate(flows["wf-0"], id => flows.GetValueOrDefault(id), false);

        Assert.Contains(errors, error => error.Contains(length == 2
            ? "wf-0 → wf-1 → wf-0" : "wf-0 → wf-1 → wf-2 → wf-0"));
    }

    [Fact]
    public void SharedSubroutineInDiamondIsNotACycle()
    {
        var root = new WorkflowDefinition("root", "主流程");
        var left = new WorkflowDefinition("left", "左");
        var right = new WorkflowDefinition("right", "右");
        var leaf = new WorkflowDefinition("leaf", "共享");
        root.AddNode(new WorkflowNodeBase("l", "左", "左", "Subroutine") { SubroutineId = left.Id });
        root.AddNode(new WorkflowNodeBase("r", "右", "右", "Subroutine") { SubroutineId = right.Id });
        left.AddNode(new WorkflowNodeBase("leaf-l", "共享", "共享", "Subroutine") { SubroutineId = leaf.Id });
        right.AddNode(new WorkflowNodeBase("leaf-r", "共享", "共享", "Subroutine") { SubroutineId = leaf.Id });
        var flows = new Dictionary<string, WorkflowDefinition> { [root.Id] = root, [left.Id] = left, [right.Id] = right, [leaf.Id] = leaf };

        Assert.Empty(WorkflowDefinitionValidator.Validate(root, id => flows.GetValueOrDefault(id), false));
    }

    [Fact]
    public void MissingNestedSubroutineIsRejectedAtRootValidation()
    {
        var root = new WorkflowDefinition("root", "主流程");
        var child = new WorkflowDefinition("child", "子流程");
        root.AddNode(new WorkflowNodeBase("c", "调用", "调用", "Subroutine") { SubroutineId = child.Id });
        child.AddNode(new WorkflowNodeBase("m", "缺失", "缺失", "Subroutine") { SubroutineId = "missing" });

        Assert.Contains(WorkflowDefinitionValidator.Validate(root, id => id == child.Id ? child : null, false),
            error => error.Contains("child") && error.Contains("missing"));
    }

    private static WorkflowNodeBase Node(string id, string toolType) =>
        new(id, id, id, toolType);
}
