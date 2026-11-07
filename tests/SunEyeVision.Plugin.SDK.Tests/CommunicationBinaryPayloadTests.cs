using SunEyeVision.Plugin.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Results;
using SunEyeVision.Plugin.Editing.Wpf.Models;
using SunEyeVision.Plugin.Editing.Wpf.Models.Communication;
using Xunit;

namespace SunEyeVision.Plugin.Editing.Wpf.Tests;

public sealed class CommunicationBinaryPayloadTests
{
    [Fact]
    public void Payload_owns_input_and_returns_only_owned_copies()
    {
        var source = new byte[] { 0, 92 };
        var payload = new BinaryValue(source);
        source[1] = 0;
        var copy = payload.ToArray(); copy[0] = 255;
        Assert.Equal(new byte[] { 0, 92 }, payload.ToArray());
        Assert.Equal("005C", payload.ToString());
    }

    [Fact]
    public void Binary_is_an_independent_sequence_not_restored_Byte_scalar()
    {
        Assert.Equal("Binary", SdkTypeVocabulary.ToTypeVocabulary(typeof(BinaryValue)));
        Assert.Equal("Binary", SdkTypeVocabulary.ToTypeVocabulary(typeof(BinaryValue[])));
        Assert.Equal(typeof(BinaryValue), SdkTypeVocabulary.ToSystemType("Binary"));
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(byte)));
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(byte[])));
        Assert.Null(SdkTypeVocabulary.ToSystemType("Byte"));
        Assert.Equal("字节序列", SdkTypeVocabulary.GetChineseName("Binary"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void Result_contract_preserves_payload_boundaries_including_single_byte(int length)
    {
        var payload = new BinaryValue(Enumerable.Range(0, length).Select(i => (byte)i).ToArray());
        var result = new PayloadResult { Data = payload };
        var variable = result.GetOutputVariables().Single(v => v.Name == "Data");
        Assert.Equal("Binary", variable.VariableType);
        Assert.Same(payload, Assert.Single(Assert.IsType<BinaryValue[]>(variable.RuntimeValue)));
        Assert.Same(payload, Assert.IsType<BinaryValue>(OutputVariableResolver.Resolve(result, "Data")));
        Assert.Same(payload, Assert.Single(Assert.IsType<BinaryValue[]>(OutputVariableResolver.Resolve(result, "Data[]"))));
    }

    [Fact]
    public void Missing_payload_keeps_output_schema_with_no_value()
    {
        var result = new PayloadResult();
        Assert.Null(result.GetOutputVariables().Single(v => v.Name == "Data").RuntimeValue);
        Assert.Null(OutputVariableResolver.Resolve(result, "Data"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void Global_variable_round_trip_preserves_exact_bytes_and_tuple_shape(int length)
    {
        var data = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        using var codec = new VariableCodecScope(typeof(Holder));
        var json = JsonSerializer.Serialize(new Holder { Value = new BinaryValue(data) }, codec.Options);
        Assert.Contains("\"Type\":\"Binary\"", json);
        var loaded = JsonSerializer.Deserialize<Holder>(json, codec.Options)!;
        Assert.Equal(data, Assert.IsType<BinaryValue>(loaded.Value).ToArray());
    }

    [Fact]
    public void Multiple_payloads_remain_separate_when_persisted()
    {
        var values = new[] { new BinaryValue(new byte[] { 1 }), new BinaryValue(new byte[] { 2, 3 }) };
        using var codec = new VariableCodecScope(typeof(Holder));
        var loaded = JsonSerializer.Deserialize<Holder>(JsonSerializer.Serialize(new Holder { Value = values }, codec.Options), codec.Options)!;
        var restored = Assert.IsType<BinaryValue[]>(loaded.Value);
        Assert.Equal(2, restored.Length);
        Assert.Equal(new byte[] { 1 }, restored[0].ToArray());
        Assert.Equal(new byte[] { 2, 3 }, restored[1].ToArray());
    }

    [Fact]
    public void Malformed_persisted_payload_does_not_become_an_empty_success_value()
    {
        using var codec = new VariableCodecScope(typeof(Holder));
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Holder>(
            "{\"Value\":{\"Type\":\"Binary\",\"Value\":[\"not base64!\"]}}", codec.Options));
        Assert.IsType<FormatException>(error.InnerException);
        Assert.Contains("Binary", error.Message);
    }

    private sealed class PayloadResult : ToolResults
    {
        public BinaryValue? Data { get; init; }
        public override List<PropertyMapEntry> GetPropertyMap() => new() { new(nameof(Data), "原始数据") };
    }

    public sealed class Holder
    {
        public object? Value { get; set; }
    }
}

