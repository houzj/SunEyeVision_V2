using System;

namespace SunEyeVision.Plugin.SDK.Devices.Communication;

[Flags]
public enum CommunicationCapabilities
{
    None = 0,
    RawSend = 1,
    RawReceive = 2,
    Read = 4,
    Write = 8,
    Browse = 16,
    Subscribe = 32
}
