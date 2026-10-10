using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Opc.Ua;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationTriggerOptimizationTests
{
    [Fact]
    public async Task Periodic_reads_never_overlap_and_stop_waits_for_inflight_cancellation()
    {
        var active = 0;
        var maximum = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new OpcUaPointPolling(_ => true, async (_, _, token) =>
        {
            var count = Interlocked.Increment(ref active);
            maximum = Math.Max(maximum, count);
            entered.TrySetResult();
            try { await Task.Delay(5000, token); }
            finally { Interlocked.Decrement(ref active); }
        }, (_, _, _) => { });
        await reader.StartAsync("dev", "node", 10, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await reader.StopAsync("dev", "node").WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, maximum);
        Assert.Equal(0, active);
        await reader.StopAllAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.StartAsync("dev", "node", 0, default));
    }
    [Theory]
    [InlineData("上升沿", false, true)]
    [InlineData("下降沿", true, false)]
    public void Boolean_edges_baseline_once_and_reset_after_invalid_quality(string rule, bool before, bool after)
    {
        var state = new CommunicationReceiveEventViewModel();
        state.Outputs.Add(new() { DataType = "Boolean", CompareRule = rule });
        OpcUaPointValueEventArgs Sample(bool value, bool valid = true) => new("dev", "node", value,
            BuiltInType.Boolean, false, valid ? StatusCodes.Good : StatusCodes.Bad, DateTimeOffset.Now, isSupportedScalar: true);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(before)).IsSuccess);
        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(after)).IsSuccess);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(after)).IsSuccess);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(before, false)).IsSuccess);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(after)).IsSuccess);
        CommunicationReceiveProcessor.ResetHistory(state);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(after)).IsSuccess);
    }

    [Fact]
    public void Ui_replay_does_not_establish_edge_history()
    {
        var state = new CommunicationReceiveEventViewModel();
        state.Outputs.Add(new() { DataType = "Boolean", CompareRule = "上升沿" });
        OpcUaPointValueEventArgs Sample(bool value) => new("dev", "node", value, BuiltInType.Boolean, false, StatusCodes.Good, DateTimeOffset.Now, isSupportedScalar: true);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(false), recordHistory: false).IsSuccess);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(true)).IsSuccess);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(false)).IsSuccess);
        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(state, Sample(true)).IsSuccess);
    }

    [Theory]
    [InlineData("等于", "START", true)]
    [InlineData("等于", "STOP", false)]
    [InlineData("包含", "TAR", true)]
    [InlineData("大于", "START", false)]
    public void Second_condition_is_independent_of_receive_success(string comparison, string expected, bool passes)
    {
        var id = Guid.NewGuid();
        var fields = new Dictionary<Guid, CommunicationFieldSample> { [id] = new(id, "command", "string", "START") };
        Assert.Equal(passes, CommunicationTriggerEvaluation.Matches(new() { ConditionFieldId = id, ConditionRule = comparison, ConditionValue = expected }, fields));
        Assert.True(CommunicationTriggerEvaluation.Matches(new(), fields));
        Assert.False(CommunicationTriggerEvaluation.Matches(new() { ConditionFieldId = Guid.NewGuid(), ConditionRule = "等于", ConditionValue = expected }, fields));
    }

    [Fact]
    public async Task Ordinary_outputs_feed_trigger_values_without_node_parameter_mapping()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var automation = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client" };
        var command = new CommunicationEventOutputDefinition { Name = "command", DataType = "string" };
        var number = new CommunicationEventOutputDefinition { Name = "number", DataType = "string" };
        var receive = new CommunicationEventDefinition { SelectedDeviceId = device.Id, Outputs = new() { command, number } };
        var rule = new CommunicationTriggerDefinition { EventId = receive.EventId, TargetId = "flow", ConditionFieldId = command.FieldId,
            ConditionRule = "等于", ConditionValue = "START", ComparisonOutputId = number.FieldId, TriggerParameterSource = $"{receive.EventId:N}.{number.FieldId:N}", ParameterMappings = new() { new() { SourceFieldId = number.FieldId,
                TargetNodeId = "node", TargetProperty = "Threshold" } } };
        var calls = new List<CommunicationTriggerRequest>();
        automation.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        automation.Reset(new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, CommunicationTriggers = new() { rule } });
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("STOP,2"), DateTimeOffset.Now);
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("START,3"), DateTimeOffset.Now);
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("START,9"), DateTimeOffset.Now);
        await automation.DrainAsync();
        Assert.Equal(3, calls.Count);
        Assert.Equal("2", calls[0].TriggerValue);
        Assert.Equal("3", calls[1].TriggerValue);
        Assert.Equal("9", calls[2].TriggerValue);
        Assert.All(calls, call => Assert.Empty(call.Assignments));
        rule.TriggerParameterSource = "4";
        automation.Refresh(new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, CommunicationTriggers = new() { rule } });
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("STOP,4"), DateTimeOffset.Now);
        await automation.DrainAsync();
        Assert.Equal("4", calls.Last().TriggerValue);
        rule.TriggerParameterSource = $"{receive.EventId:N}.{Guid.NewGuid():N}";
        automation.Refresh(new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, CommunicationTriggers = new() { rule } });
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("STOP,5"), DateTimeOffset.Now);
        await automation.DrainAsync();
        Assert.Equal(4, calls.Count); // A configured stale binding must never become an unconditional trigger.
        rule.TriggerParameterSource = "";
        automation.Refresh(new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, CommunicationTriggers = new() { rule } });
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("STOP,6"), DateTimeOffset.Now);
        await automation.DrainAsync();
        Assert.Null(calls.Last().TriggerValue);
        rule.TriggerParameterSource = null;
        rule.FieldId = number.FieldId;
        automation.Refresh(new Solution { Communications = new() { device }, CommunicationEvents = new() { receive }, CommunicationTriggers = new() { rule } });
        automation.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("STOP,7"), DateTimeOffset.Now);
        await automation.DrainAsync();
        Assert.Equal("7", calls.Last().TriggerValue);
    }

    [Fact]
    public void Configuration_roundtrip_preserves_monitor_settings_and_legacy_rule_semantics()
    {
        var point = new OpcUaDataPoint { MonitorMode = "周期读取", MonitorIntervalMs = 250 };
        var copy = JsonSerializer.Deserialize<OpcUaDataPoint>(JsonSerializer.Serialize(point))!;
        Assert.Equal(250, copy.MonitorIntervalMs);
        Assert.Equal("周期读取", point.Clone().MonitorMode);
        Assert.Equal(100, JsonSerializer.Deserialize<OpcUaDataPoint>("{}")!.MonitorIntervalMs);
        var legacy = JsonSerializer.Deserialize<CommunicationTriggerDefinition>("{\"FieldId\":\"" + Guid.NewGuid() + "\"}")!;
        Assert.Equal("无附加条件", legacy.ConditionRule);
        Assert.Empty(legacy.ParameterMappings);
        var trigger = new CommunicationTriggerDefinition { TriggerParameterSource = "START" };
        Assert.Equal("START", JsonSerializer.Deserialize<CommunicationTriggerDefinition>(JsonSerializer.Serialize(trigger))!.TriggerParameterSource);
    }
}
