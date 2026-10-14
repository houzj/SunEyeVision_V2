using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Tool.PointPointMeasure;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ModeAndSubroutineBindingRegressionTests
{
    [Fact]
    public void Coordinate_mode_ignores_inactive_shape_marker_and_binding_but_shape_mode_still_blocks()
    {
        var p = new PointPointMeasureParameters { P1InputMode = PointInputMode.ByCoordinate, P2InputMode = PointInputMode.ByCoordinate };
        p.RebindingRequired["P1Point"] = "旧图形不适配";
        var setting = ParamSetting.CreateBinding("P1Point", "missing", "value");
        var node = new WorkflowNodeBase("target", "target", "target", "PointPointMeasure") { Parameters = p };
        node.ParamSettings.SetSetting(setting);
        Assert.False(BindingRelationCatalog.Describe(node).Single(i => i.Key == "P1Point").Active);
        var request = new ToolExecutionRequest(ToolParameterSnapshot.Create(p)) { ParameterBindings = new ParameterBindingSet(new[] { ParameterBindingDefinition.FromSetting(setting) }) };
        Assert.True(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(request).IsValid);
        var flow = new SunEyeVision.Workflow.Workflow("f", "f"); flow.Nodes.Add(node);
        OutputBindingChangeService.EnsureExecutable(flow);
        p.P1InputMode = PointInputMode.ByShape;
        Assert.Throws<InvalidOperationException>(() => OutputBindingChangeService.EnsureExecutable(flow));
        Assert.False(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(new ToolExecutionRequest(p)).IsValid);
    }

    [Fact]
    public void Design_node_subroutine_id_supplies_child_image_port_before_runtime_injection()
    {
        var parentParameters = new SubroutineParameters();
        parentParameters.Inputs.Add(new SubroutineInputConfig { Name = "in_1", TypeName = "Image" });
        var parent = new WorkflowNodeBase("parent", "parent", "parent", "Subroutine") { SubroutineId = "child", Parameters = parentParameters };
        var childParameters = new MeasureGuideParameters { ImageSourceRef = new ImageSourceReference("__SubroutineInputs__", "in_1", "子程序输入", "in_1") };
        var childNode = new WorkflowNodeBase("blob", "blob", "blob", "BlobAnalysis") { Parameters = childParameters };
        childParameters.BindingValidationPending["ImageSource"] = "子程序输入端口契约暂不可用，等待校验";
        var root = new SunEyeVision.Workflow.Workflow("root", "root"); root.Nodes.Add(parent);
        var child = new SunEyeVision.Workflow.Workflow("child", "child"); child.Nodes.Add(childNode);
        var solution = new Solution(); solution.Workflows.Add(root); solution.Workflows.Add(child);
        var service = new OutputBindingChangeService(); service.Reset(solution); service.ReconcileAll();
        Assert.Empty(childParameters.BindingValidationPending); Assert.Empty(childParameters.RebindingRequired);
        Assert.NotNull(childParameters.ImageSourceRef);
        OutputBindingChangeService.EnsureExecutable(child);
        parentParameters.Inputs.Clear(); service.ReconcileAll();
        Assert.NotEmpty(childParameters.BindingValidationPending);
    }
}
