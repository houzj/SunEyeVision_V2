using System;
using System.Collections.Generic;

namespace SunEyeVision.Workflow;

/// <summary>Reusable send format. Live values and send history are not persisted.</summary>
public sealed class CommunicationSendEventDefinition
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Mode { get; set; } = "Direct";
    public string Separator { get; set; } = ";";
    public string Terminator { get; set; } = string.Empty;
    public List<CommunicationSendParameterDefinition> Parameters { get; set; } = new();
    public List<CommunicationAssemblyOutputDefinition> AssemblyOutputs { get; set; } = new();
}

public sealed class CommunicationSendParameterDefinition : CommunicationEventFieldDefinition
{
    /// <summary>Configured literal used by the manual send test; callers may supply a typed snapshot instead.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>Presentation only; event identity and persisted names are not rewritten.</summary>
public static class CommunicationSendEventNames
{
    public static string DisplayName(string? name, string? mode) => !string.IsNullOrWhiteSpace(name)
        ? name.Trim()
        : mode switch { "Direct" => "文本-直接输出", "Assembly" => "文本-组装输出", _ => "发送事件" };
}
