using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Tool.ProtocolAssemble;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.ReceiveData;
using SunEyeVision.Tool.SendData;
using SunEyeVision.Tool.SendData.Views;
using SunEyeVision.UI.Services.Communication;
using CommunicationDeviceSnapshotFactory = SunEyeVision.UI.Features.CommunicationManager.CommunicationDeviceSnapshotFactory;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationSendDataToolTests
{
    [Fact]
    public async Task Explicit_type_filters_binding_and_rejects_wrong_bound_value_with_details()
    {
        var p = Parameters("__GlobalVars__.number"); p.Rows[0].DataType = "Boolean"; p.IgnoreErrors = true;
        Assert.Equal(typeof(bool), Assert.Single(p.Rows[0].AllowedDataTypes));
        var service = new FakeSend(); var result = await Run(p, service, globals: new Dictionary<string, object?> { ["number"] = 1L });
        Assert.False(result.IsSuccess); Assert.Empty(service.Submitted);
        Assert.Contains("Boolean", result.ErrorMessage); Assert.Contains("输入类型", result.ErrorMessage);
        p.Rows[0].DataType = "Int32"; Assert.Equal(typeof(long), Assert.Single(p.Rows[0].AllowedDataTypes));
        p.Rows[0].DataType = "Double"; Assert.Equal(typeof(double), Assert.Single(p.Rows[0].AllowedDataTypes));
        p.Rows[0].DataType = "Binary"; Assert.Equal(typeof(BinaryPayload), Assert.Single(p.Rows[0].AllowedDataTypes));
    }
    [Theory]
    [InlineData("Int16", 32768L)]
    [InlineData("UInt16", -1L)]
    [InlineData("Int32", 2147483648L)]
    public void Explicit_integer_type_checks_range(string type, long value)
    { Assert.Throws<OverflowException>(() => CommunicationSendTypes.Validate(value, type)); }
    [Fact]
    public void Target_type_is_strict_but_aliases_and_workflow_vocabulary_are_explicit()
    {
        CommunicationSendTypes.RequireTarget("Int32", "int", "test1");
        var error = Assert.Throws<FormatException>(() => CommunicationSendTypes.RequireTarget("Double", "Int32", "test2"));
        Assert.Contains("test2", error.Message); Assert.Contains("Double", error.Message); Assert.Contains("Int32", error.Message);
        Assert.Equal("Integer", CommunicationSendTypes.WorkflowType("Int32"));
        Assert.Throws<FormatException>(() => CommunicationSendTypes.Validate(double.NaN, "Double"));
    }
    private sealed class FakeSend : ICommunicationSendService
    {
        public string Protocol = "Modbus";
        public object? ReceiptValue;
        public readonly List<string> Prepared = new(), Submitted = new();
        public Action<string>? AfterSubmit;
        public IReadOnlyList<CommunicationSendTarget> GetTargets() => new[] { new CommunicationSendTarget("Device", "", "d", "device", Protocol, "Binary") };
        public Task<PreparedCommunicationSend> PrepareAsync(CommunicationSendRequest request, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); var label = (string)request.Inputs[0].Value; Prepared.Add(label);
            if (request.Inputs.Any(i => Equals(i.Value, "bad"))) throw new FormatException("invalid");
            return Task.FromResult(new PreparedCommunicationSend(label, ReceiptValue ?? new BinaryPayload(Encoding.UTF8.GetBytes(label)), ct =>
            {
                ct.ThrowIfCancellationRequested(); if (label == "fail") throw new IOException("unknown submission");
                Submitted.Add(label); AfterSubmit?.Invoke(label); return Task.FromResult(1);
            }));
        }
        public void ConfigureVariablePublications(string nodeId, IReadOnlyList<SendVariablePublication> mappings) { }
    }
    private static SendDataParameters Parameters(params string[] values) => new()
        { DeviceId = "d", Rows = new(values.Select(v => new SendDataRow { InputText = v, TargetId = v })) };
    private static async Task<SendDataResults> Run(SendDataParameters p, ICommunicationSendService service, CancellationToken token = default,
        IDictionary<string, object?>? globals = null, string? node = null) =>
        Assert.IsType<SendDataResults>(await new ToolExecutor(new ParameterResolver()).ExecuteAsync(new SendDataTool(),
            new ToolExecutionRequest(p) { CommunicationOutputs = service, CancellationToken = token, GlobalVariables = globals, NodeId = node }));

    [Fact]
    public async Task All_preflight_finishes_before_any_write_and_invalid_input_blocks_all()
    {
        var service = new FakeSend(); var result = await Run(Parameters("ok", "bad"), service);
        Assert.False(result.IsSuccess); Assert.Empty(service.Submitted); Assert.Equal(new[] { "ok", "bad" }, service.Prepared);
        Assert.Empty(result.SentData);
    }
    [Theory]
    [InlineData(false, "False")]
    [InlineData(0L, "0")]
    [InlineData("ABC,2", "ABC,2")]
    public async Task Module_result_shows_successfully_submitted_native_value(object value, string expected)
    {
        var result = await Run(Parameters("point"), new FakeSend { ReceiptValue = value });
        Assert.True(result.IsSuccess); Assert.Empty(result.Payloads);
        Assert.Equal("point: " + expected, Assert.Single(result.SentData));
        Assert.Equal(result.SentData, Assert.IsType<string[]>(result.GetOutputVariables().Single(v => v.DisplayName == "已发送数据").RuntimeValue));
    }
    [Fact]
    public async Task Ignoring_independent_preflight_and_submit_errors_records_partial_facts()
    {
        var service = new FakeSend(); var p = Parameters("ok", "bad", "fail", "last"); p.IgnoreErrors = true;
        var result = await Run(p, service); Assert.Equal(ExecutionStatus.PartialSuccess, result.Status);
        Assert.Equal(2, result.Submitted); Assert.Equal(1, result.Skipped); Assert.Equal(1, result.Failed);
        Assert.Equal(new[] { "ok", "last" }, service.Submitted); Assert.Equal(2, result.Payloads.Count);
        Assert.Equal(2, result.SentData.Count);
        Assert.DoesNotContain(result.SentData, s => s.StartsWith("fail:") || s.StartsWith("bad:"));
    }
    [Fact]
    public async Task Raw_message_does_not_skip_internal_fields_even_with_ignore_enabled()
    {
        var service = new FakeSend { Protocol = "Raw" }; var p = Parameters("ok", "bad"); p.IgnoreErrors = true;
        Assert.False((await Run(p, service)).IsSuccess); Assert.Empty(service.Submitted);
    }
    [Fact]
    public async Task Cancel_after_first_commit_keeps_actual_receipt_and_does_not_send_second()
    {
        using var cts = new CancellationTokenSource(); var service = new FakeSend { AfterSubmit = _ => cts.Cancel() };
        var result = await Run(Parameters("one", "two"), service, cts.Token);
        Assert.Equal(ExecutionStatus.Cancelled, result.Status); Assert.Equal(new[] { "one" }, service.Submitted);
        Assert.Equal(1, result.Submitted); Assert.Single(result.Payloads);
        Assert.Single(result.SentData);
        Assert.NotNull(result.GetOutputVariables().Single(v => v.DisplayName == "已发送数据").RuntimeValue);
    }
    [Fact]
    public async Task Each_execution_sends_again_but_each_prepared_plan_only_commits_once()
    {
        var service = new FakeSend(); var p = Parameters("ok"); await Run(p, service); await Run(p, service); Assert.Equal(2, service.Submitted.Count);
        var plan = new PreparedCommunicationSend("single", "x", _ => Task.FromResult(1)); await plan.SubmitAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => plan.SubmitAsync());
    }
    [Fact]
    public void Save_reload_preserves_rows_and_their_stable_binding_identity()
    {
        var p = Parameters("x", "y"); var id = p.Rows[0].FieldId;
        p.Rows.Move(0, 1); var loaded = JsonSerializer.Deserialize<SendDataParameters>(JsonSerializer.Serialize(p))!;
        Assert.Equal(id, loaded.Rows[1].FieldId); Assert.Equal("x", loaded.Rows[1].InputText);
        Assert.DoesNotContain("CommunicationOutputs", JsonSerializer.Serialize(p));
        Assert.DoesNotContain("TargetChoices", JsonSerializer.Serialize(p));
    }
    [Fact]
    public void Nested_input_binding_forms_dependency_even_without_canvas_connection()
    {
        var id = Guid.NewGuid().ToString(); var flow = new SunEyeVision.Workflow.Workflow("flow", "send");
        flow.Nodes.Add(new WorkflowNodeBase("sender", "send", "send", "SendData") { Parameters = Parameters(id + ".Field") });
        flow.Nodes.Add(new WorkflowNodeBase(id, "source", "source", "ProtocolAssemble") { Parameters = new ProtocolAssembleParameters() });
        Assert.Contains(id, WorkflowExecutionDependencyGraph.Compile(flow).GetPredecessors("sender"));
    }
    [Fact]
    public async Task Real_tcp_assembly_to_send_workflow_and_hex_preserve_actual_bytes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var device = new Communication { Name = "TCP", ProtocolKey = "tcp-client", Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            EnableReceiveTerminator = true, ReceiveTerminator = "\\n" };
        var solution = new Solution { Communications = new() { device } };
        await using var runtime = new CommunicationRuntimeManager();
        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        var service = new WorkflowCommunicationSendService(() => solution, runtime);
        var receiveStore = new CommunicationReceiveService(); receiveStore.Configure(new[] { new CommunicationReceiveSource(device.Id, "", "TCP", "Binary") });
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.DataReceived += (_, message) => { receiveStore.Publish(device.Id, "", message.Timestamp, new BinaryPayload(message.Data)); arrived.TrySetResult(); };
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("ABC,2\n"), timeout.Token); await arrived.Task.WaitAsync(timeout.Token);
        ToolRegistry.RegisterTool(typeof(ProtocolAssembleTool)); ToolRegistry.RegisterTool(typeof(SendDataTool));
        ToolRegistry.RegisterTool(typeof(ReceiveDataTool)); ToolRegistry.RegisterTool(typeof(ProtocolParseTool));
        var engine = new WorkflowEngine(VisionLogger.Instance); var flow = engine.CreateWorkflow(Guid.NewGuid().ToString(), "send");
        var receive = new ReceiveDataParameters { DeviceId = device.Id };
        var receiver = new WorkflowNodeBase(Guid.NewGuid().ToString(), "接收", "接收", "ReceiveData") { Parameters = receive };
        var parse = new ProtocolParseParameters { InputText = receiver.Id + "." + receive.OutputDefinition().BindingKey,
            Outputs = new() { new() { Name = "first", DataType = "string" }, new() { Name = "second", DataType = "int" } } };
        var parser = new WorkflowNodeBase(Guid.NewGuid().ToString(), "解析", "解析", "ProtocolParse") { Parameters = parse };
        var assemble = new ProtocolAssembleParameters { Parts = new() { new() { InputText = "RESULT" },
            new() { InputText = parser.Id + "." + parse.Outputs[0].OutputDefinition().BindingKey },
            new() { InputText = parser.Id + "." + parse.Outputs[1].OutputDefinition().BindingKey } } };
        var source = new WorkflowNodeBase(Guid.NewGuid().ToString(), "组装", "组装", "ProtocolAssemble") { Parameters = assemble };
        var send = Parameters(source.Id + "." + ProtocolAssembleParameters.BytesOutput.BindingKey); send.DeviceId = device.Id;
        send.Rows[0].DataType = "Binary";
        var sink = new WorkflowNodeBase(Guid.NewGuid().ToString(), "发送", "发送", "SendData") { Parameters = send };
        flow.Nodes.Add(sink); flow.Nodes.Add(source); // No assembler-to-sender line; consumer is deliberately first.
        flow.Nodes.Add(receiver); flow.Nodes.Add(parser);
        flow.Connections.Add(new WorkflowConnection(Guid.NewGuid().ToString(), receiver.Id, parser.Id));
        flow.Connections.Add(new WorkflowConnection(Guid.NewGuid().ToString(), parser.Id, source.Id));
        var runner = new WorkflowExecutionEngine(engine, new PluginManager(VisionLogger.Instance), VisionLogger.Instance);
        var result = await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id, communicationInputs: receiveStore, communicationOutputs: service), timeout.Token);
        Assert.True(result.Success, string.Join(";", result.Errors));
        var received = new byte[12]; await peer.GetStream().ReadExactlyAsync(received, timeout.Token);
        Assert.Equal("RESULT,ABC,2", Encoding.UTF8.GetString(received));
        send = Parameters("00 5C 41 44"); send.DeviceId = device.Id; send.Hex = true;
        var sent = await Run(send, service, timeout.Token); Assert.True(sent.IsSuccess, sent.ErrorMessage);
        var binary = new byte[4]; await peer.GetStream().ReadExactlyAsync(binary, timeout.Token); Assert.Equal(new byte[] { 0, 92, 65, 68 }, binary);
        // Frozen plan must not submit after target configuration changes.
        var plan = await service.PrepareAsync(new("Device", device.Id, "", new[] { new CommunicationSendInput(Guid.NewGuid(), "", "X", "String") }, new()), timeout.Token);
        device.Name = "changed"; await Assert.ThrowsAsync<InvalidOperationException>(() => plan.SubmitAsync(timeout.Token));
    }
    [Fact]
    public async Task Real_modbus_direct_send_uses_fc16_and_configured_address()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var device = new Communication { Name = "PLC", ProtocolKey = "modbus", ModbusMode = "TcpClient", UnitId = 7, Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        var point = new ModbusAddressPoint { StartAddress = 40, Count = 1, IntBits = "16bit", PollEnabled = false }; device.ModbusAddressPoints.Add(point);
        var solution = new Solution { Communications = new() { device } }; await using var runtime = new CommunicationRuntimeManager();
        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token); using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        var service = new WorkflowCommunicationSendService(() => solution, runtime);
        var p = Parameters("__GlobalVars__.number"); p.DeviceId = device.Id; p.Rows[0].TargetId = point.Id;
        p.Rows[0].DataType = "Integer";
        var pending = Run(p, service, timeout.Token, new Dictionary<string, object?> { ["number"] = 92L });
        var header = new byte[7]; await peer.GetStream().ReadExactlyAsync(header, timeout.Token);
        var request = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1]; await peer.GetStream().ReadExactlyAsync(request, timeout.Token);
        Assert.Equal(16, request[0]); Assert.Equal(40, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(1, 2))); Assert.Equal(new byte[] { 0, 92 }, request[6..]);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 6); await peer.GetStream().WriteAsync(header.Concat(request.Take(5)).ToArray(), timeout.Token);
        var result = await pending; Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Single(result.Payloads);
    }
    [Fact]
    public async Task Send_event_requires_all_explicit_parameters_and_never_uses_test_content()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var device = new Communication { Name = "TCP", ProtocolKey = "tcp-client", Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        var field = new CommunicationSendParameterDefinition { Name = "code", DataType = "string", Content = "TEST" };
        var definition = new CommunicationSendEventDefinition { DeviceId = device.Id, Parameters = new() { field }, Separator = "", Terminator = "\n" };
        var solution = new Solution { Communications = new() { device }, CommunicationSendEvents = new() { definition } };
        await using var runtime = new CommunicationRuntimeManager(); await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token); var service = new WorkflowCommunicationSendService(() => solution, runtime);
        var p = Parameters("CURRENT"); p.DestinationKind = "Event"; p.EventId = definition.EventId.ToString(); p.Rows[0].EventFieldId = field.FieldId;
        var result = await Run(p, service, timeout.Token); Assert.True(result.IsSuccess, result.ErrorMessage);
        var bytes = new byte[8]; await peer.GetStream().ReadExactlyAsync(bytes, timeout.Token); Assert.Equal("CURRENT\n", Encoding.UTF8.GetString(bytes));
        p.Rows[0].EventFieldId = Guid.NewGuid(); Assert.False((await Run(p, service, timeout.Token)).IsSuccess);
        Assert.False(peer.GetStream().DataAvailable);
    }
    [Fact]
    public async Task Variables_register_single_producer_and_publish_without_changing_design_value()
    {
        var engine = new WorkflowEngine(VisionLogger.Instance); var flow = engine.CreateWorkflow(Guid.NewGuid().ToString(), "variables");
        var p = Parameters("CURRENT"); p.DestinationKind = "Variable"; p.Rows[0].Name = "out0";
        var variable = new GlobalVariable("code", "DESIGN", "String"); p.Rows[0].TargetId = variable.Id;
        var sender = new WorkflowNodeBase(Guid.NewGuid().ToString(), "发送", "发送", "SendData") { Parameters = p }; flow.Nodes.Add(sender);
        var receive = new ReceiveDataParameters { SourceKind = "Variable", Variables = new() { new() { VariableId = variable.Id } } };
        var receiver = new WorkflowNodeBase(Guid.NewGuid().ToString(), "接收", "接收", "ReceiveData") { Parameters = receive }; flow.Nodes.Insert(0, receiver);
        var solution = new Solution { Workflows = new() { flow }, GlobalVariables = new() { variable } }; await using var runtime = new CommunicationRuntimeManager();
        var service = new WorkflowCommunicationSendService(() => solution, runtime);
        service.ConfigureVariablePublications(sender.Id, new[] { new SendVariablePublication(variable.Id, p.VariableFields()[0].BindingKey, "String") });
        Assert.Equal(sender.Id, variable.ProducerBinding!.NodeId);
        var reader = new CommunicationReceiveService { VariableCatalog = () => new[] { new ReceiveVariableSource(variable.Id, variable.Name, variable.Group, variable.Type, true, sender.Id, p.VariableFields()[0].BindingKey, flow.Id) } };
        ToolRegistry.RegisterTool(typeof(SendDataTool)); ToolRegistry.RegisterTool(typeof(ReceiveDataTool));
        var runner = new WorkflowExecutionEngine(engine, new PluginManager(VisionLogger.Instance), VisionLogger.Instance);
        var result = await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id, communicationInputs: reader, communicationOutputs: service));
        Assert.True(result.Success, string.Join(";", result.Errors)); Assert.Equal("DESIGN", variable.Value);
        var output = result.NodeResults[receiver.Id].Result!.GetOutputVariables().Single(v => v.Name == receive.Variables[0].Definition().BindingKey);
        Assert.Equal("CURRENT", Assert.Single(Assert.IsType<string[]>(output.RuntimeValue)));
        variable.ProducerBinding = new() { WorkflowId = flow.Id, NodeId = "other", ResultName = "x" };
        Assert.Throws<InvalidOperationException>(() => service.ConfigureVariablePublications(sender.Id, new[] { new SendVariablePublication(variable.Id, p.VariableFields()[0].BindingKey, "String") }));
    }
    [Fact]
    public void Editor_preserves_saved_rows_and_adapts_both_themes_with_add_after_grid()
    {
        RenderEditor();
    }

    [Theory]
    [InlineData("Integer", "0")]
    [InlineData("Double", "1.25")]
    [InlineData("Boolean", "false")]
    public async Task Variable_literals_follow_target_type_without_treating_zero_or_false_as_missing(string type, string text)
    {
        var p = Parameters(text); p.DestinationKind = "Variable"; p.Rows[0].DataType = type;
        var variable = new GlobalVariable("value", type == "Boolean" ? true : type == "Double" ? 9d : 9L, type);
        p.Rows[0].TargetId = variable.Id;
        var flow = new SunEyeVision.Workflow.Workflow("flow", "values"); flow.Nodes.Add(new WorkflowNodeBase("sender", "send", "send", "SendData") { Parameters = p });
        var solution = new Solution { Workflows = new() { flow }, GlobalVariables = new() { variable } };
        await using var runtime = new CommunicationRuntimeManager(); var service = new WorkflowCommunicationSendService(() => solution, runtime);
        service.ConfigureVariablePublications("sender", new[] { new SendVariablePublication(variable.Id, p.VariableFields()[0].BindingKey, type) });
        var result = await Run(p, service, node: "sender"); Assert.True(result.IsSuccess, result.ErrorMessage);
        var output = Assert.IsAssignableFrom<Array>(result.GetOutputVariables().Single(v => v.Name == p.VariableFields()[0].BindingKey).RuntimeValue);
        Assert.Equal(1, output.Length);
        Assert.Equal(text, Convert.ToString(output.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!.ToLowerInvariant());
    }

    private static void RenderEditor()
    {
        Exception? failure = null; var thread = new Thread(() =>
        {
            try
            {
                var editor = new SendDataEditor(); var p = Parameters("ABC", "2"); p.DestinationKind = "Variable";
                var id = p.Rows[0].FieldId; editor.SetParameters(p); Assert.Equal(2, p.Rows.Count); Assert.Equal(id, p.Rows[0].FieldId);
                Assert.Equal(new Size(520, 600), editor.PreferredDebugWindowSize);
                ResourceDictionary? previous = null;
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    if (previous != null) editor.Resources.MergedDictionaries.Remove(previous);
                    previous = new() { Source = new Uri($"/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml", UriKind.Relative) }; editor.Resources.MergedDictionaries.Add(previous);
                    editor.Measure(new Size(520, 514)); editor.Arrange(new Rect(0, 0, 520, 514)); editor.UpdateLayout();
                    Assert.Equal(2, p.Rows.Count); Assert.Equal(id, p.Rows[0].FieldId);
                    var grid = (DataGrid)editor.FindName("RowsGrid"); var add = (Button)editor.FindName("AddButton");
                    Assert.Contains(grid.Columns, c => Equals(c.Header, "发送类型"));
                    Assert.True(add.TranslatePoint(new Point(), editor).Y >= grid.TranslatePoint(new Point(), editor).Y + grid.ActualHeight);
                    Assert.True(grid.ActualWidth > 400);
                    var root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "SunEyeVision.slnx"))) root = Directory.GetParent(root)!.FullName;
                    var directory = Path.Combine(root, "artifacts", "validation", "send-types-20261013"); Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap(520, 514, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(editor);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(directory, $"editor-{theme.ToLowerInvariant()}.png")); encoder.Save(file);
                }
            }
            catch (Exception e) { failure = e; }
        }); thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
