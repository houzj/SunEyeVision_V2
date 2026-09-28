using SunEyeVision.Plugin.SDK.Devices.Communication;

namespace SunEyeVision.DeviceDriver.Communications;

internal interface ICommunicationProtocolFactory : IProtocolConfigurationValidator
{
    string ProtocolId { get; }
    CommunicationCapabilities Capabilities { get; }
    ICommunicationAdapter Create(CommunicationDeviceSnapshot configuration);
}
