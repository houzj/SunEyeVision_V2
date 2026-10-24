using BindingCompatibility = SunEyeVision.Plugin.Contracts.BindingCompatibility;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

public sealed class ScriptBindingContractB2Tests
{
    [Theory]
    [InlineData(typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage), "Image")]
    [InlineData(typeof(double), "Double")]
    [InlineData(typeof(string), "String")]
    public void Output_source_uses_vocabulary_in_script_and_subroutine_persisted_contracts(Type type, string vocabulary)
    {
        var source = new AvailableDataSource { SourceNodeId = "source", PropertyName = "value", PropertyType = type, VariableType = vocabulary };
        var script = new ScriptOutputConfig { Name = "out", Source = source };
        var sub = new SunEyeVision.Plugin.SDK.Models.SubroutineOutputConfig { Name = "out", Source = source };
        Assert.Equal(vocabulary, script.TypeName); Assert.Equal(vocabulary, sub.TypeName);
        Assert.NotNull(script.ParamType); Assert.NotNull(sub.ParamType);
        Assert.Contains("out", script.GenerateFieldCode());
        var p = new ScriptParameters(); p.Outputs.Clear(); p.Outputs.Add(script);
        var sp = new SubroutineParameters(); sp.Outputs.Add(sub);
        foreach (var parameters in new ToolParameters[] { p, sp })
        {
            var copy = ToolParameterSnapshot.Create(parameters);
            var output = ((IOutputContractProvider)copy).GetOutputContract().Single(o => o.Name == "out");
            Assert.Equal(vocabulary, output.VariableType); Assert.NotNull(output.FieldId);
        }
    }

    public sealed class ImageContractParameters : ToolParameters, IOutputContractProvider
    {
        public IReadOnlyList<SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable> GetOutputContract() => new[] {
            new SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable { Name = "OutputImage", VariableType = "Image",
                DeclaredType = typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage), IsCollection = false, FixedCount = 1 } };
    }

    [Fact]
    public void Window_paramsettings_projection_revalidates_old_pending_marker_using_script_row_contract()
    {
        var p = new ScriptParameters(); p.Inputs.Clear();
        p.Inputs.Add(new ScriptInputConfig { Name = "input1", TypeName = "Image", BindingSource = "source.OutputImage" });
        p.BindingValidationPending["input1"] = "输入类型要求未声明完整，等待校验";
        var source = new WorkflowNodeBase("source", "source", "source", "test") { Parameters = new ImageContractParameters() };
        var target = new WorkflowNodeBase("target", "Script1", "Script1", "Script") { Parameters = p };
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("input1", "source", "OutputImage"));
        var flow = new SunEyeVision.Workflow.Workflow("f", "f"); flow.Nodes.Add(source); flow.Nodes.Add(target);
        flow.Connections.Add(new WorkflowConnection("source-target", source.Id, target.Id));
        var solution = new Solution(); solution.Workflows.Add(flow);
        var service = new OutputBindingChangeService(); service.Reset(solution); service.ReconcileAll();
        Assert.Empty(p.BindingValidationPending); Assert.Empty(p.RebindingRequired);
        Assert.Equal("source.OutputImage", p.Inputs[0].BindingSource);
        var frozenTarget = new WorkflowNodeBase("target", "Script1", "Script1", "Script") { Parameters = ToolParameterSnapshot.Create(p) };
        frozenTarget.ParamSettings.SetSetting(ParamSetting.CreateBinding("input1", "source", "OutputImage"));
        var frozen = new SunEyeVision.Workflow.Workflow("f", "f"); frozen.Nodes.Add(source); frozen.Nodes.Add(frozenTarget);
        frozen.Connections.Add(new WorkflowConnection("source-target", source.Id, frozenTarget.Id));
        OutputBindingChangeService.EnsureExecutable(frozen);
        Assert.Equal(typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage), BindingRelationCatalog.Describe(frozenTarget).Single(i => i.Key == "input1").AllowedTypes.Single());
    }

    [Theory]
    [InlineData(typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage), "Image")]
    [InlineData(typeof(string), "String")]
    [InlineData(typeof(double), "Double")]
    [InlineData(typeof(bool), "Boolean")]
    public void Selecting_source_preserves_sdk_vocabulary_through_serialization_and_frozen_contract(Type type, string vocabulary)
    {
        var row = new ScriptInputConfig { Name = "input1", TypeName = vocabulary,
            BindingSource = "source.value", Source = new AvailableDataSource {
                SourceNodeId = "source", PropertyName = "value", PropertyType = type, VariableType = vocabulary } };
        Assert.Equal(vocabulary, row.TypeName); Assert.NotNull(row.ParamType);
        Assert.Contains("input1", row.GenerateFieldCode());
        var copy = JsonSerializer.Deserialize<ScriptInputConfig>(JsonSerializer.Serialize(row))!;
        Assert.Equal(vocabulary, copy.TypeName); Assert.Equal("source.value", copy.BindingSource);
        var p = new ScriptParameters(); p.Inputs.Clear(); p.Inputs.Add(copy);
        var frozen = ToolParameterSnapshot.Create(p);
        var input = BindingInputCatalog.Describe(frozen).Single(i => i.Name == "input1");
        var contract = new[] { new SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable {
            Name = "value", VariableType = vocabulary, DeclaredType = type, IsCollection = false, FixedCount = 1 } };
        Assert.Equal(BindingCompatibility.Compatible, BindingContractValidator.Validate(contract, "value", input.AllowedTypes).State);
        Assert.Equal("source.value", input.Read());
    }

    [Fact]
    public void Input_descriptor_key_survives_rename_reorder_and_serialization()
    {
        var p = new ScriptParameters(); p.Inputs.Clear();
        var input = new ScriptInputConfig { Name = "input", TypeName = "String", BindingSource = "source.value" };
        p.Inputs.Add(input); p.Inputs.Add(new() { Name = "second", TypeName = "Double" });
        var key = BindingInputCatalog.Describe(p).Single(i => i.Name == "input").Key;
        input.Name = "renamed"; p.Inputs.Move(0, 1);
        Assert.Equal(key, BindingInputCatalog.Describe(p).Single(i => i.Name == "renamed").Key);
        var copy = JsonSerializer.Deserialize<ScriptInputConfig>(JsonSerializer.Serialize(input))!;
        Assert.Equal(input.FieldId, copy.FieldId); Assert.Equal("source.value", copy.BindingSource);
    }

    [Fact]
    public void Output_contract_identity_and_collection_shape_do_not_depend_on_execution()
    {
        var p = new ScriptParameters(); p.Outputs.Clear();
        var row = new ScriptOutputConfig { Name = "value", TypeName = "Double" }; p.Outputs.Add(row);
        var first = ((IOutputContractProvider)p).GetOutputContract().Single(o => o.Name == "value");
        row.Name = "renamed"; row.IsAccumulated = true;
        var second = ((IOutputContractProvider)p).GetOutputContract().Single(o => o.Name == "renamed");
        Assert.Equal(first.FieldId, second.FieldId); Assert.False(first.IsCollection); Assert.True(second.IsCollection); Assert.Null(second.FixedCount);
        Assert.Null(second.RuntimeValue);
    }

    [Fact]
    public void Clearing_input_clears_both_runtime_source_model_and_persisted_expression()
    {
        var p = new ScriptParameters(); p.Inputs.Clear();
        var row = new ScriptInputConfig { Name = "input", TypeName = "String", Source = new AvailableDataSource
            { SourceNodeId = "source", PropertyName = "value", PropertyType = typeof(string), VariableType = "String" }, BindingSource = "source.value", BindingDisplayText = "source.value" };
        p.Inputs.Add(row); BindingInputCatalog.Describe(p).Single(i => i.Name == "input").Write("");
        Assert.Null(row.Source); Assert.Empty(row.BindingSource); Assert.Empty(row.BindingDisplayText);
    }
}
