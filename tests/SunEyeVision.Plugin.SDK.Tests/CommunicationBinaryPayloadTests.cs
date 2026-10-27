using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class CommunicationBinaryPayloadTests
{
    [Fact]
    public void Binary_contract_uses_current_development_baseline_without_legacy_support()
    {
        Assert.Equal("3.0", SunEyeVision.Plugin.SDK.Core.ToolContract.Version);
        Assert.True(SunEyeVision.Plugin.SDK.Core.ToolContract.IsCompatible(new Version(3, 0)));
        Assert.False(SunEyeVision.Plugin.SDK.Core.ToolContract.IsCompatible(new Version(2, 0)));
        Assert.False(SunEyeVision.Plugin.SDK.Core.ToolContract.IsCompatible(new Version(2, 1)));
        Assert.False(SunEyeVision.Plugin.SDK.Core.ToolContract.IsCompatible(new Version(3, 1)));
        Assert.False(SunEyeVision.Plugin.SDK.Core.ToolContract.IsCompatible(null));
    }

    [Fact]
    public void Payload_owns_input_and_returns_only_owned_copies()
    {
        var source = new byte[] { 0, 92 };
        var payload = new BinaryPayload(source);
        source[1] = 0;
        var copy = payload.ToArray(); copy[0] = 255;
        Assert.Equal(new byte[] { 0, 92 }, payload.ToArray());
        Assert.Equal("005C", payload.ToString());
    }

    [Fact]
    public void Binary_is_an_independent_sequence_not_restored_Byte_scalar()
    {
        Assert.Equal("Binary", SdkTypeVocabulary.ToTypeVocabulary(typeof(BinaryPayload)));
        Assert.Equal("Binary", SdkTypeVocabulary.ToTypeVocabulary(typeof(BinaryPayload[])));
        Assert.Equal(typeof(BinaryPayload), SdkTypeVocabulary.ToSystemType("Binary"));
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
        var payload = new BinaryPayload(Enumerable.Range(0, length).Select(i => (byte)i).ToArray());
        var result = new PayloadResult { Data = payload };
        var variable = result.GetOutputVariables().Single(v => v.Name == "Data");
        Assert.Equal("Binary", variable.VariableType);
        Assert.Same(payload, Assert.Single(Assert.IsType<BinaryPayload[]>(variable.RuntimeValue)));
        Assert.Same(payload, Assert.IsType<BinaryPayload>(OutputVariableResolver.Resolve(result, "Data")));
        Assert.Same(payload, Assert.Single(Assert.IsType<BinaryPayload[]>(OutputVariableResolver.Resolve(result, "Data[]"))));
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
        var json = JsonSerializer.Serialize(new Holder { Value = new BinaryPayload(data) });
        Assert.Contains("\"Type\":\"Binary\"", json);
        var loaded = JsonSerializer.Deserialize<Holder>(json)!;
        Assert.Equal(data, Assert.Single(Assert.IsType<BinaryPayload[]>(loaded.Value)).ToArray());
    }

    [Fact]
    public void Multiple_payloads_remain_separate_when_persisted()
    {
        var values = new[] { new BinaryPayload(new byte[] { 1 }), new BinaryPayload(new byte[] { 2, 3 }) };
        var loaded = JsonSerializer.Deserialize<Holder>(JsonSerializer.Serialize(new Holder { Value = values }))!;
        var restored = Assert.IsType<BinaryPayload[]>(loaded.Value);
        Assert.Equal(2, restored.Length);
        Assert.Equal(new byte[] { 1 }, restored[0].ToArray());
        Assert.Equal(new byte[] { 2, 3 }, restored[1].ToArray());
    }

    [Fact]
    public void Malformed_persisted_payload_does_not_become_an_empty_success_value()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Holder>(
            "{\"Value\":{\"Type\":\"Binary\",\"Value\":[\"not base64!\"]}}"));
        Assert.IsType<FormatException>(error.InnerException);
        Assert.Contains("Binary", error.Message);
    }

    private sealed class PayloadResult : ToolResults
    {
        public BinaryPayload? Data { get; init; }
        public override List<PropertyMapEntry> GetPropertyMap() => new() { new(nameof(Data), "原始数据") };
    }

    public sealed class Holder
    {
        [JsonConverter(typeof(GlobalVariableValueConverter))]
        public object? Value { get; set; }
    }
}
