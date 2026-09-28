namespace SunEyeVision.Plugin.SDK.Devices.Communication;

public enum CommunicationErrorCode
{
    None,
    InvalidConfiguration,
    DriverUnavailable,
    ConnectionFailed,
    Timeout,
    ProtocolRejected,
    AuthenticationFailed,
    CertificateUntrusted,
    Cancelled,
    Unexpected
}

/// <summary>A protocol-independent diagnostic; never contains credentials or raw secrets.</summary>
public sealed record CommunicationError(CommunicationErrorCode Code, string Message);
