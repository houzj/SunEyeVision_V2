using System;
using System.Text;
using Opc.Ua;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationReceiveProcessorTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Modbus_register_bytes_are_concatenated_as_hex_then_decoded_to_decimal_int()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "int", IsByteRule = true,
            ByteStart = 0, ByteEnd = 1, CompareRule = "等于", CompareValue = "92"
        });
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel
        {
            Name = "in2", DataType = "int", IsByteRule = true,
            ByteStart = 2, ByteEnd = 3, CompareRule = "范围", Minimum = 16700, Maximum = 16710
        });

        var result = CommunicationReceiveProcessor.ProcessModbus(definition, [0x005C, 0x4144], Timestamp);

        Assert.True(result.IsSuccess);
        Assert.Equal(92, definition.CompareInputs[0].Value);
        Assert.Equal("92", definition.CompareInputs[0].Result);
        Assert.Equal(16708, definition.CompareInputs[1].Value);
        Assert.Equal("OK", definition.CompareInputs[1].Status);
    }

    [Fact]
    public void Modbus_invalid_byte_range_rejects_without_leaving_old_results()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var field = new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "int", IsByteRule = true,
            ByteStart = 0, ByteEnd = 1, CompareRule = "不比较"
        };
        definition.CompareInputs.Add(field);
        Assert.True(CommunicationReceiveProcessor.ProcessModbus(definition, [0x005C], Timestamp).IsSuccess);

        field.ByteEnd = 2;
        var result = CommunicationReceiveProcessor.ProcessModbus(definition, [0x005C], Timestamp);

        Assert.False(result.IsSuccess);
        Assert.Null(field.Value);
        Assert.Equal(string.Empty, field.Result);
        Assert.Equal("NG", field.Status);
    }

    [Fact]
    public void Modbus_int_comparison_failure_keeps_decimal_result_but_sets_ng()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var field = new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, ByteStart = 0, ByteEnd = 1,
            CompareRule = "范围", Minimum = 0, Maximum = 1
        };
        definition.CompareInputs.Add(field);

        var result = CommunicationReceiveProcessor.ProcessModbus(definition, [0x005C], Timestamp);

        Assert.False(result.IsSuccess);
        Assert.Equal("92", field.Result);
        Assert.Equal("NG", field.Status);
    }

    [Fact]
    public void Modbus_four_byte_order_is_applied_before_hex_to_int_conversion()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var field = new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, ByteStart = 0, ByteEnd = 3,
            ByteOrder = "CDAB", CompareRule = "不比较"
        };
        definition.CompareInputs.Add(field);

        var result = CommunicationReceiveProcessor.ProcessModbus(definition, [0x0001, 0x0002], Timestamp);

        Assert.True(result.IsSuccess);
        Assert.Equal("131073", field.Result); // 00 02 00 01 -> 0x00020001
    }

    [Fact]
    public void Modbus_int_rule_recheck_uses_byte_rule_equality()
    {
        var field = new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, CompareRule = "等于",
            CompareValue = "92", Value = 92, Result = "92"
        };

        Assert.Equal("OK", CommunicationReceiveProcessor.GetCompareStatus(field));
        field.CompareValue = "93";
        Assert.Equal("NG", CommunicationReceiveProcessor.GetCompareStatus(field));
    }

    [Fact]
    public void Modbus_two_rules_use_their_own_visible_byte_intervals()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, ByteStart = 2, ByteEnd = 3,
            CompareRule = "不比较"
        });
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, ByteStart = 0, ByteEnd = 3,
            CompareRule = "不比较"
        });

        var outcome = CommunicationReceiveProcessor.ProcessModbus(definition, [0x0002, 0x0005], Timestamp);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("5", definition.CompareInputs[0].Result);
        Assert.Equal("131077", definition.CompareInputs[1].Result);
        Assert.Contains("2-3[0005]=5", definition.ParseHistoryText);
        Assert.Contains("0-3[00020005]=131077", definition.ParseHistoryText);
    }

    [Fact]
    public void Modbus_equal_without_matching_compare_value_is_ng()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var field = new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, ByteStart = 0, ByteEnd = 1,
            CompareRule = "等于", CompareValue = "0"
        };
        definition.CompareInputs.Add(field);

        var result = CommunicationReceiveProcessor.ProcessModbus(definition, [0x0005], Timestamp);

        Assert.False(result.IsSuccess);
        Assert.Equal("5", field.Result);
        Assert.Equal("NG", field.Status);
    }

    [Theory]
    [InlineData("范围内", 5, 0, 4, "NG")]
    [InlineData("范围外", 5, 0, 4, "OK")]
    [InlineData("范围内", 4, 0, 4, "OK")]
    [InlineData("范围外", 4, 0, 4, "NG")]
    public void Modbus_range_rules_use_inclusive_boundaries(string rule, ushort value, int min, int max, string expected)
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var field = new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, ByteStart = 0, ByteEnd = 1,
            CompareRule = rule, Minimum = min, Maximum = max
        };
        definition.CompareInputs.Add(field);

        CommunicationReceiveProcessor.ProcessModbus(definition, [value], Timestamp);

        Assert.Equal(expected, field.Status);
    }

    [Fact]
    public void Rising_and_falling_edges_only_match_a_zero_state_transition()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var field = new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, ByteStart = 0, ByteEnd = 1,
            CompareRule = "上升沿"
        };
        definition.CompareInputs.Add(field);

        CommunicationReceiveProcessor.ProcessModbus(definition, [0], Timestamp);
        Assert.Equal("NG", field.Status); // First sample establishes a baseline.
        CommunicationReceiveProcessor.ProcessModbus(definition, [5], Timestamp);
        Assert.Equal("OK", field.Status);
        CommunicationReceiveProcessor.ProcessModbus(definition, [5], Timestamp);
        Assert.Equal("NG", field.Status);
        field.CompareRule = "下降沿";
        CommunicationReceiveProcessor.ProcessModbus(definition, [0], Timestamp);
        Assert.Equal("OK", field.Status);
    }

    [Fact]
    public void Compare_rule_dropdown_options_follow_value_type_and_processing_method()
    {
        var field = new CommunicationCompareInputViewModel { DataType = "string" };
        Assert.Equal(new[] { "不比较", "等于", "不等于" }, field.CompareRuleOptions);

        field.DataType = "int";
        Assert.Equal(new[] { "不比较", "范围内", "范围外" }, field.CompareRuleOptions);

        field.IsByteRule = true;
        Assert.Equal(new[] { "不比较", "等于", "范围内", "范围外", "上升沿", "下降沿" }, field.CompareRuleOptions);
    }

    [Fact]
    public void Unsupported_saved_rule_never_silently_becomes_no_compare()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var field = new CommunicationCompareInputViewModel
        {
            DataType = "int", IsByteRule = true, CompareRule = "不等于",
            ByteStart = 0, ByteEnd = 1
        };
        definition.CompareInputs.Add(field);

        CommunicationReceiveProcessor.ProcessModbus(definition, [5], Timestamp);

        Assert.Equal("不等于", field.CompareRule);
        Assert.Equal("NG", field.Status);
    }

    [Fact]
    public void Text_parse_output_string_rule_controls_status_without_converting_the_field()
    {
        var definition = new CommunicationReceiveEventViewModel();
        var output = new CommunicationEventOutputViewModel
        {
            Name = "out1", DataType = "string", CompareRule = "等于", CompareValue = "OK"
        };
        definition.Outputs.Add(output);

        var first = Process(definition, "NG");
        Assert.False(first.IsSuccess);
        Assert.Equal("NG", output.Value);
        Assert.Equal("NG", output.Result);
        Assert.Equal("NG", output.Status);

        var second = Process(definition, "OK");
        Assert.True(second.IsSuccess);
        Assert.Equal("OK", output.Status);
        output.CompareRule = "不等于";
        Assert.Equal("NG", CommunicationReceiveProcessor.GetOutputStatus(output));
    }

    [Fact]
    public void Text_parse_numeric_range_rules_apply_to_typed_binary_values()
    {
        var definition = new CommunicationReceiveEventViewModel { DataMode = "Binary" };
        var output = new CommunicationEventOutputViewModel
        {
            Name = "out1", DataType = "Int16", CompareRule = "范围外", Minimum = 0, Maximum = 4
        };
        definition.Outputs.Add(output);

        var outcome = CommunicationReceiveProcessor.Process(definition, [0, 5], Timestamp);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("5", output.Result);
        Assert.Equal("OK", output.Status);
    }

    [Fact]
    public void Byte_match_ui_configuration_is_not_treated_as_an_implemented_parser()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel { Name = "in1", DataType = "int", IsByteRule = true });

        var result = CommunicationReceiveProcessor.Process(definition, new byte[] { 0, 0, 0, 4 }, Timestamp);

        Assert.False(result.IsSuccess);
        Assert.Contains("尚未接入运行时", result.FailureReason);
        Assert.Equal(string.Empty, definition.CompareInputs[0].Result);
        Assert.Equal("NG", definition.CompareInputs[0].Status);
    }

    [Fact]
    public void Length_check_uses_complete_text_and_separator_is_not_a_field()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            Separator = ",", CharacterLengthCompare = true, CharacterLength = 5
        };
        definition.Outputs.Add(new CommunicationEventOutputViewModel { Name = "out1", DataType = "string" });
        definition.Outputs.Add(new CommunicationEventOutputViewModel { Name = "out2", DataType = "string" });

        var result = Process(definition, "ABC,2");

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC", definition.Outputs[0].Value);
        Assert.Equal("2", definition.Outputs[1].Value);
        Assert.Equal("OK", definition.Outputs[1].Status);
        Assert.DoesNotContain(",,", definition.ParseHistoryText);
    }

    [Fact]
    public void Length_mismatch_rejects_entire_message_and_clears_previous_values()
    {
        var definition = ParseTwoStrings();
        Assert.True(Process(definition, "1,2").IsSuccess);
        definition.CharacterLengthCompare = true;
        definition.CharacterLength = 4;

        var result = Process(definition, "3,4");

        Assert.False(result.IsSuccess);
        Assert.Contains("字符长度不符", result.FailureReason);
        Assert.All(definition.Outputs, field =>
        {
            Assert.Null(field.Value);
            Assert.Equal(string.Empty, field.Result);
            Assert.Equal("NG", field.Status);
        });
        Assert.Equal(2, definition.ParseHistory.Count);
        Assert.Contains("字符长度不符", definition.ParseHistoryText);
        definition.ShowMessageAsHex = true;
        Assert.Contains(Convert.ToHexString(Encoding.UTF8.GetBytes("3,4")), definition.ParseHistoryText);
    }

    [Fact]
    public void Missing_required_field_never_publishes_partial_results()
    {
        var definition = ParseTwoStrings();
        var result = Process(definition, "1");

        Assert.False(result.IsSuccess);
        Assert.Contains("字段不足", result.FailureReason);
        Assert.All(definition.Outputs, field => Assert.Null(field.Value));
        Assert.Single(definition.ParseHistory);
        Assert.False(definition.ParseHistory[0].IsSuccess);
    }

    [Fact]
    public void One_output_rule_consumes_first_field_without_requiring_the_message_to_have_one_field()
    {
        var definition = new CommunicationReceiveEventViewModel { EventType = CommunicationEventType.ProtocolCompare };
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel { Name = "in1", DataType = "string" });

        var result = Process(definition, "4,3,4,5,6,7");

        Assert.True(result.IsSuccess);
        Assert.Equal("4", definition.CompareInputs[0].Value);
        Assert.Equal("OK", definition.CompareInputs[0].Status);
        Assert.True(definition.ParseHistory[0].IsSuccess);
    }

    [Fact]
    public void Optional_character_length_checks_complete_message_independently_of_output_count()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            CharacterLengthCompare = true, CharacterLength = 11
        };
        definition.Outputs.Add(new CommunicationEventOutputViewModel { Name = "out1", DataType = "string" });

        Assert.True(Process(definition, "4,3,4,5,6,7").IsSuccess);
        Assert.Equal("4", definition.Outputs[0].Value);

        definition.CharacterLength = 1;
        var rejected = Process(definition, "4,3,4,5,6,7");
        Assert.False(rejected.IsSuccess);
        Assert.Contains("期望 1，实际 11", rejected.FailureReason);
        Assert.Null(definition.Outputs[0].Value);
    }

    [Fact]
    public void Range_comparison_reports_ng_and_does_not_copy_value_into_assembly()
    {
        var definition = new CommunicationReceiveEventViewModel { EventType = CommunicationEventType.ProtocolCompare, DataMode = "Binary" };
        var input = new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "int", CompareRule = "范围", Minimum = 0, Maximum = 1
        };
        definition.CompareInputs.Add(input);
        var output = new CommunicationAssemblyOutputViewModel { SourceFieldId = input.FieldId, Content = "configured" };
        definition.AssemblyOutputs.Add(output);

        Assert.True(CommunicationReceiveProcessor.Process(definition, new byte[] { 0, 0, 0, 1 }, Timestamp).IsSuccess);
        Assert.Equal("1", output.Result);
        var result = CommunicationReceiveProcessor.Process(definition, new byte[] { 0, 0, 0, 4 }, Timestamp);

        Assert.False(result.IsSuccess);
        Assert.Equal(4, input.Value);
        Assert.Equal("4", input.Result);
        Assert.Equal("NG", input.Status);
        Assert.Equal(string.Empty, output.Result);
        Assert.Equal("configured", output.Content);
        Assert.Contains("比较未通过", definition.ParseHistoryText);
    }

    [Fact]
    public void Text_numeric_type_is_ng_with_empty_result_even_when_comparison_is_disabled()
    {
        var definition = new CommunicationReceiveEventViewModel { EventType = CommunicationEventType.ProtocolCompare };
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel { Name = "in1", DataType = "int" });

        var result = Process(definition, "4");

        Assert.False(result.IsSuccess);
        Assert.Contains("类型不匹配", result.FailureReason);
        Assert.Null(definition.CompareInputs[0].Value);
        Assert.Equal(string.Empty, definition.CompareInputs[0].Result);
        Assert.Equal("NG", definition.CompareInputs[0].Status);
        Assert.Contains("类型不匹配", definition.ParseHistoryText);
    }

    [Fact]
    public void Character_length_rejects_before_field_type_validation_and_splitting()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            Separator = ",", CharacterLengthCompare = true, CharacterLength = 1
        };
        definition.Outputs.Add(new CommunicationEventOutputViewModel { Name = "out1", DataType = "int" });

        var result = Process(definition, "4,3");

        Assert.False(result.IsSuccess);
        Assert.Contains("字符长度不符：期望 1，实际 3", result.FailureReason);
        Assert.Equal(string.Empty, definition.Outputs[0].Result);
        Assert.Equal("NG", definition.Outputs[0].Status);
    }

    [Theory]
    [InlineData("等于", "NG", true)]
    [InlineData("等于", "OK", false)]
    [InlineData("不等于", "NG", false)]
    [InlineData("不等于", "OK", true)]
    public void String_rules_use_exact_ordinal_comparison(string rule, string actual, bool expected)
    {
        var definition = new CommunicationReceiveEventViewModel { EventType = CommunicationEventType.ProtocolCompare };
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "string", CompareRule = rule, CompareValue = "NG"
        });

        Assert.Equal(expected, Process(definition, actual).IsSuccess);
    }

    [Fact]
    public void Binary_big_endian_int_is_decoded_correctly_and_short_data_is_rejected()
    {
        var definition = new CommunicationReceiveEventViewModel { DataMode = "Binary", ByteOrder = "BigEndian" };
        definition.Outputs.Add(new CommunicationEventOutputViewModel { Name = "out1", DataType = "int" });

        Assert.True(CommunicationReceiveProcessor.Process(definition, new byte[] { 0, 0, 0, 4 }, Timestamp).IsSuccess);
        Assert.Equal(4, definition.Outputs[0].Value);
        Assert.False(CommunicationReceiveProcessor.Process(definition, new byte[] { 0, 4 }, Timestamp).IsSuccess);
        Assert.Null(definition.Outputs[0].Value);
    }

    [Fact]
    public void Opc_ua_boolean_uses_native_value_and_does_not_convert_text()
    {
        var definition = new CommunicationReceiveEventViewModel();
        var output = new CommunicationEventOutputViewModel { Name = "out1", DataType = "Boolean" };
        definition.Outputs.Add(output);
        var update = new OpcUaPointValueEventArgs("device", "ns=2;s=test1", false,
            BuiltInType.Boolean, false, StatusCodes.Good, Timestamp);

        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, update).IsSuccess);
        Assert.Equal(false, output.Value);
        Assert.Equal("False", output.Result);
        Assert.Equal("OK", output.Status);

        Assert.False(Process(definition, "False").IsSuccess);
        Assert.Equal(string.Empty, output.Result);
        Assert.Equal("NG", output.Status);
    }

    [Theory]
    [InlineData("等于", "False", true)]
    [InlineData("等于", "True", false)]
    [InlineData("不等于", "True", true)]
    [InlineData("不等于", "False", false)]
    public void Opc_ua_boolean_comparison_uses_boolean_rules(string rule, string expected, bool success)
    {
        var definition = new CommunicationReceiveEventViewModel { EventType = CommunicationEventType.ProtocolCompare };
        var field = new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "Boolean", CompareRule = rule, CompareValue = expected
        };
        definition.CompareInputs.Add(field);
        Assert.Equal(new[] { "不比较", "等于", "不等于" }, field.CompareRuleOptions);

        var update = new OpcUaPointValueEventArgs("device", "ns=2;s=test1", false,
            BuiltInType.Boolean, false, StatusCodes.Good, Timestamp);
        Assert.Equal(success, CommunicationReceiveProcessor.ProcessOpcUa(definition, update).IsSuccess);
        Assert.Equal("False", field.Result);
        Assert.Equal(success ? "OK" : "NG", field.Status);
    }

    [Fact]
    public void Opc_ua_type_mismatch_or_bad_quality_does_not_publish_stale_result()
    {
        var definition = new CommunicationReceiveEventViewModel();
        var output = new CommunicationEventOutputViewModel { Name = "out1", DataType = "Boolean" };
        definition.Outputs.Add(output);
        var good = new OpcUaPointValueEventArgs("device", "ns=2;s=test1", true,
            BuiltInType.Boolean, false, StatusCodes.Good, Timestamp);
        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, good).IsSuccess);

        var mismatch = new OpcUaPointValueEventArgs("device", "ns=2;s=test1", "True",
            BuiltInType.String, false, StatusCodes.Good, Timestamp);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(definition, mismatch).IsSuccess);
        Assert.Null(output.Value);
        Assert.Equal(string.Empty, output.Result);

        var bad = new OpcUaPointValueEventArgs("device", "ns=2;s=test1", false,
            BuiltInType.Boolean, false, StatusCodes.Bad, Timestamp);
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(definition, bad).IsSuccess);
        Assert.Equal("NG", output.Status);
    }

    [Fact]
    public void Opc_ua_cached_value_can_be_replayed_after_correcting_type_without_new_notification()
    {
        var definition = new CommunicationReceiveEventViewModel();
        var output = new CommunicationEventOutputViewModel { Name = "out1", DataType = "int" };
        definition.Outputs.Add(output);
        var cached = new OpcUaPointValueEventArgs("device", "ns=2;s=test2", 12.5d,
            BuiltInType.Double, false, StatusCodes.Good, Timestamp);

        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(definition, cached, recordHistory: false).IsSuccess);
        Assert.Equal("NG", output.Status);
        Assert.Equal(string.Empty, output.Result);
        Assert.Equal(string.Empty, definition.ParseHistoryText);

        output.DataType = "Double";
        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, cached, recordHistory: false).IsSuccess);
        Assert.Equal(12.5d, output.Value);
        Assert.Equal("12.5", output.Result);
        Assert.Equal("OK", output.Status);
        Assert.Equal(string.Empty, definition.ParseHistoryText);
    }

    [Fact]
    public void Opc_ua_cached_boolean_can_fill_new_output_without_fake_receive_history()
    {
        var definition = new CommunicationReceiveEventViewModel();
        var cached = new OpcUaPointValueEventArgs("device", "ns=2;s=test1", true,
            BuiltInType.Boolean, false, StatusCodes.Good, Timestamp);
        var output = new CommunicationEventOutputViewModel { Name = "out1", DataType = "Boolean" };
        definition.Outputs.Add(output);

        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, cached, recordHistory: false).IsSuccess);
        Assert.Equal("True", output.Result);
        Assert.Equal("OK", output.Status);
        Assert.Equal(string.Empty, definition.ParseHistoryText);
    }

    [Fact]
    public void Opc_ua_string_uses_separator_and_keeps_trailing_empty_field()
    {
        var definition = new CommunicationReceiveEventViewModel { Separator = "," };
        for (var index = 1; index <= 6; index++)
            definition.Outputs.Add(new CommunicationEventOutputViewModel
            {
                Name = $"out{index}", DataType = "string"
            });
        var update = new OpcUaPointValueEventArgs("device", "ns=2;s=test3", "1,1,1,1,1,",
            BuiltInType.String, false, StatusCodes.Good, Timestamp);

        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, update).IsSuccess);
        Assert.Equal(new[] { "1", "1", "1", "1", "1", "" },
            definition.Outputs.Select(output => output.Result));
        Assert.All(definition.Outputs, output => Assert.Equal("OK", output.Status));
    }

    [Fact]
    public void Opc_ua_string_replays_existing_value_for_new_field_count_without_new_notification()
    {
        var definition = new CommunicationReceiveEventViewModel { Separator = "," };
        var first = new CommunicationEventOutputViewModel { Name = "out1", DataType = "string" };
        definition.Outputs.Add(first);
        var update = new OpcUaPointValueEventArgs("device", "ns=2;s=test3", "11111,22222",
            BuiltInType.String, false, StatusCodes.Good, Timestamp);

        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, update, recordHistory: false).IsSuccess);
        Assert.Equal("11111", first.Result);
        var second = new CommunicationEventOutputViewModel { Name = "out2", DataType = "string" };
        definition.Outputs.Add(second);
        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, update, recordHistory: false).IsSuccess);
        Assert.Equal("22222", second.Result);
        Assert.Equal(string.Empty, definition.ParseHistoryText);
    }

    [Fact]
    public void Opc_ua_split_string_does_not_coerce_numeric_looking_field()
    {
        var definition = new CommunicationReceiveEventViewModel { Separator = "," };
        var field = new CommunicationEventOutputViewModel { Name = "out1", DataType = "int" };
        definition.Outputs.Add(field);
        var update = new OpcUaPointValueEventArgs("device", "ns=2;s=test3", "11111,22222",
            BuiltInType.String, false, StatusCodes.Good, Timestamp);

        var outcome = CommunicationReceiveProcessor.ProcessOpcUa(definition, update);
        Assert.False(outcome.IsSuccess);
        Assert.Equal("NG", field.Status);
        Assert.Equal(string.Empty, field.Result);
    }

    [Fact]
    public void Opc_ua_string_compare_uses_split_fields_and_checks_whole_text_length_first()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            EventType = CommunicationEventType.ProtocolCompare,
            Separator = ",", CharacterLengthCompare = true, CharacterLength = 3
        };
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "string", CompareRule = "不比较"
        });
        definition.CompareInputs.Add(new CommunicationCompareInputViewModel
        {
            Name = "in2", DataType = "string", CompareRule = "等于", CompareValue = "B"
        });
        var update = new OpcUaPointValueEventArgs("device", "ns=2;s=test3", "A,B",
            BuiltInType.String, false, StatusCodes.Good, Timestamp);

        Assert.True(CommunicationReceiveProcessor.ProcessOpcUa(definition, update).IsSuccess);
        Assert.Equal("A", definition.CompareInputs[0].Result);
        Assert.Equal("B", definition.CompareInputs[1].Result);

        definition.CharacterLength = 2;
        Assert.False(CommunicationReceiveProcessor.ProcessOpcUa(definition, update).IsSuccess);
        Assert.All(definition.CompareInputs, input => Assert.Equal(string.Empty, input.Result));
    }

    private static CommunicationReceiveEventViewModel ParseTwoStrings()
    {
        var definition = new CommunicationReceiveEventViewModel { Separator = "," };
        definition.Outputs.Add(new CommunicationEventOutputViewModel { Name = "out1", DataType = "string" });
        definition.Outputs.Add(new CommunicationEventOutputViewModel { Name = "out2", DataType = "string" });
        return definition;
    }

    private static CommunicationReceiveOutcome Process(CommunicationReceiveEventViewModel definition, string text) =>
        CommunicationReceiveProcessor.Process(definition, Encoding.UTF8.GetBytes(text), Timestamp);
}
