namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>One source lifetime, shared by the host's per-call sessions or a directly owned session.</summary>
internal sealed class ImageSourceRunState : IDisposable
{
    private readonly object _gate = new();
    private long? _initial;
    private int _sequence;
    private bool _disposed;
    private string? _sourceKind,_device;
    private int _cameraFailures;
    private Guid _sourceRevision,_deviceRevision,_sequenceRevision;
    internal ImageSourceStitchState Stitch { get; } = new();

    internal void ObserveSource(string kind,string device,Guid sourceRevision,Guid deviceRevision)
    {
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if(_sourceKind!=kind||!string.Equals(_device,device,StringComparison.OrdinalIgnoreCase)
                ||_sourceRevision!=sourceRevision||_deviceRevision!=deviceRevision)_cameraFailures=0;
            _sourceKind=kind;_device=device;
            _sourceRevision=sourceRevision;_deviceRevision=deviceRevision;
        }
    }
    internal long CameraFailures(bool increment=false)
    {
        lock(_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if(increment)_cameraFailures=unchecked(_cameraFailures+1);
            return _cameraFailures;
        }
    }

    internal long NextSequence(long initial,Guid revision)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_initial != initial||_sequenceRevision!=revision)
            {
                _sequence = checked((int)initial);
                _initial = initial;
                _sequenceRevision=revision;
            }
            _sequence = unchecked(_sequence + 1);
            return _sequence;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            Stitch.Dispose();
        }
    }
}
