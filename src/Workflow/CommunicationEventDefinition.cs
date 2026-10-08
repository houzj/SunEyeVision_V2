using System;
using System.Collections.Generic;

namespace SunEyeVision.Workflow;

/// <summary>
/// 通讯接收事件的持久化配置。运行时值、展开状态和临时消息不写入方案。
/// </summary>
public sealed class CommunicationEventDefinition
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SelectedDeviceId { get; set; } = string.Empty;
    public string SelectedAddress { get; set; } = string.Empty;
    public string ProcessingMethod { get; set; } = "Text";
    public string EventType { get; set; } = "ProtocolParse";
    public string DataMode { get; set; } = "Text";
    public string ByteOrder { get; set; } = "BigEndian";
    public int ByteOffset { get; set; }
    public bool ByteLengthCompare { get; set; }
    public int ByteLength { get; set; } = 1;
    public bool AsciiData { get; set; }
    public string ByteMatchLogic { get; set; } = "与";
    public string Separator { get; set; } = ",";
    public bool CharacterLengthCompare { get; set; }
    public int CharacterLength { get; set; } = 1;
    public bool EchoToDevice { get; set; }
    public List<CommunicationEventOutputDefinition> Outputs { get; set; } = new();
    public List<CommunicationCompareInputDefinition> CompareInputs { get; set; } = new();
    public List<CommunicationAssemblyOutputDefinition> AssemblyOutputs { get; set; } = new();
}

public class CommunicationEventFieldDefinition
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "int";
}

public sealed class CommunicationEventOutputDefinition : CommunicationEventFieldDefinition
{
    public string CompareRule { get; set; } = "不比较";
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 1;
    public string CompareValue { get; set; } = string.Empty;
}

public sealed class CommunicationCompareInputDefinition : CommunicationEventFieldDefinition
{
    public int ByteStart { get; set; }
    public int ByteEnd { get; set; } = 1;
    public string ByteOrder { get; set; } = "ABCD";
    public string CompareRule { get; set; } = "不比较";
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 1;
    public string CompareValue { get; set; } = string.Empty;
}

public sealed class CommunicationAssemblyOutputDefinition : CommunicationEventFieldDefinition
{
    public string Content { get; set; } = string.Empty;
    public string BindingSource { get; set; } = string.Empty;
    public string BindingDisplayText { get; set; } = string.Empty;
    public bool IsContentBound { get; set; }
    public Guid? SourceFieldId { get; set; }
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourceNameSnapshot { get; set; } = string.Empty;
}
