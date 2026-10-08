using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>
/// Describes the location an event must select inside its bound device.
/// This is independent of how the resulting value is decoded or compared.
/// </summary>
internal enum CommunicationReceiveTargetKind
{
    None,
    ModbusAddress,
    S7Memory,
    CipAttribute,
    OpcUaDataPoint
}

internal static class CommunicationReceiveTargetKinds
{
    public static CommunicationReceiveTargetKind For(Communication? device) => device switch
    {
        { IsModbus: true } => CommunicationReceiveTargetKind.ModbusAddress,
        { IsS7: true } => CommunicationReceiveTargetKind.S7Memory,
        { IsCip: true } => CommunicationReceiveTargetKind.CipAttribute,
        { IsOpcUa: true } => CommunicationReceiveTargetKind.OpcUaDataPoint,
        _ => CommunicationReceiveTargetKind.None
    };

    public static string Label(CommunicationReceiveTargetKind kind) => kind switch
    {
        CommunicationReceiveTargetKind.ModbusAddress => "绑定地址",
        CommunicationReceiveTargetKind.S7Memory => "绑定内存地址",
        CommunicationReceiveTargetKind.CipAttribute => "绑定属性",
        CommunicationReceiveTargetKind.OpcUaDataPoint => "绑定数据点",
        _ => string.Empty
    };
}
