using System;
using Opc.Ua;

namespace SunEyeVision.DeviceDriver.Communications;

/// <summary>Typed OPC UA value/quality event, independent of the diagnostic receive log.</summary>
public sealed class OpcUaPointValueEventArgs : EventArgs
{
    public string CommunicationId { get; }
    public string NodeId { get; }
    public object? Value { get; }
    public BuiltInType BuiltInType { get; }
    public bool CanWrite { get; }
    public bool IsSupportedScalar { get; }
    public StatusCode StatusCode { get; }
    public DateTimeOffset Timestamp { get; }
    public string? ErrorMessage { get; }

    public OpcUaPointValueEventArgs(string communicationId, string nodeId, object? value,
        BuiltInType builtInType, bool canWrite, StatusCode statusCode, DateTimeOffset timestamp, string? errorMessage = null,
        bool isSupportedScalar = true)
    {
        CommunicationId = communicationId;
        NodeId = nodeId;
        Value = value;
        BuiltInType = builtInType;
        CanWrite = canWrite;
        IsSupportedScalar = isSupportedScalar;
        StatusCode = statusCode;
        Timestamp = timestamp;
        ErrorMessage = errorMessage;
    }
}
