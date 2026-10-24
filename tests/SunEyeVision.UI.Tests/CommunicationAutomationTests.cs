using System.Buffers.Binary;
using System.IO;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationAutomationTests
{
    [Fact]
    public async Task Event_trigger_uses_only_successful_real_samples_and_needs_no_assembly_field()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client", EnableDataUpload = false };
        var receive = new CommunicationEventDefinition { SelectedDeviceId = device.Id,
            Outputs = new() { new() { Name = "in1", DataType = "string", CompareRule = "等于", CompareValue = "GO" } } };
        service.Reset(new Solution { Communications = new() { device }, CommunicationEvents = new() { receive },
            CommunicationTriggers = new() { new() { EventId = receive.EventId, TargetId = "flow" } } });
        var calls = new ConcurrentQueue<CommunicationTriggerRequest>();
        service.Dispatcher = (request, _) => { calls.Enqueue(request); return Task.FromResult(true); };
        service.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("BAD"), DateTimeOffset.Now);
        service.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("GO"), DateTimeOffset.Now);
        service.ProcessRaw(device.Id, Encoding.UTF8.GetBytes("GO"), DateTimeOffset.Now);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Null(call.TriggerValue));
    }

    [Theory]
    [InlineData("等于", "GO", true)]
    [InlineData("等于", "xxGOxx", false)]
    [InlineData("包含", "xxGOxx", true)]
    [InlineData("不检查内容", "anything", true)]
    public async Task String_matching_follows_vm_semantics_and_requires_data_upload(string mode, string input, bool expected)
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client", EnableDataUpload = true };
        var solution = new Solution { Communications = new() { device }, CommunicationTriggers = new()
            { new() { Source = "GlobalData", MatchMode = mode, Text = "GO", TargetId = "flow" } } };
        var count = 0;
        service.Dispatcher = (_, _) => { Interlocked.Increment(ref count); return Task.FromResult(true); };
        service.Reset(solution); service.ProcessRaw(device.Id, Encoding.UTF8.GetBytes(input), DateTimeOffset.Now);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(expected ? 1 : 0, count);
        device.EnableDataUpload = false; service.Refresh(solution);
        service.ProcessRaw(device.Id, Encoding.UTF8.GetBytes(input), DateTimeOffset.Now);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(expected ? 1 : 0, count);
    }

    [Fact]
    public async Task Modbus_edge_state_survives_configuration_refresh_and_repeated_samples_do_not_retrigger()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "modbus" };
        var point = new ModbusAddressPoint { Count = 1 }; device.ModbusAddressPoints.Add(point);
        var receive = new CommunicationEventDefinition { SelectedDeviceId = device.Id, SelectedAddress = point.Id,
            ProcessingMethod = "ByteMatch", EventType = "ProtocolCompare", CompareInputs = new()
                { new() { Name = "edge", CompareRule = "上升沿" } } };
        var solution = new Solution { Communications = new() { device }, CommunicationEvents = new() { receive },
            CommunicationTriggers = new() { new() { EventId = receive.EventId, TargetId = "flow" } } };
        var count = 0; service.Dispatcher = (_, _) => { Interlocked.Increment(ref count); return Task.FromResult(true); };
        service.Reset(solution);
        service.ProcessModbus(new(device.Id, point.Id, 0, 1, new ushort[] { 0 }, DateTimeOffset.Now));
        service.ProcessModbus(new(device.Id, point.Id, 0, 1, new ushort[] { 1 }, DateTimeOffset.Now));
        service.Refresh(solution);
        service.ProcessModbus(new(device.Id, point.Id, 0, 1, new ushort[] { 1 }, DateTimeOffset.Now));
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Busy_trigger_is_skipped_warned_and_never_queued()
    {
        var manager = new WorkflowExecutionManager(new EmptyInput(), new WorkflowEngine(VisionLogger.Instance), new RuntimeInputSnapshotService()) { AllowLegacyEditorCommitForTests = true };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = manager.RunCommunicationOperationAsync(async _ => { entered.TrySetResult(); await release.Task; }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client", EnableDataUpload = true };
        service.Reset(new Solution { Communications = new() { device }, CommunicationTriggers = new()
            { new() { Source = "GlobalData", MatchMode = "不检查内容", TargetId = "flow" } } });
        var calls = 0;
        var diagnostics = new ConcurrentQueue<(LogLevel, string)>();
        service.Diagnostic += (level, text) => diagnostics.Enqueue((level, text));
        service.Dispatcher = (_, token) => manager.RunCommunicationOperationAsync(_ => { Interlocked.Increment(ref calls); return Task.CompletedTask; }, token);
        try
        {
            service.ProcessRaw(device.Id, new byte[] { 65 }, DateTimeOffset.Now);
            await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, calls);
            Assert.Contains(diagnostics, entry => entry.Item1 == LogLevel.Warning && entry.Item2.Contains("忙碌") && entry.Item2.Contains("不排队"));
            Assert.False(manager.StartContinuousRun(new WorkflowTabViewModel()));
        }
        finally { release.TrySetResult(); }
        await active.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(50);
        Assert.Equal(0, calls);
        Assert.False(manager.IsAnyRunning());
        service.ProcessRaw(device.Id, new byte[] { 65 }, DateTimeOffset.Now);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, calls); manager.Dispose();
    }

    [Fact]
    public async Task Manual_run_lease_also_rejects_communication_operations()
    {
        var input = new BlockingInput();
        var manager = new WorkflowExecutionManager(input, new WorkflowEngine(VisionLogger.Instance), new RuntimeInputSnapshotService()) { AllowLegacyEditorCommitForTests = true };
        var running = manager.RunSingleAsync(new WorkflowTabViewModel());
        try
        {
            await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(await manager.RunCommunicationOperationAsync(_ => Task.CompletedTask, default));
        }
        finally { input.Release.TrySetResult(null); }
        await running.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(manager.IsAnyRunning()); manager.Dispose();
    }

    [Fact]
    public async Task Real_tcp_trigger_runs_without_manager_window_and_solution_close_cancels_it()
    {
        var solutions = new SolutionManager(Path.Combine(Path.GetTempPath(), "SunEyeAuto", Guid.NewGuid().ToString("N")));
        await using var composition = new CommunicationRuntimeComposition();
        await using var service = new CommunicationApplicationService(solutions, composition);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Automation.Dispatcher = async (_, token) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { canceled.TrySetResult(); throw; }
            return true;
        };
        var device = new Communication { ProtocolKey = "tcp-client", Host = "127.0.0.1",
            Port = ((IPEndPoint)listener.LocalEndpoint).Port, EnableDataUpload = true, IsEnabled = true };
        solutions.SetCurrentSolution(new Solution { Communications = new() { device }, CommunicationTriggers = new()
            { new() { Source = "GlobalData", MatchMode = "等于", Text = "GO", TargetId = "flow" } } });
        using var peer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await service.ReadyAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("GO"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        solutions.CloseSolution();
        await service.ReadyAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(composition.Runtime.IsConnected(device.Id));
    }

    [Fact]
    public async Task Workflow_response_writes_split_values_then_id_at_bound_register_address()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var device = new Communication { ProtocolKey = "modbus", Host = "127.0.0.1",
            ModbusMode = "TcpClient", UnitId = 7, Port = ((IPEndPoint)listener.LocalEndpoint).Port, IsEnabled = true };
        var point = new ModbusAddressPoint { StartAddress = 40, Count = 4, IntBits = "16bit" };
        device.ModbusAddressPoints.Add(point);
        await using var runtime = new CommunicationRuntimeManager();
        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        await using var service = new CommunicationAutomationService(runtime);
        var flow = new SunEyeVision.Workflow.Workflow();
        var config = new CommunicationResponseDefinition { Enabled = true, DeviceId = device.Id, TargetId = point.Id,
            DataType = "int", Separator = ",", Content = "10,20", AlternateContent = "30,40" };
        service.Reset(new Solution { Communications = new() { device }, Workflows = new() { flow },
            CommunicationResponses = new() { WorkflowState = config } });
        service.WorkflowStateChanged(flow.Id, true);
        var stream = peer.GetStream(); var header = new byte[7];
        await stream.ReadExactlyAsync(header, timeout.Token);
        var request = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1];
        await stream.ReadExactlyAsync(request, timeout.Token);
        Assert.Equal(16, request[0]); Assert.Equal(40, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(1)));
        Assert.Equal(3, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(3)));
        Assert.Equal(new byte[] { 0, 30, 0, 40, 0, 1 }, request[6..]);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), 6);
        await stream.WriteAsync(header.Concat(request.Take(5)).ToArray(), timeout.Token);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        service.WorkflowStateChanged(flow.Id, true);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(stream.DataAvailable);
        config.AlternateContent = "30,abc"; service.Refresh(new Solution { Communications = new() { device },
            Workflows = new() { flow }, CommunicationResponses = new() { WorkflowState = config } });
        var errors = new ConcurrentQueue<string>(); service.Diagnostic += (_, text) => errors.Enqueue(text);
        service.WorkflowStateChanged(flow.Id, false); // valid idle -> acknowledge first
        await stream.ReadExactlyAsync(header, timeout.Token);
        request = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1]; await stream.ReadExactlyAsync(request, timeout.Token);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), 6);
        await stream.WriteAsync(header.Concat(request.Take(5)).ToArray(), timeout.Token);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        service.WorkflowStateChanged(flow.Id, true);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(stream.DataAvailable);
        Assert.Contains(errors, text => text.Contains("自动执行失败"));
    }

    [Fact]
    public void Native_response_rejects_wrong_type_empty_segments_overflow_and_read_only_before_write()
    {
        var device = new Communication { ProtocolKey = "modbus" };
        var point = new ModbusAddressPoint { StartAddress = 40, Count = 1, IntBits = "16bit" };
        device.ModbusAddressPoints.Add(point);
        var config = new CommunicationResponseDefinition { DeviceId = device.Id, TargetId = point.Id, DataType = "int", Separator = "," };
        Assert.Throws<FormatException>(() => CommunicationResponseFormatter.Create(config, device, "1,abc", "1"));
        Assert.Throws<FormatException>(() => CommunicationResponseFormatter.Create(config, device, "1,,2", "1"));
        using var runtime = new RuntimeDisposable();
        var sender = new CommunicationSendEventService(runtime.Runtime);
        Assert.Throws<FormatException>(() => sender.Prepare(CommunicationResponseFormatter.Create(config, device, "1,2", "1"), device));
        Assert.ThrowsAny<Exception>(() => sender.Prepare(CommunicationResponseFormatter.Create(config, device, "999999"), device));
        point.AccessMode = "只读";
        Assert.ThrowsAny<Exception>(() => sender.Prepare(CommunicationResponseFormatter.Create(config, device, "1"), device));
    }

    [Fact]
    public async Task Closing_solution_cancels_delayed_response_before_any_write()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var device = new Communication { ProtocolKey = "tcp-client", IsEnabled = true };
        var errors = new ConcurrentQueue<string>(); service.Diagnostic += (_, text) => errors.Enqueue(text);
        service.Reset(new Solution { Communications = new() { device }, CommunicationResponses = new()
            { SolutionLoaded = new() { Enabled = true, DeviceId = device.Id, Content = "ready", DelaySeconds = 1 } } });
        service.SolutionLoaded(); service.Reset(null);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Empty(errors);
    }

    [Fact]
    public void Trigger_response_config_roundtrip_keeps_stable_ids_and_no_runtime_state()
    {
        var rule = new CommunicationTriggerDefinition { EventId = Guid.NewGuid(), FieldId = Guid.NewGuid(), TargetId = "workflow-id" };
        var solution = new Solution { CommunicationTriggers = new() { rule },
            CommunicationResponses = new() { WorkflowState = new() { Enabled = true, DeviceId = "device-id", TargetId = "point-id",
                DataType = "int", Separator = ",", AlternateContent = "1,2" } } };
        var json = JsonSerializer.Serialize(solution, WorkflowSerializationOptions.Default);
        var loaded = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!;
        Assert.Equal(rule.EventId, loaded.CommunicationTriggers[0].EventId);
        Assert.Equal(rule.FieldId, loaded.CommunicationTriggers[0].FieldId);
        Assert.Equal("point-id", loaded.CommunicationResponses.WorkflowState.TargetId);
        Assert.Equal("1,2", loaded.CommunicationResponses.WorkflowState.AlternateContent);
    }

    [Fact]
    public async Task Responses_keep_workflow_and_camera_transitions_in_order_on_one_device()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var device = new Communication { ProtocolKey = "tcp-client", Host = "127.0.0.1",
            Port = ((IPEndPoint)listener.LocalEndpoint).Port, IsEnabled = true };
        await using var runtime = new CommunicationRuntimeManager();
        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        await using var service = new CommunicationAutomationService(runtime);
        var flow = new SunEyeVision.Workflow.Workflow();
        var camera = new Device { Type = DeviceType.Camera };
        service.Reset(new Solution { Communications = new() { device }, Workflows = new() { flow }, Devices = new() { camera },
            CommunicationResponses = new()
            {
                WorkflowState = new() { Enabled = true, DeviceId = device.Id, Content = "idle", AlternateContent = "busy", Terminator = "\n" },
                CameraState = new() { Enabled = true, DeviceId = device.Id, Content = "connected", AlternateContent = "dropped", Terminator = "\n" }
            } });
        service.WorkflowStateChanged(flow.Id, true); service.WorkflowStateChanged(flow.Id, false);
        service.CameraStateChanged(camera.Id, true); service.CameraStateChanged(camera.Id, false);
        var expected = "busy,1\nidle,1\nconnected,1\ndropped,1\n";
        var bytes = new byte[Encoding.UTF8.GetByteCount(expected)];
        await peer.GetStream().ReadExactlyAsync(bytes, timeout.Token);
        await service.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Protocol_identifiers_stay_stable_after_delete_reorder_and_reload()
    {
        await using var runtime = new CommunicationRuntimeManager();
        await using var service = new CommunicationAutomationService(runtime);
        var first = new SunEyeVision.Workflow.Workflow(); var second = new SunEyeVision.Workflow.Workflow();
        var solution = new Solution { Workflows = new() { first, second } };
        service.Reset(solution);
        Assert.Equal(2, solution.CommunicationResponses.WorkflowIdentifiers[second.Id]);
        solution.Workflows.Remove(first); solution.Workflows.Add(new());
        service.Refresh(solution);
        Assert.Equal(2, solution.CommunicationResponses.WorkflowIdentifiers[second.Id]);
        Assert.Equal(3, solution.CommunicationResponses.WorkflowIdentifiers[solution.Workflows.Last().Id]);
        var loaded = JsonSerializer.Deserialize<Solution>(JsonSerializer.Serialize(solution, WorkflowSerializationOptions.Default), WorkflowSerializationOptions.Default)!;
        service.Reset(loaded);
        Assert.Equal(2, loaded.CommunicationResponses.WorkflowIdentifiers[second.Id]);
    }

    [Fact]
    public async Task Failed_workflow_does_not_publish_success_completion()
    {
        var manager = new WorkflowExecutionManager(new EmptyInput(), new WorkflowEngine(VisionLogger.Instance), new RuntimeInputSnapshotService()) { AllowLegacyEditorCommitForTests = true };
        var completed = 0; var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.WorkflowExecutionCompleted += (_, _) => Interlocked.Increment(ref completed);
        manager.WorkflowExecutionError += (_, _) => failed.TrySetResult();
        await manager.RunSingleAsync(new WorkflowTabViewModel()).WaitAsync(TimeSpan.FromSeconds(3));
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, completed); manager.Dispose();
    }

    [Fact]
    public async Task Single_module_uses_a_parameter_snapshot_and_rejects_missing_upstream_results()
    {
        ToolRegistry.RegisterTool(typeof(AutomationProbeTool));
        var engine = new WorkflowEngine(VisionLogger.Instance);
        var workflow = engine.CreateWorkflow("communication-probe-flow", "通信单模块探针");
        var parameters = new AutomationProbeParameters { Value = 17 };
        var node = new WorkflowNodeBase("probe-node", "探针", "探针", "CommunicationAutomationProbe") { Parameters = parameters };
        workflow.Nodes.Add(node);
        var manager = new WorkflowExecutionManager(new EmptyInput(), engine, new RuntimeInputSnapshotService()) { AllowLegacyEditorCommitForTests = true };
        Assert.True(await manager.RunCommunicationOperationAsync(token =>
            manager.RunCommunicationNodeAsync(workflow.Id, node.Id, token), default));
        Assert.Equal(17, AutomationProbeTool.SeenValue);
        Assert.Equal(17, parameters.Value);
        node.ParamSettings.SetSetting(new ParamSetting { ParameterName = "Value", BindingType = BindingType.NodeOutput,
            SourceNodeId = "upstream", SourceProperty = "Value" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RunCommunicationOperationAsync(token =>
            manager.RunCommunicationNodeAsync(workflow.Id, node.Id, token), default));
        Assert.False(manager.IsAnyRunning()); manager.Dispose();
    }

    [Fact]
    public async Task Communication_mapping_overrides_only_execution_copy_and_does_not_leak_to_next_run()
    {
        ToolRegistry.RegisterTool(typeof(AutomationProbeTool));
        var engine = new WorkflowEngine(VisionLogger.Instance);
        var workflow = engine.CreateWorkflow("mapping-probe-flow", "参数绑定探针");
        var parameters = new AutomationProbeParameters { Value = 17 };
        var node = new WorkflowNodeBase("mapping-probe-node", "探针", "探针", "CommunicationAutomationProbe") { Parameters = parameters };
        node.ParamSettings.SetSetting(ParamSetting.CreateConstant("Value", 23));
        workflow.Nodes.Add(node);
        var manager = new WorkflowExecutionManager(new EmptyInput(), engine, new RuntimeInputSnapshotService()) { AllowLegacyEditorCommitForTests = true };
        Assert.True(await manager.RunCommunicationOperationAsync(token => manager.RunCommunicationNodeAsync(workflow.Id, node.Id, token),
            default, new[] { new CommunicationParameterAssignment(node.Id, "Value", "运行参数", 42) }));
        Assert.Equal(42, AutomationProbeTool.SeenValue);
        Assert.Equal(17, parameters.Value);
        Assert.Equal(23, node.ParamSettings.GetSetting("Value")!.ConstantValue);
        Assert.True(await manager.RunCommunicationOperationAsync(token => manager.RunCommunicationNodeAsync(workflow.Id, node.Id, token), default));
        Assert.Equal(23, AutomationProbeTool.SeenValue);
        manager.Dispose();
    }

    [Fact]
    public async Task Full_workflow_mapping_overrides_execution_binding_without_changing_design_binding()
    {
        ToolRegistry.RegisterTool(typeof(AutomationProbeTool));
        var logger = VisionLogger.Instance;
        var engine = new WorkflowEngine(logger);
        var workflow = engine.CreateWorkflow("binding-override-flow", "完整流程绑定探针");
        var parameters = new AutomationProbeParameters { Value = 17 };
        var node = new WorkflowNodeBase("binding-override-node", "探针", "探针", "CommunicationAutomationProbe") { Parameters = parameters };
        node.ParamSettings.SetSetting(ParamSetting.CreateConstant("Value", 23));
        workflow.Nodes.Add(node);
        var overrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        { [node.Id] = new Dictionary<string, object?> { ["Value"] = 42 } };
        var request = new WorkflowRunRequest(workflow.Id, nodeParameterOverrides: overrides, nodeBindingOverrides: overrides);
        ((Dictionary<string, object?>)overrides[node.Id])["Value"] = 99;
        var executor = new WorkflowExecutionEngine(engine, new PluginManager(logger), logger);
        var result = await executor.ExecuteWorkflowAsync(request, default);
        Assert.True(result.Success, string.Join(";", result.Errors));
        Assert.Equal(42, AutomationProbeTool.SeenValue);
        Assert.Equal(17, parameters.Value);
        Assert.Equal(23, node.ParamSettings.GetSetting("Value")!.ConstantValue);
    }

    public sealed class AutomationProbeParameters : ToolParameters, IRuntimeParameterContract
    {
        public int Value { get; set; }
        public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters() => new[] { new RuntimeParameterDescriptor("Value", "值", ValueType: typeof(int)) };
    }
    public sealed class AutomationProbeResults : ToolResults { }
    [Tool("CommunicationAutomationProbe", "通讯执行回归探针")]
    public sealed class AutomationProbeTool : IToolPlugin<AutomationProbeParameters, AutomationProbeResults>
    {
        public static int SeenValue;
        public AutomationProbeResults Run(AutomationProbeParameters parameters)
        {
            SeenValue = parameters.Value; parameters.Value = 999;
            return new() { Status = ExecutionStatus.Success };
        }
    }

    private sealed class EmptyInput : IInputProvider { public Task<object?> GetInputImageAsync() => Task.FromResult<object?>(null); }
    private sealed class BlockingInput : IInputProvider
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<object?> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<object?> GetInputImageAsync() { Entered.TrySetResult(); return await Release.Task; }
    }
    private sealed class RuntimeDisposable : IDisposable
    {
        public CommunicationRuntimeManager Runtime { get; } = new();
        public void Dispose() => Runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
