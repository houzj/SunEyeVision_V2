using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class CommunicationToolResultsTests
{
    [Fact]
    public void Stable_field_contract_resolves_label_changes_and_binary_values()
    {
        var id = Guid.NewGuid();
        var field = new CommunicationOutputField(id, "重命名后", "Binary");
        var result = new CommunicationToolResults(new[] { field });
        var data = new BinaryPayload(new byte[] { 0, 92 });
        result.PublishOutputs(new Dictionary<Guid, object?> { [id] = data });
        result.SetSuccess();
        Assert.Same(data, OutputVariableResolver.Resolve(result, field.BindingKey));
        Assert.Equal("重命名后", result.GetOutputVariables().Single(v => v.Name == field.BindingKey).DisplayName);
        Assert.Contains(result.GetOutputVariables(), v => v.Name == "ExecutionTimeMs");
    }

    [Fact]
    public void Any_invalid_field_clears_prior_outputs_atomically_but_keeps_analysis()
    {
        var a = new CommunicationOutputField(Guid.NewGuid(), "out0", "Integer");
        var b = new CommunicationOutputField(Guid.NewGuid(), "out1", "String");
        var result = new CommunicationToolResults(new[] { a, b });
        result.PublishOutputs(new Dictionary<Guid, object?> { [a.FieldId] = 92L, [b.FieldId] = "AD" });
        result.SetAnalysis(new[] { new CommunicationAnalysisRow(b.FieldId, "out1", "input", "string", "类型错误", false) });
        Assert.Throws<ArgumentException>(() => result.PublishOutputs(new Dictionary<Guid, object?>
        { [a.FieldId] = 1L, [b.FieldId] = 2L }));
        result.SetError("解析失败");
        Assert.Null(OutputVariableResolver.Resolve(result, a.BindingKey));
        Assert.Null(OutputVariableResolver.Resolve(result, b.BindingKey));
        Assert.Single(result.Analysis);
        Assert.DoesNotContain(result.GetOutputVariables(), v => v.Name == nameof(result.Analysis));
    }

    [Fact]
    public void Result_snapshot_is_not_mutated_by_input_or_binding_consumer_arrays()
    {
        var field = new CommunicationOutputField(Guid.NewGuid(), "out0", "Integer");
        var values = new long[] { 1, 2 };
        var result = new CommunicationToolResults(new[] { field });
        result.PublishOutputs(new Dictionary<Guid, object?> { [field.FieldId] = values });
        result.SetSuccess();
        values[0] = 9;
        var consumer = Assert.IsType<long[]>(OutputVariableResolver.Resolve(result, field.BindingKey));
        consumer[1] = 8;
        Assert.Equal(new long[] { 1, 2 }, Assert.IsType<long[]>(OutputVariableResolver.Resolve(result, field.BindingKey)));
    }

    [Fact]
    public void Failed_status_never_exposes_previously_published_business_values()
    {
        var field = new CommunicationOutputField(Guid.NewGuid(), "out0", "String");
        var result = new CommunicationToolResults(new[] { field });
        result.PublishOutputs(new Dictionary<Guid, object?> { [field.FieldId] = "previous" });
        Assert.Null(OutputVariableResolver.Resolve(result, field.BindingKey));
        result.SetSuccess();
        Assert.Equal("previous", OutputVariableResolver.Resolve(result, field.BindingKey));
        result.SetError("本次失败");
        Assert.Null(OutputVariableResolver.Resolve(result, field.BindingKey));
    }

    [Fact]
    public void Unknown_or_duplicate_definitions_do_not_produce_fake_outputs()
    {
        var field = new CommunicationOutputField(Guid.NewGuid(), "out0", "Integer");
        Assert.Throws<ArgumentException>(() => new CommunicationToolResults(new[] { field, field }));
        Assert.Throws<NotSupportedException>(() => new CommunicationToolResults(new[] { field with { VariableType = "Byte" } }));
    }
}
