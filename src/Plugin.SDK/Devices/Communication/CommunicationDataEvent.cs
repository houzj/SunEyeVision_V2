using System;

namespace SunEyeVision.Plugin.SDK.Devices.Communication;

public enum CommunicationDataDirection { Received, Sent }

/// <summary>Metadata for an observed transfer; payload ownership remains with the producer.</summary>
public sealed record CommunicationDataEvent(
    CommunicationDeviceRef Device,
    DateTimeOffset Timestamp,
    CommunicationDataDirection Direction,
    string? RequestId,
    int ByteCount);
