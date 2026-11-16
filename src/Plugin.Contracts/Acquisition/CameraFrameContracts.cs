namespace SunEyeVision.Plugin.Contracts.Acquisition;

public enum CameraRequestPurpose { Workflow, Preview }

/// <summary>A request is tied either to an actual workflow invocation or to a non-workflow preview.</summary>
public sealed record CameraFrameRequest
{
    public CameraFrameRequest(string deviceId, CameraRequestPurpose purpose, RunIdentity? producerRun,
        TimeSpan timeout, string? diagnosticCorrelationId = null, double? exposureTime = null, double? gain = null)
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
        if (exposureTime is { } exposure && (!double.IsFinite(exposure) || exposure <= 0))
            throw new ArgumentOutOfRangeException(nameof(exposureTime));
        if (gain is { } level && !double.IsFinite(level))
            throw new ArgumentOutOfRangeException(nameof(gain));
        if (purpose != CameraRequestPurpose.Workflow && (exposureTime.HasValue || gain.HasValue))
            throw new ArgumentException("逐轮参数仅用于工作流单帧采集。");
        DeviceId=deviceId; Purpose=purpose; ProducerRun=producerRun; Timeout=timeout;
        DiagnosticCorrelationId=diagnosticCorrelationId;
        ExposureTime=exposureTime; Gain=gain;
    }
    public string DeviceId { get; }
    public CameraRequestPurpose Purpose { get; }
    public RunIdentity? ProducerRun { get; }
    public TimeSpan Timeout { get; }
    public string? DiagnosticCorrelationId { get; }
    public double? ExposureTime { get; }
    public double? Gain { get; }
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
