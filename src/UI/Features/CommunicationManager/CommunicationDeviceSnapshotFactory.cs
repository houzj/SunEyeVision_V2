using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.SDK.Devices.Communication;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>Maps persisted configuration into a driver-owned immutable snapshot.</summary>
public static class CommunicationDeviceSnapshotFactory
{
    public static CommunicationDeviceSnapshot Create(Communication device) => new()
    {
        Device = new CommunicationDeviceRef(device.Id),
        Name = device.Name,
        ProtocolKey = device.ProtocolKey,
        ProtocolDisplayName = device.ProtocolDisplayName,
        Host = device.Host,
        Port = device.Port,
        LocalHost = device.LocalHost,
        LocalPort = device.LocalPort,
        RemoteHost = device.RemoteHost,
        RemotePort = device.RemotePort,
        SerialPort = device.SerialPort,
        BaudRate = device.BaudRate,
        DataBits = device.DataBits,
        Parity = device.Parity,
        StopBits = device.StopBits,
        CharacterTimeout = device.CharacterTimeout,
        RequestTimeout = device.RequestTimeout,
        UnitId = device.UnitId,
        ModbusMode = device.ModbusMode,
        Rack = device.Rack,
        Slot = device.Slot,
        S7CpuType = device.S7CpuType,
        EndpointUrl = device.EndpointUrl,
        SecurityMode = device.SecurityMode,
        OpcUaIdentityMode = device.OpcUaIdentityMode
    };
}
