using System;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Communication;

/// <summary>Detached parser state. It has no controls, dispatcher, or design-object references.</summary>
internal static class CommunicationReceiveStateFactory
{
    public static CommunicationReceiveEventViewModel Create(CommunicationEventDefinition d)
    {
        var row = new CommunicationReceiveEventViewModel
        {
            EventId = d.EventId, Name = d.Name, SelectedDeviceId = d.SelectedDeviceId, SelectedAddress = d.SelectedAddress,
            ProcessingMethod = Enum.TryParse<CommunicationEventProcessingMethod>(d.ProcessingMethod, out var method) ? method : CommunicationEventProcessingMethod.Text,
            EventType = Enum.TryParse<CommunicationEventType>(d.EventType, out var type) ? type : CommunicationEventType.ProtocolParse,
            DataMode = d.DataMode, ByteOrder = d.ByteOrder, ByteOffset = d.ByteOffset,
            ByteLengthCompare = d.ByteLengthCompare, ByteLength = d.ByteLength, AsciiData = d.AsciiData,
            ByteMatchLogic = d.ByteMatchLogic, Separator = d.Separator, CharacterLengthCompare = d.CharacterLengthCompare,
            CharacterLength = d.CharacterLength
        };
        foreach (var field in d.Outputs) row.Outputs.Add(new()
        {
            FieldId = field.FieldId, Name = field.Name, DataType = field.DataType, CompareRule = field.CompareRule,
            Minimum = field.Minimum, Maximum = field.Maximum, CompareValue = field.CompareValue
        });
        foreach (var field in d.CompareInputs) row.CompareInputs.Add(new()
        {
            FieldId = field.FieldId, Name = field.Name, DataType = field.DataType, IsByteRule = row.IsByteMatch,
            ByteStart = field.ByteStart, ByteEnd = field.ByteEnd, ByteOrder = field.ByteOrder,
            CompareRule = field.CompareRule, Minimum = field.Minimum, Maximum = field.Maximum, CompareValue = field.CompareValue
        });
        foreach (var field in d.AssemblyOutputs) row.AssemblyOutputs.Add(new()
        {
            FieldId = field.FieldId, Name = field.Name, DataType = field.DataType, Content = field.Content,
            BindingSource = field.BindingSource, BindingDisplayText = field.BindingDisplayText,
            IsContentBound = field.IsContentBound, SourceFieldId = field.SourceFieldId,
            SourceNodeId = field.SourceNodeId, SourceNameSnapshot = field.SourceNameSnapshot
        });
        return row;
    }
}
