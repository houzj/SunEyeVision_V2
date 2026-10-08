using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationSendEventTests
{
    private static CommunicationSendParameterDefinition Field(string name, string type, string content) =>
        new() { Name = name, DataType = type, Content = content };

    [Fact]
    public void Direct_output_joins_ordered_values_and_appends_terminator_once()
    {
        var definition = new CommunicationSendEventDefinition { Separator = ";", Terminator = "\r\n" };
        definition.Parameters.AddRange(new[] { Field("count", "int", "7"), Field("score", "float", "1.25"), Field("label", "string", "OK") });
        var message = CommunicationSendEventFormatter.Build(definition);
        Assert.Equal("7;1.25;OK\r\n", message.Text);
        Assert.Equal(Encoding.UTF8.GetBytes(message.Text), message.Utf8Bytes);
        Assert.IsType<int>(message.Inputs[0].Value);
        Assert.IsType<float>(message.Inputs[1].Value);
    }

    [Fact]
    public void Typed_snapshot_rejects_missing_or_wrong_type_instead_of_using_test_literal()
    {
        var field = Field("count", "int", "7");
        var definition = new CommunicationSendEventDefinition { Parameters = new() { field } };
        Assert.Throws<FormatException>(() => CommunicationSendEventFormatter.Build(definition, new Dictionary<Guid, object?>()));
        Assert.Throws<FormatException>(() => CommunicationSendEventFormatter.Build(definition, new Dictionary<Guid, object?> { [field.FieldId] = "7" }));
        Assert.Equal("9", CommunicationSendEventFormatter.Build(definition, new Dictionary<Guid, object?> { [field.FieldId] = 9 }).Text);
    }

    [Fact]
    public void Assembly_outputs_can_reorder_repeat_and_mix_constants_with_inputs()
    {
        var first = Field("in1", "string", "A"); var second = Field("in2", "int", "3");
        var definition = new CommunicationSendEventDefinition { Mode = "Assembly", Separator = "|", Parameters = new() { first, second } };
        definition.AssemblyOutputs.AddRange(new[]
        {
            new CommunicationAssemblyOutputDefinition { Name = "prefix", DataType = "string", Content = "RESULT" },
            new CommunicationAssemblyOutputDefinition { Name = "out1", DataType = "int", IsContentBound = true, SourceFieldId = second.FieldId },
            new CommunicationAssemblyOutputDefinition { Name = "out2", DataType = "string", IsContentBound = true, SourceFieldId = first.FieldId }
        });
        first.Name = "renamed";
        Assert.Equal("RESULT|3|A", CommunicationSendEventFormatter.Build(definition).Text);
    }

    [Fact]
    public void Deleted_input_is_not_rebound_to_new_same_name_after_save_reload()
    {
        var definition = new CommunicationSendEventDefinition { Mode = "Assembly" };
        var input = Field("in1", "string", "old"); definition.Parameters.Add(input);
        definition.AssemblyOutputs.Add(new CommunicationAssemblyOutputDefinition
        {
            Name = "out1", DataType = "string", IsContentBound = true, SourceFieldId = input.FieldId,
            BindingSource = "SendEventInputs.in1", BindingDisplayText = "参数列表.in1"
        });
        var model = CommunicationSendEventViewModel.FromDefinition(definition);
        model.Parameters.RemoveAt(0);
        model.Parameters.Add(new CommunicationSendParameterViewModel { Name = "in1", DataType = "string", Content = "new" });
        var reloaded = CommunicationSendEventViewModel.FromDefinition(JsonSerializer.Deserialize<CommunicationSendEventDefinition>(JsonSerializer.Serialize(model.Capture()))!);
        Assert.Equal(input.FieldId, reloaded.AssemblyOutputs[0].SourceFieldId);
        Assert.Throws<FormatException>(() => CommunicationSendEventFormatter.Build(reloaded.Capture()));
    }

    [Fact]
    public void Parameter_rename_preserves_bound_field_id_and_updates_display_path()
    {
        var model = new CommunicationSendEventViewModel { Mode = "Assembly" };
        var input = new CommunicationSendParameterViewModel { Name = "in1", DataType = "string", Content = "A" };
        model.Parameters.Add(input);
        var output = new CommunicationAssemblyOutputViewModel { Name = "out1", DataType = "string", AvailableDataSources = model.InputSources };
        model.AssemblyOutputs.Add(output);
        output.ApplyBindingSelection("SendEventInputs.in1", "参数列表.in1");
        input.Name = "label";
        Assert.Equal(input.FieldId, output.SourceFieldId);
        Assert.Equal("SendEventInputs.label", output.BindingSource);
        Assert.Equal("A", CommunicationSendEventFormatter.Build(model.Capture()).Text);
    }

    [Fact]
    public void Send_configuration_roundtrip_excludes_live_result_and_history()
    {
        var model = new CommunicationSendEventViewModel { Name = "result", DeviceId = "device", TargetId = "target", Separator = ";", Terminator = "\n" };
        model.Parameters.Add(new CommunicationSendParameterViewModel { Name = "flag", DataType = "Boolean", Content = "True", Result = "runtime-only" });
        model.AddHistory("history-only", new byte[] { 1 }, true);
        var solution = new Solution { CommunicationSendEvents = new() { model.Capture() } };
        var json = JsonSerializer.Serialize(solution, WorkflowSerializationOptions.Default);
        Assert.DoesNotContain("runtime-only", json); Assert.DoesNotContain("history-only", json);
        var restored = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!.CommunicationSendEvents.Single();
        Assert.Equal(model.EventId, restored.EventId);
        Assert.Equal(model.Parameters[0].FieldId, restored.Parameters[0].FieldId);
        Assert.IsType<bool>(CommunicationSendEventFormatter.Build(restored).Outputs[0].Value);
    }

    [Fact]
    public void Modbus_prepares_register_values_without_truncation_or_implicit_type_changes()
    {
        var device = new Communication { ProtocolKey = "modbus" };
        var point = new ModbusAddressPoint { StartAddress = 12, Count = 2, IntBits = "16bit" };
        device.ModbusAddressPoints.Add(point);
        var definition = new CommunicationSendEventDefinition { DeviceId = device.Id, TargetId = point.Id,
            Parameters = new() { Field("a", "int", "-1"), Field("b", "int", "2") } };
        var service = new CommunicationSendEventService(new CommunicationRuntimeManager());
        var plan = service.Prepare(definition, device);
        Assert.Equal((ushort)12, plan.StartAddress);
        Assert.Equal(new ushort[] { 0xFFFF, 2 }, plan.Registers);
        point.Count = 1;
        Assert.Throws<FormatException>(() => service.Prepare(definition, device));
        point.Count = 2; point.AccessMode = "只读";
        Assert.Throws<InvalidOperationException>(() => service.Prepare(definition, device));
    }

    [Fact]
    public async Task Tcp_send_event_delivers_ordered_utf8_payload_to_loopback_peer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var device = new Communication { ProtocolKey = "tcp-client", Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        await using var runtime = new CommunicationRuntimeManager();
        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        var definition = new CommunicationSendEventDefinition { DeviceId = device.Id, Separator = ",", Terminator = "\r\n",
            Parameters = new() { Field("text", "string", "你好"), Field("number", "int", "4") } };
        var service = new CommunicationSendEventService(runtime);
        var plan = service.Prepare(definition, device);
        await service.SendAsync(plan, timeout.Token);
        var received = new byte[plan.PayloadBytes.Length];
        await peer.GetStream().ReadExactlyAsync(received, timeout.Token);
        Assert.Equal("你好,4\r\n", Encoding.UTF8.GetString(received));
    }

    [Fact]
    public void Assembly_can_output_constants_without_input_parameters()
    {
        var definition = new CommunicationSendEventDefinition { Mode = "Assembly",
            AssemblyOutputs = new() { new CommunicationAssemblyOutputDefinition { Name = "out1", DataType = "string", Content = "READY" } } };
        Assert.Equal("READY", CommunicationSendEventFormatter.Build(definition).Text);
    }

    [Fact]
    public async Task Modbus_send_event_writes_bound_registers_with_fc16()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var device = new Communication { ProtocolKey = "modbus", ModbusMode = "TcpClient", UnitId = 7,
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        var target = new ModbusAddressPoint { StartAddress = 40, Count = 2, IntBits = "16bit" };
        device.ModbusAddressPoints.Add(target);
        await using var runtime = new CommunicationRuntimeManager();
        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        var definition = new CommunicationSendEventDefinition { DeviceId = device.Id, TargetId = target.Id,
            Parameters = new() { Field("a", "int", "10"), Field("b", "int", "20") } };
        var service = new CommunicationSendEventService(runtime);
        var sending = service.SendAsync(service.Prepare(definition, device), timeout.Token);
        var stream = peer.GetStream();
        var header = new byte[7]; await stream.ReadExactlyAsync(header, timeout.Token);
        var request = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1];
        await stream.ReadExactlyAsync(request, timeout.Token);
        Assert.Equal(7, header[6]); Assert.Equal(16, request[0]);
        Assert.Equal(40, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(1, 2)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(3, 2)));
        Assert.Equal(new byte[] { 0, 10, 0, 20 }, request[6..]);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 6);
        await stream.WriteAsync(header.Concat(request.Take(5)).ToArray(), timeout.Token);
        Assert.Equal(1, await sending);
    }
}
