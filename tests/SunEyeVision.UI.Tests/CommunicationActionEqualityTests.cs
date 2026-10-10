using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Opc.Ua;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationActionEqualityTests
{
    [Theory]
    [InlineData("Boolean", " true ", true)]
    [InlineData("Boolean", "FALSE", false)]
    [InlineData("Boolean", "1", false)]
    [InlineData("int16", "2", true)]
    [InlineData("uint16", "2", true)]
    [InlineData("int32", "02", true)]
    [InlineData("uint32", "2", true)]
    [InlineData("float32", "2.0", true)]
    [InlineData("double", "2e0", true)]
    [InlineData("int32", "2.0", false)]
    [InlineData("int16", "32768", false)]
    [InlineData("uint16", "-1", false)]
    [InlineData("int32", "2147483648", false)]
    [InlineData("double", "NaN", false)]
    [InlineData("double", "Infinity", false)]
    [InlineData("string", "START", true)]
    [InlineData("string", "start", false)]
    [InlineData("string", " START ", false)]
    public void Literals_are_compared_using_declared_type(string type, string literal, bool expected)
    {
        object value = type switch { "Boolean" => true, "string" => "START",
            _ => Convert.ChangeType(2, CommunicationTriggerEvaluation.ScalarType(type)!) };
        var id = Guid.NewGuid();
        var rule = new CommunicationTriggerDefinition { ParameterComparisonEnabled = true, TriggerParameterSource = literal };
        var result = CommunicationTriggerEvaluation.EvaluateEquality(rule, Guid.NewGuid(), new[] { id },
            new Dictionary<Guid, CommunicationFieldSample> { [id] = new(id, "out1", type, value) });
        Assert.Equal(expected, result.Matches);
    }

    [Fact]
    public void Empty_source_disables_comparison_and_legacy_switch_is_ignored()
    {
        var id = Guid.NewGuid();
        var fields = new Dictionary<Guid, CommunicationFieldSample> { [id] = new(id, "out1", "string", "START") };
        var rule = new CommunicationTriggerDefinition { ParameterComparisonEnabled = true, TriggerParameterSource = "" };
        Assert.True(CommunicationTriggerEvaluation.EvaluateEquality(rule, Guid.NewGuid(), new[] { id }, fields).Matches);
        rule.TriggerParameterSource = "OTHER";
        Assert.False(CommunicationTriggerEvaluation.EvaluateEquality(rule, Guid.NewGuid(), new[] { id }, fields).Matches);
        rule.ParameterComparisonEnabled = false;
        Assert.False(CommunicationTriggerEvaluation.EvaluateEquality(rule, Guid.NewGuid(), new[] { id }, fields).Matches);
        rule.TriggerParameterSource = "";
        Assert.True(CommunicationTriggerEvaluation.EvaluateEquality(rule, Guid.NewGuid(), Array.Empty<Guid>(), new Dictionary<Guid, CommunicationFieldSample>()).Matches);
        var legacy = JsonSerializer.Deserialize<CommunicationTriggerDefinition>("{}")!;
        Assert.False(legacy.ParameterComparisonEnabled);
        Assert.DoesNotContain("SourceDeviceId", JsonSerializer.Serialize(legacy));
        rule.ComparisonOutputId = id;
        var copy = JsonSerializer.Deserialize<CommunicationTriggerDefinition>(JsonSerializer.Serialize(rule))!;
        Assert.Equal(id, copy.ComparisonOutputId);
        Assert.Equal("Event", copy.Source);
    }

    [Fact]
    public void Bound_empty_value_still_compares_and_unbinding_then_clearing_disables()
    {
        var eventId = Guid.NewGuid(); var actual = Guid.NewGuid(); var source = Guid.NewGuid();
        var rule = new CommunicationTriggerDefinition { ComparisonOutputId = actual,
            TriggerParameterSource = $"{eventId:N}.{source:N}" };
        var fields = new Dictionary<Guid, CommunicationFieldSample> { [actual] = new(actual, "actual", "string", "START"),
            [source] = new(source, "source", "string", "") };
        Assert.False(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { actual, source }, fields).Matches);
        fields[actual] = new(actual, "actual", "string", "");
        Assert.True(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { actual, source }, fields).Matches);
        fields.Remove(source);
        Assert.NotNull(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { actual, source }, fields).Error);
        rule.TriggerParameterSource = "";
        Assert.True(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, Array.Empty<Guid>(), fields).Matches);
        rule.TriggerParameterSource = " ";
        Assert.False(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { actual }, fields).Matches);
    }

    [Fact]
    public void Multiple_outputs_require_selection_and_deleted_or_foreign_bindings_fail_closed()
    {
        var eventId = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var fields = new Dictionary<Guid, CommunicationFieldSample> {
            [first] = new(first, "out1", "int32", 2), [second] = new(second, "out2", "int32", 3) };
        var rule = new CommunicationTriggerDefinition { ParameterComparisonEnabled = true, TriggerParameterSource = "2" };
        Assert.NotNull(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { first, second }, fields).Error);
        rule.ComparisonOutputId = first;
        Assert.True(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { first, second }, fields).Matches);
        rule.TriggerParameterSource = $"{eventId:N}.{second:N}";
        Assert.False(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { first, second }, fields).Matches);
        fields[second] = new(second, "renamed", "int32", 2);
        Assert.True(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { first, second }, fields).Matches);
        fields.Remove(first);
        Assert.NotNull(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { second }, fields).Error);
        rule.ComparisonOutputId = second;
        rule.TriggerParameterSource = $"{Guid.NewGuid():N}.{second:N}";
        Assert.NotNull(CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { second }, fields).Error);
    }

    [Theory]
    [InlineData(2.0, true)]
    [InlineData(2.5, false)]
    [InlineData(2147483648.0, false)]
    [InlineData(double.NaN, false)]
    public void Numeric_bound_values_only_allow_lossless_conversion(double value, bool passes)
    {
        var eventId = Guid.NewGuid(); var actual = Guid.NewGuid(); var bound = Guid.NewGuid();
        var rule = new CommunicationTriggerDefinition { ParameterComparisonEnabled = true,
            ComparisonOutputId = actual, TriggerParameterSource = $"{eventId:N}.{bound:N}" };
        var fields = new Dictionary<Guid, CommunicationFieldSample> { [actual] = new(actual, "actual", "int32", 2),
            [bound] = new(bound, "bound", "double", value) };
        Assert.Equal(passes, CommunicationTriggerEvaluation.EvaluateEquality(rule, eventId, new[] { actual, bound }, fields).Matches);
    }

    [Fact]
    public async Task OpcUa_rising_event_and_action_condition_are_independent_and_snapshot_local()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var automation = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "opcua" };
        var point = new OpcUaDataPoint { TagName = "node" }; device.OpcUaDataPoints.Add(point);
        var output = new CommunicationEventOutputDefinition { Name = "out1", DataType = "Boolean", CompareRule = "上升沿" };
        var receive = new CommunicationEventDefinition { SelectedDeviceId = device.Id, SelectedAddress = point.Id, Outputs = new() { output } };
        var rule = new CommunicationTriggerDefinition { EventId = receive.EventId, TargetId = "flow",
            ParameterComparisonEnabled = true, TriggerParameterSource = "false" };
        var solution = new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, CommunicationTriggers = new() { rule } };
        var calls = new List<CommunicationTriggerRequest>();
        automation.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        automation.Reset(solution);
        void Sample(bool value) => automation.ProcessOpcUa(new(device.Id, point.NodeId, value, BuiltInType.Boolean,
            false, StatusCodes.Good, DateTimeOffset.Now, isSupportedScalar: true));
        Sample(false); Sample(true);
        Assert.Empty(calls);
        rule.TriggerParameterSource = "true"; automation.Refresh(solution);
        Sample(false); Sample(true);
        Assert.True(Assert.IsType<bool>(Assert.Single(calls).TriggerValue));
        Assert.Empty(calls[0].Assignments);
        Sample(true);
        Assert.Single(calls);
        await automation.DrainAsync();
    }

    [Fact]
    public async Task Protocol_compare_assembly_outputs_use_the_same_action_equality()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var automation = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client" };
        var input = new CommunicationCompareInputDefinition { Name = "in1", DataType = "string" };
        var output = new CommunicationAssemblyOutputDefinition { Name = "out1", DataType = "string", SourceFieldId = input.FieldId };
        var receive = new CommunicationEventDefinition { SelectedDeviceId = device.Id, EventType = "ProtocolCompare",
            CompareInputs = new() { input }, AssemblyOutputs = new() { output } };
        var rule = new CommunicationTriggerDefinition { EventId = receive.EventId, TargetId = "flow",
            ParameterComparisonEnabled = true, TriggerParameterSource = "START" };
        var calls = new List<CommunicationTriggerRequest>();
        automation.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        automation.Reset(new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, CommunicationTriggers = new() { rule } });
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("STOP"), DateTimeOffset.Now);
        Assert.Empty(calls);
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("START"), DateTimeOffset.Now);
        Assert.Equal("START", Assert.Single(calls).TriggerValue);
        await automation.DrainAsync();
    }

    [Fact]
    public async Task Old_global_rules_are_not_loaded_or_executed()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var automation = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client", EnableDataUpload = true };
        var old = new CommunicationTriggerDefinition { Source = "String", Text = "GO" };
        var solution = new Solution { Communications = new() { device }, CommunicationTriggers = new() { old } };
        var calls = new List<CommunicationTriggerRequest>();
        automation.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        automation.Reset(solution);
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("GO"), DateTimeOffset.Now);
        Assert.Empty(calls);
        solution.CommunicationTriggers.Add(new() { Source = "GlobalData", MatchMode = "不等于", Text = "STOP" });
        automation.Refresh(solution);
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("GO"), DateTimeOffset.Now);
        Assert.Single(calls);
        await automation.DrainAsync();
    }
}
