namespace SunEyeVision.Plugin.Contracts.Acquisition;

public enum CameraRequestPurpose { Workflow, Preview }

/// <summary>A request is tied either to an actual workflow invocation or to a non-workflow preview.</summary>
public sealed record CameraFrameRequest
{
    public CameraFrameRequest(string deviceId, CameraRequestPurpose purpose, RunIdentity? producerRun,
        TimeSpan timeout, string? diagnosticCorrelationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        if (purpose == CameraRequestPurpose.Workflow && producerRun is null ||
            purpose == CameraRequestPurpose.Preview && producerRun is not null)
            throw new ArgumentException("采集用途与运行身份不一致。", nameof(producerRun));
        if (producerRun is { } run && (run.DocumentSessionId == Guid.Empty || run.RunId == Guid.Empty || run.ScopeId == Guid.Empty || run.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(run.NodeId)))
            throw new ArgumentException("工作流采集必须使用完整的本次运行身份。", nameof(producerRun));
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        DeviceId=deviceId; Purpose=purpose; ProducerRun=producerRun; Timeout=timeout;
        DiagnosticCorrelationId=diagnosticCorrelationId;
    }
    public string DeviceId { get; }
    public CameraRequestPurpose Purpose { get; }
    public RunIdentity? ProducerRun { get; }
    public TimeSpan Timeout { get; }
    public string? DiagnosticCorrelationId { get; }
}

/// <summary>A successful result owns its frozen image until disposed; retained readers live independently.</summary>
public interface ICameraFrameLease : IAsyncDisposable
{
    string DeviceId { get; }
    long DeviceFrameId { get; }
    DateTimeOffset Timestamp { get; }
    IReadOnlyImage Image { get; }
}

public interface ICameraFrameProvider
{
    ValueTask<ICameraFrameLease> CaptureAsync(CameraFrameRequest request, CancellationToken cancellation);
}
