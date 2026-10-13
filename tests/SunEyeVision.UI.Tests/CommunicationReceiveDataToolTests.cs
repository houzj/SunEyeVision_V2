using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;
using SunEyeVision.Tool.ReceiveData;
using SunEyeVision.Tool.ReceiveData.Views;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ProtocolAssemble;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;
using ToolContext = SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class CommunicationReceiveDataToolTests
{
    private static CommunicationReceiveService Store(bool active = false)
    {
        var store = new CommunicationReceiveService();
        store.Configure(new[] { new CommunicationReceiveSource("device", "", "TCP", "Binary", ActiveRead: active) }); return store;
    }
    [Fact]
    public async Task Current_message_has_no_age_limit_is_not_consumed_and_changes_at_execution()
    {
        var service = Store();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync("device", ""));
        service.Publish("device", "", DateTimeOffset.UtcNow.AddDays(-1), new BinaryPayload(new byte[] { 1 }));
        var request = new WorkflowRunRequest("flow", communicationInputs: service);
        service.Publish("device", "", DateTimeOffset.UtcNow, new BinaryPayload(new byte[] { 2 }));
        var a = await request.CommunicationInputs!.ReadAsync("device", "");
        Assert.Equal("02", a.Value.ToString()); Assert.Equal(a.Id, (await service.ReadAsync("device", "")).Id);
        service.InvalidateDevice("device"); await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync("device", ""));
        Assert.Null(service.Publish("device", "", DateTimeOffset.UtcNow, new BinaryPayload(new byte[] { 3 })));
        service.MarkConnected("device"); await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync("device", ""));
    }
    [Fact]
    public async Task Active_read_never_uses_cache_and_rejects_changed_configuration_or_cancel()
    {
        var service = Store(true); var reads = 0;
        service.Publish("device", "", DateTimeOffset.UtcNow, new BinaryPayload(new byte[] { 9 }));
        service.ActiveReader = (_, _) => Task.FromResult(((object)new BinaryPayload(new byte[] { (byte)++reads }), (DateTimeOffset?)null));
        Assert.Equal("01", (await service.ReadAsync("device", "")).Value.ToString());
        Assert.Equal("02", (await service.ReadAsync("device", "")).Value.ToString());
        service.ActiveReader = (_, _) => throw new IOException("read failed");
        await Assert.ThrowsAsync<IOException>(() => service.ReadAsync("device", ""));
        service.ActiveReader = (s, _) => { service.Configure(new[] { s with { ConfigurationKey = "changed" } }); return Task.FromResult(((object)"stale", (DateTimeOffset?)null)); };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync("device", ""));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReadAsync("device", "", new CancellationToken(true)));
    }
    [Theory]
    [InlineData("String", "ABC")]
    [InlineData("Integer", 7)]
    [InlineData("Double", 1.5)]
    [InlineData("Boolean", true)]
    public void Variable_rows_use_current_service_not_start_globals_preserve_types_and_fail_atomically(string type, object value)
    {
        var service = Store(); var current = value;
        service.VariableReader = ids => ids.ToDictionary(id => id, id => new ReceiveVariableValue(new(id, "v", "group", type), current));
        var p = new ReceiveDataParameters { SourceKind = "Variable", Variables = new() { new() { VariableId = "v", DataType = type } } };
        p.SetExecutionContext(new ToolContext { CommunicationInputs = service, GlobalVariables = new Dictionary<string, object?> { ["v"] = "OLD" } });
        var result = new ReceiveDataTool().Run(p); Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("全局 / group / v", Assert.Single(result.Analysis).Source);
        Assert.Contains("当前值：", Assert.Single(result.Analysis).Detail);
        Assert.Contains("读取时间", Assert.Single(result.Analysis).Detail);
        var field = p.Variables[0].Definition(); Assert.NotNull(Value(result, field));
        var key = field.BindingKey; p.Variables[0].Name = "renamed"; Assert.Equal(key, p.Variables[0].Definition().BindingKey);
        p.Variables.Add(new() { Name = "out1", VariableId = "v", DataType = type == "String" ? "Integer" : "String" });
        result = new ReceiveDataTool().Run(p); Assert.False(result.IsSuccess); Assert.All(result.GetOutputVariables().Where(f => f.Name.StartsWith("Field_")), f => Assert.Null(f.RuntimeValue));
    }
    [Fact]
    public async Task Application_variable_reads_current_value_by_stable_id_and_rejects_deletion()
    {
        var solutions = new SolutionManager(Path.Combine(Path.GetTempPath(), "SunEyeReceive", Guid.NewGuid().ToString("N")));
        await using var composition = new CommunicationRuntimeComposition(); await using var app = new CommunicationApplicationService(solutions, composition);
        var variable = new GlobalVariable("v", 1L, "Integer", "group");
        var solution = new Solution { GlobalVariables = new() { variable } }; solutions.SetCurrentSolution(solution); await app.ReadyAsync();
        var p = new ReceiveDataParameters { SourceKind = "Variable", Variables = new() { new() { VariableId = variable.Id, DataType = "Integer" } } };
        p.SetExecutionContext(new ToolContext { CommunicationInputs = app.Receives, GlobalVariables = new Dictionary<string, object?> { ["v"] = 1L } });
        variable.Value = 2L;
        var result = new ReceiveDataTool().Run(p); Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2L, Assert.Single(Assert.IsType<long[]>(Value(result, p.Variables[0].Definition()))));
        variable.Name = "renamed"; Assert.Equal("renamed", app.Receives.ReadVariables(new[] { variable.Id })[variable.Id].Source.Name);
        result = new ReceiveDataTool().Run(p);
        Assert.Equal("全局 / group / renamed", Assert.Single(result.Analysis).Source);
        Assert.DoesNotContain(variable.Id, Assert.Single(result.Analysis).Detail);
        solution.GlobalVariables.Clear(); result = new ReceiveDataTool().Run(p); Assert.False(result.IsSuccess);
    }
    [Theory]
    [InlineData("", "TCP客户端1", "", "TCP客户端1")]
    [InlineData("point", "OPC UA1", "test1", "OPC UA1 / test1")]
    [InlineData("address", "Modbus1", "Address1", "Modbus1 / Address1")]
    [InlineData("", "", "", "通信设备")]
    public void Device_analysis_shows_source_names_and_read_value_not_internal_ids(string target, string deviceName, string targetName, string expected)
    {
        var deviceId = Guid.NewGuid().ToString(); var service = new CommunicationReceiveService();
        service.Configure(new[] { new CommunicationReceiveSource(deviceId, target, "", "Binary", DeviceName: deviceName, TargetName: targetName) });
        service.Publish(deviceId, target, DateTimeOffset.UtcNow, new BinaryPayload(new byte[] { 0x41, 0x42 }));
        var p = new ReceiveDataParameters { DeviceId = deviceId, TargetId = target };
        p.SetExecutionContext(new ToolContext { CommunicationInputs = service });
        var result = new ReceiveDataTool().Run(p); Assert.True(result.IsSuccess, result.ErrorMessage);
        var row = Assert.Single(result.Analysis); Assert.Equal(expected, row.Source);
        Assert.Contains("HEX 4142", row.Detail); Assert.Contains("数据时间", row.Detail);
        Assert.DoesNotContain(deviceId, row.Source + row.Detail);
        Assert.Equal("4142", Assert.Single(Assert.IsType<BinaryPayload[]>(Value(result, p.OutputDefinition()))).ToString());
    }
    [Fact]
    public async Task Async_receive_cancel_or_read_error_clears_business_outputs_without_cache_fallback()
    {
        var p = new ReceiveDataParameters { DeviceId = "device" };
        Assert.False(new ReceiveDataTool().Run(p).IsSuccess);
        var service = Store(true); service.Publish("device", "", DateTimeOffset.UtcNow, new BinaryPayload(new byte[] { 9 }));
        service.ActiveReader = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return ((object)"never", null); };
        using var cancellation = new CancellationTokenSource();
        var request = new ToolExecutionRequest(p) { CommunicationInputs = service, CancellationToken = cancellation.Token };
        var tool = new ReceiveDataTool(); var pending = tool.ExecuteAsync(request); cancellation.Cancel();
        var result = await pending; Assert.Equal(ExecutionStatus.Cancelled, result.Status);
        Assert.Null(Value((CommunicationToolResults)result, p.OutputDefinition()));
        service.ActiveReader = (_, _) => throw new IOException("Read failed");
        result = await tool.ExecuteAsync(new ToolExecutionRequest(p) { CommunicationInputs = service });
        Assert.False(result.IsSuccess); Assert.Null(Value((CommunicationToolResults)result, p.OutputDefinition()));
    }
    [Fact]
    public async Task Receive_variable_dependencies_use_successful_producer_not_design_initial_value()
    {
        var service = Store();
        service.VariableCatalog = () => new[] { new ReceiveVariableSource("v", "code", "group", "String", true, "producer", ProtocolAssembleParameters.TextOutput.BindingKey) };
        service.VariableReader = _ => throw new InvalidOperationException("No producer result");
        var p = new ReceiveDataParameters { SourceKind = "Variable", Variables = new() { new() { VariableId = "v" } } };
        var produced = new ProtocolAssembleTool().Run(new ProtocolAssembleParameters { Parts = new() { new() { InputText = "CURRENT" } } });
        var result = await new ReceiveDataTool().ExecuteAsync(new ToolExecutionRequest(p) { CommunicationInputs = service,
            GlobalVariables = new Dictionary<string, object?> { ["code"] = "DESIGN" }, NodeResults = new Dictionary<string, ToolResults> { ["producer"] = produced } });
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("CURRENT", Assert.Single(Assert.IsType<string[]>(Value((CommunicationToolResults)result, p.Variables[0].Definition()))));
        result = await new ReceiveDataTool().ExecuteAsync(new ToolExecutionRequest(p) { CommunicationInputs = service,
            GlobalVariables = new Dictionary<string, object?> { ["code"] = "DESIGN" } }); Assert.False(result.IsSuccess);
        var flow = new WorkflowEngine(VisionLogger.Instance).CreateWorkflow("flow", "variables");
        flow.Nodes.Add(new WorkflowNodeBase("producer", "组装", "组装", "ProtocolAssemble") { Parameters = new ProtocolAssembleParameters() });
        flow.Nodes.Add(new WorkflowNodeBase("receiver", "接收", "接收", "ReceiveData") { Parameters = p });
        var variable = new GlobalVariable("code", "DESIGN", "String") { Id = "v", ProducerBinding = new() { WorkflowId = flow.Id, NodeId = "producer", ResultName = ProtocolAssembleParameters.TextOutput.BindingKey } };
        var plan = SolutionExecutionDependencyGraph.Compile(new Solution { Workflows = new() { flow }, GlobalVariables = new() { variable } });
        Assert.Contains(plan.Dependencies, edge => edge.Kind == SolutionDependencyKind.GlobalVariable && edge.GlobalVariableName == "code");
        // No canvas edge: the receive variable itself must order the local producer before its consumer.
        flow.Nodes.Move(1, 0);
        ToolRegistry.RegisterTool(typeof(ReceiveDataTool)); ToolRegistry.RegisterTool(typeof(ProtocolAssembleTool));
        var engine = new WorkflowEngine(VisionLogger.Instance); engine.RegisterWorkflow(flow);
        var runner = new WorkflowExecutionEngine(engine, new PluginManager(VisionLogger.Instance), VisionLogger.Instance);
        var run = await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id, communicationInputs: service), default);
        Assert.True(run.Success, string.Join(";", run.Errors));
        service.VariableCatalog = () => new[] { new ReceiveVariableSource("v", "code", "group", "String", true, "producer", ProtocolAssembleParameters.TextOutput.BindingKey, "foreign-flow") };
        var scoped = new RuntimeVariableReceiveService(service, null, workflowId: flow.Id);
        result = await new ReceiveDataTool().ExecuteAsync(new ToolExecutionRequest(p) { CommunicationInputs = scoped,
            NodeResults = new Dictionary<string, ToolResults> { ["producer"] = produced } });
        Assert.False(result.IsSuccess); // Same node id from a different workflow must not supply this variable.
    }
    [Fact]
    public void Receive_schema_serialization_and_notifications_keep_row_identity_and_remove_runtime_settings()
    {
        var p = new ReceiveDataParameters { SourceKind = "Variable", Variables = new() { new() { VariableId = "v" }, new() { Name = "out1", VariableId = "w", DataType = "Integer" } } };
        var changes = 0; p.OutputContractChanged += () => changes++;
        var key = p.Variables[0].Definition().BindingKey; p.Variables.Move(0, 1); p.Variables[1].Name = "renamed";
        Assert.True(changes >= 2); Assert.Equal(key, p.Variables[1].Definition().BindingKey);
        var json = JsonSerializer.Serialize<ToolParameters>(p, WorkflowSerializationOptions.Default);
        var copy = Assert.IsType<ReceiveDataParameters>(JsonSerializer.Deserialize<ToolParameters>(json, WorkflowSerializationOptions.Default));
        Assert.Equal(key, copy.Variables[1].Definition().BindingKey); Assert.Equal(2, copy.GetOutputContract().Count(f => f.Name.StartsWith("Field_")));
        Assert.DoesNotContain("MaximumAge", json); Assert.DoesNotContain("CommunicationInputs", json);
    }
    [Fact]
    public void Editor_reuses_host_selector_groups_devices_filters_targets_and_renders_themes()
    {
        Exception? failure = null; var thread = new Thread(() =>
        {
            try
            {
                var service = Store(); service.Configure(new[] { new CommunicationReceiveSource("raw", "", "TCP", "Binary", DeviceName: "TCP"),
                    new CommunicationReceiveSource("plc", "a", "PLC/A", "Binary", DeviceName: "PLC", TargetName: "A", ActiveRead: true),
                    new CommunicationReceiveSource("plc", "b", "PLC/B", "Binary", DeviceName: "PLC", TargetName: "B", ActiveRead: true) });
                var picked = 0; var provider = new DataSourceQueryService { CommunicationReceiveService = service,
                    SelectReceiveVariable = () => { picked++; return new("v", "code", "group", "Integer"); } };
                var p = new ReceiveDataParameters { DeviceId = "raw" }; var editor = new ReceiveDataEditor(); editor.SetParameters(p); editor.SetDataProvider(provider);
                Assert.Equal(new Size(460, 600), editor.PreferredDebugWindowSize); Assert.Equal(2, ((ComboBox)editor.FindName("Devices")).Items.Count);
                Assert.Equal(Visibility.Collapsed, ((ComboBox)editor.FindName("Targets")).Visibility);
                var devices = (ComboBox)editor.FindName("Devices"); devices.SelectedIndex = 1;
                Assert.Equal("plc", p.DeviceId); Assert.Equal("", p.TargetId); Assert.Equal(2, ((ComboBox)editor.FindName("Targets")).Items.Count);
                p.SourceKind = "Variable";
                var button = new Button { DataContext = p.Variables[0] };
                typeof(ReceiveDataEditor).GetMethod("SelectVariable_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(editor, new object[] { button, new RoutedEventArgs() });
                Assert.Equal(1, picked); Assert.Equal("全局.code", p.Variables[0].VariablePath); Assert.Equal("Integer", p.Variables[0].DataType);
                Assert.Equal(Visibility.Collapsed, ((Grid)editor.FindName("DeviceRow")).Visibility); Assert.Null(editor.FindName("AgeRow"));
                ResourceDictionary? previous = null;
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    if (previous != null) editor.Resources.MergedDictionaries.Remove(previous);
                    previous = new() { Source = new Uri($"/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml", UriKind.Relative) }; editor.Resources.MergedDictionaries.Add(previous);
                    editor.Measure(new Size(460, 514)); editor.Arrange(new Rect(0, 0, 460, 514)); editor.UpdateLayout();
                    var root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "SunEyeVision.slnx"))) root = Directory.GetParent(root)!.FullName;
                    var dir = Path.Combine(root, "artifacts", "validation", "receive-current-20261012"); Directory.CreateDirectory(dir);
                    var bitmap = new RenderTargetBitmap(460, 514, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(dir, $"editor-{theme.ToLowerInvariant()}.png")); encoder.Save(file);
                }
            }
            catch (Exception e) { failure = e; }
        }); thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Fact]
    public async Task Real_tcp_new_message_after_trigger_is_current_for_workflow()
    {
        var solutions = new SolutionManager(Path.Combine(Path.GetTempPath(), "SunEyeReceive", Guid.NewGuid().ToString("N")));
        await using var composition = new CommunicationRuntimeComposition(); await using var app = new CommunicationApplicationService(solutions, composition);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var device = new Communication { Name = "TCP", ProtocolKey = "tcp-client", Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, EnableReceiveTerminator = true, ReceiveTerminator = "\\n" };
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.Runtime.DataReceived += (_, _) => { if (!first.TrySetResult()) second.TrySetResult(); };
        solutions.SetCurrentSolution(new Solution { Communications = new() { device } }); using var peer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5)); await app.ReadyAsync();
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("ABC,2\n")); await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var requestService = app.Receives;
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("NEW,9\n")); await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await ExecuteChain(requestService, device.Id, "", false, "RESULT,NEW,9");
        solutions.CloseSolution(); await app.ReadyAsync(); await Assert.ThrowsAsync<InvalidOperationException>(() => requestService.ReadAsync(device.Id, ""));
    }
    [Fact]
    public async Task Real_modbus_with_polling_disabled_reads_once_per_execution_and_flows_to_assembly()
    {
        var solutions = new SolutionManager(Path.Combine(Path.GetTempPath(), "SunEyeReceive", Guid.NewGuid().ToString("N")));
        await using var composition = new CommunicationRuntimeComposition(); await using var app = new CommunicationApplicationService(solutions, composition);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var device = new Communication { Name = "PLC", ProtocolKey = "modbus", Host = "127.0.0.1", ModbusMode = "TcpClient", Port = ((IPEndPoint)listener.LocalEndpoint).Port, UnitId = 7 };
        var point = new ModbusAddressPoint { StartAddress = 12, Count = 2, PollEnabled = false, TimeoutMs = 2000 }; device.ModbusAddressPoints.Add(point);
        var reads = 0;
        var simulator = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token); var stream = peer.GetStream();
            for (var n = 0; n < 2; n++)
            {
                var header = new byte[7]; await stream.ReadExactlyAsync(header, timeout.Token);
                var request = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1]; await stream.ReadExactlyAsync(request, timeout.Token);
                Assert.Equal(3, request[0]); Assert.Equal(12, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(1)));
                Interlocked.Increment(ref reads); var response = new byte[13]; header.AsSpan(0, 4).CopyTo(response); BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), 7); response[6] = header[6];
                new byte[] { 3, 4, 0, (byte)(92 + n), 65, 68 }.CopyTo(response, 7); await stream.WriteAsync(response, timeout.Token);
            }
            await Task.Delay(Timeout.Infinite, timeout.Token);
        }, timeout.Token);
        solutions.SetCurrentSolution(new Solution { Communications = new() { device } }); await app.ReadyAsync();
        Assert.Equal(0, reads); await ExecuteChain(app.Receives, device.Id, point.Id, true, "RESULT,92,AD");
        await ExecuteChain(app.Receives, device.Id, point.Id, true, "RESULT,93,AD"); Assert.Equal(2, reads);
        solutions.CloseSolution(); await app.ReadyAsync(); timeout.Cancel(); try { await simulator; } catch (OperationCanceledException) { }
    }
    private static async Task ExecuteChain(ICommunicationReceiveService snapshot, string deviceId, string targetId, bool bytes, string expected)
    {
        ToolRegistry.RegisterTool(typeof(ReceiveDataTool)); ToolRegistry.RegisterTool(typeof(ProtocolParseTool)); ToolRegistry.RegisterTool(typeof(ProtocolAssembleTool));
        var engine = new WorkflowEngine(VisionLogger.Instance); var flow = engine.CreateWorkflow(Guid.NewGuid().ToString(), "接收联合链");
        var receive = new ReceiveDataParameters { DeviceId = deviceId, TargetId = targetId };
        var source = new WorkflowNodeBase(Guid.NewGuid().ToString(), "接收", "接收", "ReceiveData") { Parameters = receive };
        var parse = new ProtocolParseParameters { Mode = bytes ? "Bytes" : "Text", InputText = source.Id + "." + receive.OutputDefinition().BindingKey,
            Outputs = new() { new() { Name = "first", DataType = bytes ? "int" : "string", Start = 0, End = 1 },
                new() { Name = "second", DataType = bytes ? "string" : "int", Start = 2, End = 3 } } };
        var parser = new WorkflowNodeBase(Guid.NewGuid().ToString(), "解析", "解析", "ProtocolParse") { Parameters = parse };
        var assemble = new ProtocolAssembleParameters { Parts = new() { new() { InputText = "RESULT" },
            new() { InputText = parser.Id + "." + parse.Outputs[0].OutputDefinition().BindingKey },
            new() { InputText = parser.Id + "." + parse.Outputs[1].OutputDefinition().BindingKey } } };
        var assembler = new WorkflowNodeBase(Guid.NewGuid().ToString(), "组装", "组装", "ProtocolAssemble") { Parameters = assemble };
        flow.Nodes.Add(source); flow.Nodes.Add(parser); flow.Nodes.Add(assembler);
        flow.Connections.Add(new WorkflowConnection(Guid.NewGuid().ToString(), source.Id, parser.Id));
        flow.Connections.Add(new WorkflowConnection(Guid.NewGuid().ToString(), parser.Id, assembler.Id));
        var query = new DataSourceQueryService(flow, flow);
        var available = query.GetAvailableDataSources(parser.Id);
        Assert.Same(receive, query.GetNodeContext(source.Id)!.Parameters);
        var candidate = Assert.Single(available.Where(s => s.PropertyName.StartsWith("Field_")));
        Assert.Equal(source.Id, candidate.SourceNodeId);
        Assert.Equal(receive.OutputDefinition().BindingKey, candidate.PropertyName);
        Assert.Equal("Binary", candidate.VariableType);
        var executor = new WorkflowExecutionEngine(engine, new PluginManager(VisionLogger.Instance), VisionLogger.Instance);
        var result = await executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id, communicationInputs: snapshot), default);
        Assert.True(result.Success, string.Join(";", result.Errors));
        var assembled = Assert.IsType<CommunicationToolResults>(result.NodeResults[assembler.Id].Result);
        Assert.Equal(expected, Assert.Single(Assert.IsType<string[]>(Value(assembled, ProtocolAssembleParameters.TextOutput))));
        Assert.NotEmpty(assembled.Analysis);
    }
    private static object? Value(CommunicationToolResults result, CommunicationOutputField field) => result.GetOutputVariables().Single(v => v.Name == field.BindingKey).RuntimeValue;
}
