using System;
using SunEyeVision.Plugin.SDK.Devices.Communication;

namespace SunEyeVision.DeviceDriver.Communications;

/// <summary>
/// Immutable connection configuration. A running driver never reads a mutable solution model.
/// </summary>
public sealed record CommunicationDeviceSnapshot
{
    public required CommunicationDeviceRef Device { get; init; }
    public required string Name { get; init; }
    public required string ProtocolKey { get; init; }
    public required string ProtocolDisplayName { get; init; }
    public string Id => Device.Value;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string LocalHost { get; init; } = string.Empty;
    public int LocalPort { get; init; }
    public string RemoteHost { get; init; } = string.Empty;
    public int RemotePort { get; init; }
    public string SerialPort { get; init; } = string.Empty;
    public int BaudRate { get; init; }
    public int DataBits { get; init; }
    public string Parity { get; init; } = string.Empty;
    public int StopBits { get; init; }
    public int CharacterTimeout { get; init; }
    public int RequestTimeout { get; init; }
    public int UnitId { get; init; }
    public string ModbusMode { get; init; } = string.Empty;
    public int Rack { get; init; }
    public int Slot { get; init; }
    public string S7CpuType { get; init; } = string.Empty;
    public string EndpointUrl { get; init; } = string.Empty;
    public bool EnableReceiveTerminator { get; init; }
    public string ReceiveTerminator { get; init; } = string.Empty;

    public bool IsModbus => ProtocolKey is "modbus" or "modbus-tcp" or "modbus-rtu";
    public bool IsModbusRtu => IsModbus && (ModbusMode is "串口" or "Serial" or "Rtu" || ProtocolKey == "modbus-rtu");
}
