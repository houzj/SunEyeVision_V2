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

public sealed class CommunicationGlobalUploadTests
{
    [Theory]
    [InlineData("Boolean", " true ", true)]
    [InlineData("Boolean", "false", false)]
    [InlineData("UInt16", "1", true)]
    [InlineData("Int32", "1.0", false)]
    [InlineData("UInt32", "-1", false)]
    [InlineData("Double", "1.0", true)]
    [InlineData("String", "1.0", false)]
    [InlineData("String", "1", true)]
    [InlineData("Int64", "1", true)]
    [InlineData("UInt64", "1", true)]
    public void Global_equal_uses_native_type_not_string_conversion(string type, string parameter, bool matches)
    {
        object value = type switch { "Boolean" => true, "String" => "1", _ => Convert.ChangeType(1, CommunicationTriggerEvaluation.ScalarType(type)!) };
        var sample = new CommunicationUploadSnapshot(Guid.NewGuid(), "dev", "point", DateTimeOffset.Now, type, value);
        var rule = new CommunicationTriggerDefinition { Source = "GlobalData", MatchMode = "等于", Text = parameter };
        Assert.Equal(matches, CommunicationTriggerEvaluation.EvaluateUpload(rule, sample).Matches);
    }

    [Fact]
    public void Old_format_collections_quality_and_empty_parameter_fail_closed()
    {
        var rule = new CommunicationTriggerDefinition { Source = "String", Text = "true", MatchMode = "等于" };
        var sample = new CommunicationUploadSnapshot(Guid.NewGuid(), "dev", "point", DateTimeOffset.Now, "Boolean", true);
        Assert.False(CommunicationTriggerEvaluation.EvaluateUpload(rule, sample).Matches);
        rule.Source = "GlobalData";
        Assert.True(CommunicationTriggerEvaluation.EvaluateUpload(rule, sample).Matches);
        rule.Text = ""; Assert.NotNull(CommunicationTriggerEvaluation.EvaluateUpload(rule, sample).Error);
        rule.MatchMode = "包含"; rule.Text = "t";
        Assert.NotNull(CommunicationTriggerEvaluation.EvaluateUpload(rule, sample).Error);
        rule.MatchMode = "不检查内容";
        Assert.True(CommunicationTriggerEvaluation.EvaluateUpload(rule, sample).Matches);
        Assert.False(CommunicationTriggerEvaluation.EvaluateUpload(rule,
            new(sample.SessionId, "dev", "point", sample.Timestamp, "Boolean", true, false)).Matches);
        Assert.False(CommunicationTriggerEvaluation.EvaluateUpload(rule,
            new(sample.SessionId, "dev", "point", sample.Timestamp, "UInt16[]", new ushort[] { 1, 2 })).Matches);
        Assert.False(CommunicationTriggerEvaluation.EvaluateUpload(rule,
            new(sample.SessionId, "dev", "point", sample.Timestamp, "Double", double.NaN)).Matches);
        var copy = JsonSerializer.Deserialize<CommunicationTriggerDefinition>(JsonSerializer.Serialize(rule))!;
        Assert.Equal("GlobalData", copy.Source);
        Assert.DoesNotContain("UploadScope", JsonSerializer.Serialize(copy));
        Assert.DoesNotContain("SourceDeviceId", JsonSerializer.Serialize(copy));
    }

    [Fact]
    public void Upload_snapshot_owns_buffers_and_preserves_provenance()
    {
        var words = new ushort[] { 1, 2 }; var raw = new byte[] { 1, 2 };
        var session = Guid.NewGuid(); var time = DateTimeOffset.Now;
        var snapshot = new CommunicationUploadSnapshot(session, "dev", "address", time, "UInt16[]", words, rawData: raw);
        words[0] = 9; raw[0] = 9;
        Assert.Equal((ushort)1, Assert.IsAssignableFrom<IReadOnlyList<ushort>>(snapshot.Value)[0]);
        Assert.Equal((byte)1, snapshot.RawData![0]);
        Assert.Equal(session, snapshot.SessionId); Assert.Equal(time, snapshot.Timestamp); Assert.Equal("address", snapshot.TargetId);
    }

    [Fact]
    public async Task Upload_observer_receives_owned_raw_data_and_transition_blocks_old_action()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client", EnableDataUpload = true };
        var rule = new CommunicationTriggerDefinition { Source = "GlobalData", MatchMode = "不检查内容" };
        var solution = new Solution { Communications = new() { device }, CommunicationTriggers = new() { rule } };
        var calls = new List<CommunicationTriggerRequest>();
        var uploads = new List<CommunicationUploadSnapshot>();
        service.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        service.Reset(solution);
        Action<CommunicationUploadSnapshot> observer = sample => { uploads.Add(sample); service.Reset(solution); };
        service.DataUploaded += observer;
        var bytes = Encoding.UTF8.GetBytes("GO");
        service.ProcessRaw(device.Id, bytes, DateTimeOffset.Now);
        bytes[0] = 0;
        Assert.Empty(calls);
        Assert.Equal((byte)'G', Assert.Single(uploads).RawData![0]);
        service.DataUploaded -= observer;
        service.ProcessRaw(device.Id, new byte[] { 0xff }, DateTimeOffset.Now);
        Assert.Empty(calls);
        service.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("GO"), DateTimeOffset.Now);
        Assert.Single(calls);
        await service.DrainAsync();
    }

    [Fact]
    public async Task OpcUa_native_upload_repeats_and_upload_switch_does_not_disable_receive_events()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "opcua", EnableDataUpload = true };
        var point = new OpcUaDataPoint { TagName = "bit" }; device.OpcUaDataPoints.Add(point);
        var receive = new CommunicationEventDefinition { SelectedDeviceId = device.Id, SelectedAddress = point.Id,
            Outputs = new() { new() { Name = "out1", DataType = "Boolean" } } };
        var global = new CommunicationTriggerDefinition { Source = "GlobalData", MatchMode = "等于", Text = "true", TargetId = "flow" };
        var eventRule = new CommunicationTriggerDefinition { EventId = receive.EventId, TargetId = "flow" };
        var solution = new Solution { Communications = new() { device }, CommunicationEvents = new() { receive },
            CommunicationTriggers = new() { global, eventRule } };
        var calls = new List<CommunicationTriggerRequest>();
        service.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        service.Reset(solution);
        void Sample(bool value, StatusCode status) => service.ProcessOpcUa(new(device.Id, point.NodeId, value,
            BuiltInType.Boolean, false, status, DateTimeOffset.Now));
        Sample(true, StatusCodes.Good); Sample(true, StatusCodes.Good);
        Assert.Equal(2, calls.FindAll(r => r.RuleId == global.Id).Count);
        Assert.IsType<bool>(calls.Find(r => r.RuleId == global.Id)!.TriggerValue);
        var oldUpload = calls.Find(r => r.RuleId == global.Id)!.Upload!;
        Assert.Equal(point.Id, oldUpload.TargetId);
        device.EnableDataUpload = false; service.Refresh(solution);
        Sample(true, StatusCodes.Good);
        Assert.Equal(2, calls.FindAll(r => r.RuleId == global.Id).Count);
        Assert.Equal(3, calls.FindAll(r => r.RuleId == eventRule.Id).Count);
        device.EnableDataUpload = true; service.Refresh(solution);
        Sample(true, StatusCodes.Bad);
        Assert.Equal(5, calls.Count);
        service.Reset(solution);
        service.ProcessUploadedData(oldUpload);
        Assert.Equal(5, calls.Count);
        await service.DrainAsync();
    }

    [Theory]
    [InlineData("ABCD", 0, 1)]
    [InlineData("CDAB", 1, 0)]
    [InlineData("BADC", 0, 256)]
    [InlineData("DCBA", 256, 0)]
    public async Task Modbus_explicit_32bit_value_obeys_byte_order(string order, int first, int second)
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "modbus", EnableDataUpload = true };
        var point = new ModbusAddressPoint { IntBits = "32bit", Count = 2, ByteOrder = order }; device.ModbusAddressPoints.Add(point);
        var rule = new CommunicationTriggerDefinition { Source = "GlobalData", MatchMode = "等于", Text = "1" };
        var calls = new List<CommunicationTriggerRequest>();
        service.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        service.Reset(new Solution { Communications = new() { device }, CommunicationTriggers = new() { rule } });
        service.ProcessModbus(new(device.Id, point.Id, 0, 2, new ushort[] { (ushort)first, (ushort)second }, DateTimeOffset.Now));
        Assert.Equal(1, Assert.IsType<int>(Assert.Single(calls).TriggerValue));
        await service.DrainAsync();
    }

    [Fact]
    public async Task Modbus_word_is_native_unsigned_and_multiple_values_are_not_first_item()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "modbus", EnableDataUpload = true };
        var word = new ModbusAddressPoint { Count = 1 };
        var block = new ModbusAddressPoint { Count = 3, StartAddress = 10 };
        device.ModbusAddressPoints.Add(word); device.ModbusAddressPoints.Add(block);
        var rule = new CommunicationTriggerDefinition { Source = "GlobalData", MatchMode = "等于", Text = "65535" };
        var calls = new List<CommunicationTriggerRequest>();
        service.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        var solution = new Solution { Communications = new() { device }, CommunicationTriggers = new() { rule } };
        service.Reset(solution);
        service.ProcessModbus(new(device.Id, word.Id, 0, 1, new ushort[] { 65535 }, DateTimeOffset.Now));
        Assert.Equal(ushort.MaxValue, Assert.IsType<ushort>(Assert.Single(calls).TriggerValue));
        rule.MatchMode = "不检查内容"; service.Refresh(solution);
        service.ProcessModbus(new(device.Id, block.Id, 10, 3, new ushort[] { 65535, 1, 2 }, DateTimeOffset.Now));
        Assert.Single(calls);
        await service.DrainAsync();
    }

    [Fact]
    public async Task Mixed_devices_match_new_global_rules_but_old_format_never_executes()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var raw = new Communication { ProtocolKey = "tcp-client", EnableDataUpload = true };
        var opc = new Communication { ProtocolKey = "opcua", EnableDataUpload = true };
        var point = new OpcUaDataPoint { TagName = "number" }; opc.OpcUaDataPoints.Add(point);
        var oldRule = JsonSerializer.Deserialize<CommunicationTriggerDefinition>(
            """{"Source":"String","Text":"1","UploadScope":"AllScalars","SourceDeviceId":"*","MatchMode":"等于"}""")!;
        var rule = new CommunicationTriggerDefinition { Source = "GlobalData", Text = "1" };
        var solution = new Solution { Communications = new() { raw, opc }, CommunicationTriggers = new() { oldRule, rule } };
        var calls = new List<CommunicationTriggerRequest>();
        service.Dispatcher = (request, _) => { calls.Add(request); return Task.FromResult(true); };
        service.Reset(solution);
        service.ProcessRaw(raw.Id, Encoding.UTF8.GetBytes("1"), DateTimeOffset.Now);
        service.ProcessOpcUa(new(opc.Id, point.NodeId, 1, BuiltInType.Int32, false, StatusCodes.Good, DateTimeOffset.Now));
        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal(rule.Id, call.RuleId));
        Assert.IsType<string>(calls[0].TriggerValue); Assert.IsType<int>(calls[1].TriggerValue);
        await service.DrainAsync();
    }

    [Theory]
    [InlineData("Boolean", "false", true)]
    [InlineData("Boolean", "true", false)]
    [InlineData("Boolean", "garbage", false)]
    [InlineData("Int32", "2", true)]
    [InlineData("Int32", "1", false)]
    [InlineData("Int32", "1.0", false)]
    [InlineData("UInt16", "-1", false)]
    [InlineData("Double", "2.0", true)]
    [InlineData("Double", "1.0", false)]
    [InlineData("Double", "NaN", false)]
    [InlineData("String", "other", true)]
    [InlineData("String", "1", false)]
    [InlineData("String", "", false)]
    public void Not_equal_requires_valid_native_parameter(string type, string parameter, bool matches)
    {
        object value = type switch { "Boolean" => true, "String" => "1", _ => Convert.ChangeType(1, CommunicationTriggerEvaluation.ScalarType(type)!) };
        var rule = new CommunicationTriggerDefinition { Source = "GlobalData", MatchMode = "不等于", Text = parameter };
        var sample = new CommunicationUploadSnapshot(Guid.NewGuid(), "dev", "point", DateTimeOffset.Now, type, value);
        Assert.Equal(matches, CommunicationTriggerEvaluation.EvaluateUpload(rule, sample).Matches);
        Assert.False(CommunicationTriggerEvaluation.EvaluateUpload(rule,
            new(sample.SessionId, "dev", "point", sample.Timestamp, type, value, false)).Matches);
    }

    [Theory]
    [InlineData("完全匹配")]
    [InlineData("部分匹配")]
    [InlineData("不匹配")]
    [InlineData("unknown")]
    public void Removed_modes_do_not_execute(string mode)
    {
        var rule = new CommunicationTriggerDefinition { Source = "GlobalData", MatchMode = mode, Text = "GO" };
        Assert.False(CommunicationTriggerEvaluation.EvaluateUpload(rule,
            new(Guid.NewGuid(), "dev", null, DateTimeOffset.Now, "String", "GO")).Matches);
    }
}
