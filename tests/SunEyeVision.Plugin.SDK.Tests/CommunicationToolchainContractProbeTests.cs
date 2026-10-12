using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>P0 contract probe only; it does not register a production communication tool.</summary>
public sealed class CommunicationToolchainContractProbeTests
{
    [Fact]
    public void Configured_field_key_survives_display_rename_and_reordering()
    {
        var first = new ProbeField(Guid.NewGuid(), "out0", "Integer");
        var second = new ProbeField(Guid.NewGuid(), "out1", "String");
        var result = new ProbeResult(new[] { first, second }, new()
        {
            [first.Id] = 92L, [second.Id] = "AD"
        });
        var originalKey = result.GetOutputVariables()[0].Name;
        var renamed = first with { DisplayName = "产品编号" };
        var reordered = new ProbeResult(new[] { second, renamed }, new()
        {
            [first.Id] = 92L, [second.Id] = "AD"
        });
        Assert.Equal(originalKey, reordered.GetOutputVariables()[1].Name);
        Assert.Equal("产品编号", reordered.GetOutputVariables()[1].DisplayName);
        Assert.Equal(92L, Assert.IsType<long>(OutputVariableResolver.Resolve(reordered, originalKey)));
    }

    [Fact]
    public void Missing_current_value_keeps_schema_without_reusing_previous_value()
    {
        var field = new ProbeField(Guid.NewGuid(), "out0", "Integer");
        var success = new ProbeResult(new[] { field }, new() { [field.Id] = 92L });
        var failure = new ProbeResult(new[] { field }, new());
        var key = Assert.Single(success.GetOutputVariables()).Name;
        Assert.Null(Assert.Single(failure.GetOutputVariables()).RuntimeValue);
        Assert.Null(OutputVariableResolver.Resolve(failure, key));
        Assert.Equal(92L, Assert.IsType<long>(OutputVariableResolver.Resolve(success, key)));
    }

    [Fact]
    public void Scalar_and_list_follow_existing_sdk_tuple_contract()
    {
        var field = new ProbeField(Guid.NewGuid(), "out0", "Integer");
        var scalar = new ProbeResult(new[] { field }, new() { [field.Id] = 1L });
        var list = new ProbeResult(new[] { field }, new() { [field.Id] = new long[] { 1, 2 } });
        var key = Assert.Single(scalar.GetOutputVariables()).Name;
        Assert.Equal(new long[] { 1 }, Assert.IsType<long[]>(scalar.GetOutputVariables()[0].RuntimeValue));
        Assert.Equal(1L, Assert.IsType<long>(OutputVariableResolver.Resolve(scalar, key)));
        Assert.Equal(new long[] { 1, 2 }, Assert.IsType<long[]>(OutputVariableResolver.Resolve(list, key)));
    }

    [Theory]
    [InlineData(typeof(int), "Integer")]
    [InlineData(typeof(float), "Double")]
    [InlineData(typeof(string), "String")]
    public void Protocol_clr_types_use_existing_canonical_vocabulary(Type type, string expected)
        => Assert.Equal(expected, SdkTypeVocabulary.ToTypeVocabulary(type));

    [Fact]
    public void Raw_byte_arrays_do_not_restore_Byte_scalar_or_silently_register_unknown_types()
    {
        // Whole payloads use BinaryPayload; raw numeric Byte is intentionally not restored.
        Assert.Null(SdkTypeVocabulary.ToTypeVocabulary(typeof(byte[])));
        Assert.Null(SdkTypeVocabulary.ToSystemType("Byte"));
        Assert.Throws<ArgumentException>(() => SdkTypeVocabulary.RegisterType(typeof(byte[]), "CommunicationBytes"));
    }

    private sealed record ProbeField(Guid Id, string DisplayName, string TypeName);

    private sealed class ProbeResult : ToolResults
    {
        private readonly IReadOnlyList<ProbeField> _fields;
        private readonly Dictionary<Guid, object?> _values;

        public ProbeResult(IReadOnlyList<ProbeField> fields, Dictionary<Guid, object?> values)
        {
            _fields = fields.ToArray();
            _values = values.ToDictionary(pair => pair.Key, pair => pair.Value is long[] array
                ? (object)(long[])array.Clone() : pair.Value);
        }

        public override IReadOnlyList<OutputVariable> GetOutputVariables() => _fields.Select(field =>
        {
            var type = SdkTypeVocabulary.ToSystemType(field.TypeName)
                ?? throw new InvalidOperationException("输出类型尚未注册。");
            _values.TryGetValue(field.Id, out var value);
            return new OutputVariable
            {
                Name = "Field_" + field.Id.ToString("N"), DisplayName = field.DisplayName,
                VariableType = field.TypeName, Kind = OutputVariableKind.Data,
                RuntimeValue = OutputVariableArrayPromoter.Promote(value, field.TypeName, type)
            };
        }).ToArray();
    }
}
