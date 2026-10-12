using System;
using System.Text;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationProtocolOptimizationTests
{
    [Fact]
    public void Official_tcp_example_converts_text_fields_atomically()
    {
        var state = new CommunicationReceiveEventViewModel();
        state.Outputs.Add(new() { Name = "out1", DataType = "string" });
        state.Outputs.Add(new() { Name = "out2", DataType = "int" });
        Assert.True(CommunicationReceiveProcessor.Process(state, Encoding.UTF8.GetBytes("ABC,2"), DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal("ABC", state.Outputs[0].Value); Assert.Equal(2, state.Outputs[1].Value);
        Assert.False(CommunicationReceiveProcessor.Process(state, Encoding.UTF8.GetBytes("ABC,bad"), DateTimeOffset.UtcNow).IsSuccess);
        Assert.All(state.Outputs, field => Assert.Null(field.Value));
    }

    [Fact]
    public void Modbus_mixed_fields_produce_native_values_and_real_binary_output()
    {
        var state = ByteEvent();
        var number = Field("int", 0, 1);
        var text = Field("string", 2, 3);
        var binary = Field("Binary", 0, 3);
        state.CompareInputs.Add(number); state.CompareInputs.Add(text); state.CompareInputs.Add(binary);
        var output = new CommunicationAssemblyOutputViewModel { DataType = "Binary", SourceFieldId = binary.FieldId };
        state.AssemblyOutputs.Add(output);
        Assert.True(CommunicationReceiveProcessor.ProcessModbus(state, [0x005C, 0x4144], DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(92, number.Value); Assert.Equal("AD", text.Value);
        Assert.Equal("005C4144", Assert.IsType<BinaryPayload>(binary.Value).ToString());
        Assert.Same(binary.Value, output.Value);
        text.ByteEnd = 4;
        Assert.False(CommunicationReceiveProcessor.ProcessModbus(state, [0x005C, 0x4144], DateTimeOffset.UtcNow).IsSuccess);
        Assert.All(state.CompareInputs, field => Assert.Null(field.Value)); Assert.Null(output.Value);
    }

    [Fact]
    public void Raw_byte_event_supports_float_and_binary_comparison()
    {
        var state = ByteEvent();
        state.CompareInputs.Add(Field("float", 0, 3));
        var binary = Field("Binary", 0, 3); binary.CompareRule = "等于"; binary.CompareValue = "0x3F,80:00 00";
        state.CompareInputs.Add(binary);
        Assert.True(CommunicationReceiveProcessor.Process(state, Convert.FromHexString("3F800000"), DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(1f, state.CompareInputs[0].Value);
        Assert.False(binary.IsNumericRange);
        Assert.DoesNotContain("范围内", binary.CompareRuleOptions);
    }

    [Fact]
    public void Event_ascii_validates_input_without_becoming_module_ascii_numeric_conversion()
    {
        var state = ByteEvent(); state.AsciiData = true;
        state.CompareInputs.Add(Field("int", 0, 0));
        Assert.True(CommunicationReceiveProcessor.Process(state, [0x31], DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(49, state.CompareInputs[0].Value);
        Assert.Equal(1, ProtocolValueCodec.Decode([0x31], 0, 0, "int", ascii: true));
        Assert.False(CommunicationReceiveProcessor.Process(state, [0xFF], DateTimeOffset.UtcNow).IsSuccess);
    }

    private static CommunicationReceiveEventViewModel ByteEvent() => new()
    { ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch, EventType = CommunicationEventType.ProtocolCompare };
    private static CommunicationCompareInputViewModel Field(string type, int start, int end) => new()
    { Name = type, DataType = type, IsByteRule = true, ByteStart = start, ByteEnd = end };
}
