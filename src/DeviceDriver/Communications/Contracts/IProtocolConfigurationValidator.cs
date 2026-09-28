namespace SunEyeVision.DeviceDriver.Communications;

internal interface IProtocolConfigurationValidator
{
    void Validate(CommunicationDeviceSnapshot configuration);
}
