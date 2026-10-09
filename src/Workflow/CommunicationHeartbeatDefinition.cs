using System;

namespace SunEyeVision.Workflow;

/// <summary>Configuration only. Runtime counters and timer state are not persisted.</summary>
public sealed class CommunicationHeartbeatDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Mode { get; set; } = "Single";
    public string DataType { get; set; } = "string";
    public string ContentA { get; set; } = "";
    public string ContentB { get; set; } = "";
    public int IntervalMs { get; set; } = 1000;
    public bool Enabled { get; set; }
}
