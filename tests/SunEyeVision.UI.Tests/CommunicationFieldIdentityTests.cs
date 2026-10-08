using System;
using System.Collections.ObjectModel;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationFieldIdentityTests
{
    [Fact]
    public void New_fields_have_distinct_stable_ids_even_when_display_names_match()
    {
        var deletedFieldId = Guid.NewGuid();
        var existing = new CommunicationCompareInputViewModel
        {
            FieldId = deletedFieldId,
            Name = "in2"
        };
        var replacement = new CommunicationCompareInputViewModel { Name = "in2" };

        Assert.Equal("in2", existing.Name);
        Assert.Equal("in2", replacement.Name);
        Assert.NotEqual(existing.FieldId, replacement.FieldId);
        Assert.NotEqual(deletedFieldId, replacement.FieldId);
    }

    [Fact]
    public void Assembly_binding_resolves_source_field_id_from_available_data_source()
    {
        var field = new CommunicationCompareInputViewModel { Name = "in1" };
        var source = new AvailableDataSource
        {
            SourceNodeId = "ReceiveEventInputs",
            SourceNodeName = "解析列表",
            SourceFieldId = field.FieldId,
            PropertyName = field.Name,
            DisplayName = field.Name,
            LeafDisplayName = field.Name,
            PropertyType = typeof(string)
        };
        var output = new CommunicationAssemblyOutputViewModel
        {
            AvailableDataSources = new ObservableCollection<AvailableDataSource> { source },
            BindingSource = source.GetBindingPath()
        };

        Assert.Equal(field.FieldId, output.SourceFieldId);
        Assert.Equal(source.SourceNodeId, output.SourceNodeId);
        Assert.Equal(field.Name, output.SourceNameSnapshot);
    }

    [Fact]
    public void Binding_identity_survives_source_display_name_replacement()
    {
        var fieldId = Guid.NewGuid();
        var oldSource = CreateSource(fieldId, "in1");
        var output = new CommunicationAssemblyOutputViewModel
        {
            AvailableDataSources = new ObservableCollection<AvailableDataSource> { oldSource },
            BindingSource = oldSource.GetBindingPath()
        };

        var renamedSource = CreateSource(fieldId, "温度");
        output.AvailableDataSources = new ObservableCollection<AvailableDataSource> { renamedSource };
        output.BindingSource = renamedSource.GetBindingPath();

        Assert.Equal(fieldId, output.SourceFieldId);
        Assert.Equal("温度", output.SourceNameSnapshot);
        Assert.Equal("ReceiveEventInputs.温度", output.BindingSource);
    }

    [Fact]
    public void Selected_assembly_binding_receives_value_after_successful_modbus_comparison()
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var input = new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "int", IsByteRule = true,
            ByteStart = 0, ByteEnd = 1, CompareRule = "不比较"
        };
        definition.CompareInputs.Add(input);
        var source = CreateSource(input.FieldId, input.Name);
        var output = new CommunicationAssemblyOutputViewModel
        {
            Name = "out1", DataType = "int",
            AvailableDataSources = new ObservableCollection<AvailableDataSource> { source }
        };
        definition.AssemblyOutputs.Add(output);

        output.ApplyBindingSelection(source.GetBindingPath(), "解析列表.in1");
        var result = CommunicationReceiveProcessor.ProcessModbus(definition, [5], DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(input.FieldId, output.SourceFieldId);
        Assert.Equal("5", output.Result);

        output.ApplyBindingSelection(string.Empty, string.Empty);
        CommunicationReceiveProcessor.ProcessModbus(definition, [6], DateTimeOffset.UtcNow);
        Assert.Null(output.SourceFieldId);
        Assert.Equal(string.Empty, output.Result);
    }

    [Theory]
    [InlineData("解析列表.in1", "解析列表.in1")]
    [InlineData("", "解析列表.in1")]
    public void Display_text_binding_is_canonicalized_before_assembly(string bindingSource, string displayText)
    {
        var definition = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.ByteMatch,
            EventType = CommunicationEventType.ProtocolCompare
        };
        var input = new CommunicationCompareInputViewModel
        {
            Name = "in1", DataType = "int", IsByteRule = true,
            ByteStart = 0, ByteEnd = 1, CompareRule = "不比较"
        };
        definition.CompareInputs.Add(input);
        var output = new CommunicationAssemblyOutputViewModel
        {
            Name = "out1", DataType = "int",
            AvailableDataSources = new ObservableCollection<AvailableDataSource>
            {
                CreateSource(input.FieldId, input.Name)
            }
        };
        definition.AssemblyOutputs.Add(output);

        output.ApplyBindingSelection(bindingSource, displayText);
        var result = CommunicationReceiveProcessor.ProcessModbus(definition, [5], DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("ReceiveEventInputs.in1", output.BindingSource);
        Assert.Equal(input.FieldId, output.SourceFieldId);
        Assert.Equal("5", output.Result);
    }

    private static AvailableDataSource CreateSource(Guid fieldId, string name) => new()
    {
        SourceNodeId = "ReceiveEventInputs",
        SourceNodeName = "解析列表",
        SourceFieldId = fieldId,
        PropertyName = name,
        DisplayName = name,
        LeafDisplayName = name,
        PropertyType = typeof(string)
    };
}
