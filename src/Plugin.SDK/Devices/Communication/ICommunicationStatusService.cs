using System;

namespace SunEyeVision.Plugin.SDK.Devices.Communication;

public sealed record CommunicationStatusSnapshot(
    CommunicationDeviceRef Device,
    CommunicationState State,
    CommunicationCapabilities Capabilities,
    CommunicationError? LastError,
    DateTimeOffset Timestamp);

/// <summary>Read-only status capability; callers cannot disconnect a shared device through it.</summary>
public interface ICommunicationStatusService
{
    CommunicationStatusSnapshot GetStatus(CommunicationDeviceRef device);
    event EventHandler<CommunicationStatusSnapshot>? StatusChanged;
}
