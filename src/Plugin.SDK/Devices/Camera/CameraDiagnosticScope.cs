using System;
using System.Threading;

namespace SunEyeVision.Plugin.SDK.Devices.Camera;

/// <summary>
/// Carries an optional camera operation correlation id across async adapter calls for diagnostic logs.
/// </summary>
public sealed class CameraDiagnosticScope : IDisposable
{
    private static readonly AsyncLocal<string?> CurrentId = new();
    private readonly string? _previousId;
    private bool _disposed;

    private CameraDiagnosticScope(string? correlationId)
    {
        _previousId = CurrentId.Value;
        CurrentId.Value = string.IsNullOrWhiteSpace(correlationId) ? _previousId : correlationId;
    }

    public static string? CurrentCorrelationId => CurrentId.Value;

    public static CameraDiagnosticScope Begin(string? correlationId) => new(correlationId);

    public void Dispose()
    {
        if (_disposed)
            return;

        CurrentId.Value = _previousId;
        _disposed = true;
    }
}
