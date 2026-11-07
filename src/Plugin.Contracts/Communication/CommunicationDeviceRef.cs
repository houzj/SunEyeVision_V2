using System;

namespace SunEyeVision.Plugin.Contracts.Communication;

/// <summary>Stable logical reference to a communication device in the current solution.</summary>
public sealed record CommunicationDeviceRef
{
    public string Value { get; }

    public CommunicationDeviceRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}
