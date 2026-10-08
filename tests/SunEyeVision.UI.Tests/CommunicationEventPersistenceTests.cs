using System;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationEventPersistenceTests
{
    [Fact]
    public void Text_parse_output_comparison_rule_round_trips()
    {
        var definition = new CommunicationEventDefinition
        {
            Outputs =
            {
                new CommunicationEventOutputDefinition
                {
                    Name = "out1", DataType = "string", CompareRule = "不等于", CompareValue = "NG"
                }
            }
        };

        var restored = JsonSerializer.Deserialize<CommunicationEventDefinition>(
            JsonSerializer.Serialize(definition, WorkflowSerializationOptions.Default), WorkflowSerializationOptions.Default)!;

        var output = Assert.Single(restored.Outputs);
        Assert.Equal("不等于", output.CompareRule);
        Assert.Equal("NG", output.CompareValue);
    }

    [Fact]
    public void Byte_match_configuration_round_trips_address_and_rule_fields()
    {
        var definition = new CommunicationEventDefinition
        {
            ProcessingMethod = "ByteMatch", EventType = "ProtocolCompare",
            SelectedDeviceId = "modbus-1", SelectedAddress = "address-id-1",
            ByteLengthCompare = true, ByteLength = 4, AsciiData = true, ByteMatchLogic = "或",
            CompareInputs =
            {
                new CommunicationCompareInputDefinition
                {
                    Name = "in1", ByteStart = 0, ByteEnd = 3, ByteOrder = "CDAB",
                    DataType = "int", CompareRule = "范围", Minimum = 1, Maximum = 7
                }
            }
        };

        var restored = JsonSerializer.Deserialize<CommunicationEventDefinition>(
            JsonSerializer.Serialize(definition, WorkflowSerializationOptions.Default), WorkflowSerializationOptions.Default)!;

        Assert.Equal("address-id-1", restored.SelectedAddress);
        Assert.True(restored.ByteLengthCompare);
        Assert.Equal(4, restored.ByteLength);
        Assert.True(restored.AsciiData);
        Assert.Equal("或", restored.ByteMatchLogic);
        var input = Assert.Single(restored.CompareInputs);
        Assert.Equal(0, input.ByteStart);
        Assert.Equal(3, input.ByteEnd);
        Assert.Equal("CDAB", input.ByteOrder);
    }

    [Fact]
    public void CommunicationEventConfiguration_RoundTripsStableFieldAndBindingIdentity()
    {
        var inputId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var solution = new Solution
        {
            CommunicationEvents =
            {
                new CommunicationEventDefinition
                {
                    EventId = eventId,
                    Name = "协议比对1",
                    SelectedDeviceId = "device-1",
                    EventType = "ProtocolCompare",
                    CompareInputs =
                    {
                        new CommunicationCompareInputDefinition
                        {
                            FieldId = inputId,
                            Name = "inRenamed",
                            DataType = "string",
                            CompareRule = "等于",
                            CompareValue = "OK"
                        }
                    },
                    AssemblyOutputs =
                    {
                        new CommunicationAssemblyOutputDefinition
                        {
                            FieldId = Guid.NewGuid(),
                            Name = "out1",
                            DataType = "string",
                            BindingSource = "ReceiveEventInputs.inRenamed",
                            BindingDisplayText = "解析列表.inRenamed",
                            IsContentBound = true,
                            SourceFieldId = inputId,
                            SourceNodeId = "ReceiveEventInputs",
                            SourceNameSnapshot = "inRenamed"
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(solution, WorkflowSerializationOptions.Default);
        var restored = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default)!;
        var definition = Assert.Single(restored.CommunicationEvents);
        var input = Assert.Single(definition.CompareInputs);
        var output = Assert.Single(definition.AssemblyOutputs);

        Assert.Equal(eventId, definition.EventId);
        Assert.Equal(inputId, input.FieldId);
        Assert.Equal(inputId, output.SourceFieldId);
        Assert.Equal("解析列表.inRenamed", output.BindingDisplayText);
        Assert.Equal("string", input.DataType);
    }
}
