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

    private static WorkflowNodeBase Node(string id, string toolType) =>
        new(id, id, id, toolType);
}
