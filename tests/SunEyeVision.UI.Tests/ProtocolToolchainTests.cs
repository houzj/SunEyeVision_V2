using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Tool.ProtocolAssemble;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Core.Services.Serialization;
using Xunit;

namespace SunEyeVision.UI.Tests;

// Match application startup: register plugin parameter types before any test
// initializes/caches workflow serializer metadata. This is process-wide setup,
// not a mid-test mutation competing with other collections' serialization.
internal static class ProtocolTestParameterRegistration
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        ParameterTypeRegistry.RegisterParameterType(typeof(ProtocolParseParameters), "ProtocolParseParameters");
        ParameterTypeRegistry.RegisterParameterType(typeof(ProtocolAssembleParameters), "ProtocolAssembleParameters");
        ParameterTypeRegistry.RegisterParameterType(typeof(SunEyeVision.Tool.ReceiveData.ReceiveDataParameters), "ReceiveDataParameters");
    }
}

public sealed class ProtocolToolchainTests
{
    private static object? Value(CommunicationToolResults result, CommunicationOutputField field) =>
        result.GetOutputVariables().Single(v => v.Name == field.BindingKey).RuntimeValue;
    private static ProtocolParseParameters Text(string input, params string[] types) => new()
    {
        InputText = input,
        Outputs = new ObservableCollection<ProtocolParseField>(types.Select((t, i) => new ProtocolParseField { Name = "out" + i, DataType = t }))
    };
    private static ProtocolParseParameters Bytes(string input, string type = "int", int start = 0, int end = 1) => new()
    {
        Mode = "Bytes", InputText = input,
        Outputs = new() { new() { DataType = type, Start = start, End = end } }
    };

    [Fact]
    public void Assemble_preserves_order_empty_parts_and_real_utf8_bytes()
    {
        var p = new ProtocolAssembleParameters { Separator = "|", Parts = new() { new() { InputText = "0001" }, new() { InputText = "" }, new() { InputText = "中" } } };
        var r = new ProtocolAssembleTool().Run(p);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal("0001||中", Assert.Single(Assert.IsType<string[]>(Value(r, ProtocolAssembleParameters.TextOutput))));
        Assert.Equal("303030317C7CE4B8AD", Assert.Single(Assert.IsType<BinaryPayload[]>(Value(r, ProtocolAssembleParameters.BytesOutput))).ToString());
    }

    [Fact]
    public void Text_consumes_only_configured_fields_and_preserves_empty_string()
    {
        var p = Text("12,,1.5,unused", "int", "string", "float");
        var r = new ProtocolParseTool().Run(p);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(12L, Assert.Single(Assert.IsType<long[]>(Value(r, p.Outputs[0].OutputDefinition()))));
        Assert.Equal("", Assert.Single(Assert.IsType<string[]>(Value(r, p.Outputs[1].OutputDefinition()))));
        Assert.Equal(1.5, Assert.Single(Assert.IsType<double[]>(Value(r, p.Outputs[2].OutputDefinition()))));
        Assert.Contains(r.Analysis, a => a.Name == "未配置片段");
    }

    [Theory]
    [InlineData("1", "int", "int")]
    [InlineData("1,bad", "int", "int")]
    [InlineData("1,NaN", "int", "float")]
    [InlineData("1,2147483648", "int", "int")]
    public void Text_failure_keeps_schema_without_partial_values(string input, string first, string second)
    {
        var p = Text(input, first, second); var r = new ProtocolParseTool().Run(p);
        Assert.False(r.IsSuccess);
        Assert.All(p.Outputs, f => Assert.Null(Value(r, f.OutputDefinition())));
        Assert.Contains(r.Analysis, a => !a.IsValid);
    }

    [Fact]
    public void Stable_binding_connects_assemble_to_parse_and_survives_rename()
    {
        var upstream = new ProtocolAssembleTool().Run(new() { Parts = new() { new() { InputText = "42" }, new() { InputText = "ok" } } });
        var node = Guid.NewGuid().ToString();
        var p = Text(node + "." + ProtocolAssembleParameters.BytesOutput.BindingKey, "int", "string");
        p.NodeResults = new Dictionary<string, ToolResults> { [node] = upstream };
        var key = p.Outputs[0].OutputDefinition().BindingKey; var notifications = 0;
        p.OutputContractChanged += () => notifications++;
        p.Outputs[0].Name = "编号";
        var r = new ProtocolParseTool().Run(p);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(key, p.Outputs[0].OutputDefinition().BindingKey);
        Assert.True(notifications > 0);
        Assert.Equal(42L, Assert.Single(Assert.IsType<long[]>(Value(r, p.Outputs[0].OutputDefinition()))));
        Assert.Equal(p.GetOutputContract().Select(v => v.Name), r.GetOutputVariables().Select(v => v.Name));
        upstream.SetError("本轮上游失败");
        Assert.False(new ProtocolParseTool().Run(p).IsSuccess);
    }

    [Theory]
    [InlineData("FFFF", false, 65535)]
    [InlineData("FFFF", true, -1)]
    [InlineData("0001", false, 1)]
    public void Binary_integer_obeys_signed_configuration(string input, bool signed, long expected)
    {
        var p = Bytes(input); p.Outputs[0].NumericInterpretation = signed ? "Signed" : "Unsigned";
        var r = new ProtocolParseTool().Run(p);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(expected, Assert.Single(Assert.IsType<long[]>(Value(r, p.Outputs[0].OutputDefinition()))));
    }

    [Theory]
    [InlineData("3F800000", "ABCD")]
    [InlineData("803F0000", "BADC")]
    [InlineData("00003F80", "CDAB")]
    [InlineData("0000803F", "DCBA")]
    public void Float_orders_decode_same_value_without_mutating_analysis_source(string input, string order)
    {
        var p = Bytes(input, "float", 0, 3); p.Outputs[0].Order = order;
        var r = new ProtocolParseTool().Run(p);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(1d, Assert.Single(Assert.IsType<double[]>(Value(r, p.Outputs[0].OutputDefinition()))));
        Assert.Equal(input, Assert.Single(r.Analysis).Source);
    }

    [Fact]
    public void Ascii_and_binary_are_explicit_distinct_rules_and_index_zero_is_closed()
    {
        var p = Bytes("3132", "int", 0, 1);
        var binary = new ProtocolParseTool().Run(p);
        Assert.Equal(12594L, Assert.Single(Assert.IsType<long[]>(Value(binary, p.Outputs[0].OutputDefinition()))));
        p.AsciiData = true;
        var ascii = new ProtocolParseTool().Run(p);
        Assert.Equal(12L, Assert.Single(Assert.IsType<long[]>(Value(ascii, p.Outputs[0].OutputDefinition()))));
    }

    [Theory]
    [InlineData("GG", "int", 0, 1, "ABCD")]
    [InlineData("0001", "int", 0, 2, "ABCD")]
    [InlineData("FFFFFF", "int", 0, 2, "CDAB")]
    [InlineData("FF", "string", 0, 0, "ABCD")]
    [InlineData("7F800000", "float", 0, 3, "ABCD")]
    public void Invalid_bytes_fail_without_outputs(string input, string type, int start, int end, string order)
    {
        var p = Bytes(input, type, start, end); p.Outputs[0].Order = order;
        var r = new ProtocolParseTool().Run(p);
        Assert.False(r.IsSuccess); Assert.Null(Value(r, p.Outputs[0].OutputDefinition()));
    }

    [Fact]
    public void Default_four_byte_int_is_signed_and_explicit_unsigned_overflow_fails()
    {
        var p = Bytes("0xFF,FF:FF FF", "int", 0, 3);
        var result = new ProtocolParseTool().Run(p);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(-1L, Assert.Single(Assert.IsType<long[]>(Value(result, p.Outputs[0].OutputDefinition()))));
        p.Outputs[0].NumericInterpretation = "Unsigned";
        Assert.False(new ProtocolParseTool().Run(p).IsSuccess);
    }

    [Fact]
    public void Parameter_json_preserves_stable_field_identity_and_custom_separator()
    {
        var p = Text("1::ok", "int", "string"); p.Separator = "::";
        var copy = JsonSerializer.Deserialize<ProtocolParseParameters>(JsonSerializer.Serialize(p))!;
        Assert.Equal(p.Outputs.Select(f => f.FieldId), copy.Outputs.Select(f => f.FieldId));
        Assert.True(new ProtocolParseTool().Run(copy).IsSuccess);
    }

    [Fact]
    public void Binding_rejects_missing_and_multi_value_inputs_without_guessing()
    {
        Assert.Throws<InvalidOperationException>(() => ProtocolBindingInput.Resolve("__GlobalVars__.absent", null, null));
        Assert.Throws<FormatException>(() => ProtocolBindingInput.Resolve("__GlobalVars__.many", null, new Dictionary<string, object?> { ["many"] = new long[] { 1, 2 } }));
        Assert.Equal(7L, ProtocolBindingInput.Resolve("__GlobalVars__.one", null, new Dictionary<string, object?> { ["one"] = new long[] { 7 } }));
        Assert.Equal("0001", ProtocolBindingInput.Resolve("0001", null, null));
    }

    [Fact]
    public void Host_executor_injects_current_globals_and_nodes_into_plugin_chain()
    {
        var executor = new ToolExecutor(new ParameterResolver());
        var p = new ProtocolAssembleParameters { Parts = new() { new() { InputText = "__GlobalVars__.number" } } };
        var assembled = executor.Run(new ProtocolAssembleTool(), new ToolExecutionRequest(p)
        { GlobalVariables = new Dictionary<string, object?> { ["number"] = new long[] { 27 } } });
        Assert.True(assembled.IsSuccess, assembled.ErrorMessage);
        var id = Guid.NewGuid().ToString();
        var parse = Text(id + "." + ProtocolAssembleParameters.TextOutput.BindingKey, "int");
        var parsed = executor.Run(new ProtocolParseTool(), new ToolExecutionRequest(parse)
        { NodeResults = new Dictionary<string, ToolResults> { [id] = assembled } });
        Assert.True(parsed.IsSuccess, parsed.ErrorMessage);
        Assert.Equal(27L, Assert.Single(Assert.IsType<long[]>(Value(parsed, parse.Outputs[0].OutputDefinition()))));
    }

    [Fact]
    public void Both_parameter_types_roundtrip_through_host_polymorphic_registry()
    {
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(typeof(ProtocolParseTool));
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(typeof(ProtocolAssembleTool));
        Assert.IsType<ProtocolParseTool>(SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.CreateToolInstance("ProtocolParse"));
        Assert.IsType<ProtocolAssembleTool>(SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.CreateToolInstance("ProtocolAssemble"));
        Assert.Contains(((IToolPlugin)new ProtocolAssembleTool()).OutputVariables, v => v.Name == ProtocolAssembleParameters.BytesOutput.BindingKey);
        var options = ParameterTypeRegistry.SerializationOptions;
        ToolParameters p = Text("9", "int");
        var parsed = Assert.IsType<ProtocolParseParameters>(JsonSerializer.Deserialize<ToolParameters>(JsonSerializer.Serialize(p, options), options));
        Assert.Equal(((ProtocolParseParameters)p).Outputs[0].FieldId, parsed.Outputs[0].FieldId);
        ToolParameters a = new ProtocolAssembleParameters { Separator = "::" };
        var assembly = Assert.IsType<ProtocolAssembleParameters>(JsonSerializer.Deserialize<ToolParameters>(JsonSerializer.Serialize(a, options), options));
        Assert.Equal("::", assembly.Separator);
        var copiedOptions = new JsonSerializerOptions(options);
        Assert.IsType<ProtocolParseParameters>(JsonSerializer.Deserialize<ToolParameters>(JsonSerializer.Serialize(p, copiedOptions), copiedOptions));
        Assert.IsType<ProtocolAssembleParameters>(JsonSerializer.Deserialize<ToolParameters>(JsonSerializer.Serialize(a, SunEyeVision.Workflow.WorkflowSerializationOptions.Default), SunEyeVision.Workflow.WorkflowSerializationOptions.Default));
    }

    [Fact]
    public void Cancellation_and_duplicate_names_never_publish_values()
    {
        var p = Text("1,2", "int", "int");
        p.SetExecutionContext(new SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext { CancellationToken = new CancellationToken(true) });
        var r = new ProtocolParseTool().Run(p);
        Assert.Equal(ExecutionStatus.Cancelled, r.Status);
        Assert.All(p.Outputs, f => Assert.Null(Value(r, f.OutputDefinition())));
        p.Outputs[1].Name = p.Outputs[0].Name;
        Assert.False(new ProtocolParseTool().Run(p).IsSuccess);
    }

    [Fact]
    public void Editors_keep_custom_separator_and_show_only_configuration()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var p = Text("1", "int"); p.Separator = "::";
                var editor = new SunEyeVision.Tool.ProtocolParse.Views.ProtocolParseEditor(); editor.SetParameters(p);
                Assert.Equal("::", p.Separator);
                p.Mode = "Bytes";
                Assert.Equal(Visibility.Visible, Assert.IsType<StackPanel>(editor.FindName("ByteSettings")).Visibility);
                p.Separator = "@@";
                Assert.Equal("@@", Assert.IsType<ComboBox>(editor.FindName("DelimiterOptions")).SelectedValue);
                var assembly = new ProtocolAssembleParameters { Separator = "::" };
                var other = new SunEyeVision.Tool.ProtocolAssemble.Views.ProtocolAssembleEditor(); other.SetParameters(assembly);
                Assert.Equal("::", assembly.Separator);
                Assert.NotNull(new ProtocolParseTool().CreateDataAnalysisControl());
                Assert.NotNull(new ProtocolAssembleTool().CreateDataAnalysisControl());
                editor.Measure(new Size(820, 680)); editor.Arrange(new Rect(0, 0, 820, 680));
                other.Measure(new Size(820, 680)); other.Arrange(new Rect(0, 0, 820, 680));
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)), "插件编辑器测试超时。");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
