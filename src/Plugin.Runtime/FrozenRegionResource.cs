using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Immutable RLE region; owner and each reader independently retain managed storage.</summary>
public sealed class FrozenRegionResource : IReadOnlyRegion, IDisposable
{
    private sealed record State(CoordinateFrame Frame, ImageFrameAddress Source, RegionRun[] Runs);
    private readonly object _gate = new();
    private State? _state;

    public FrozenRegionResource(CoordinateFrame frame, ImageFrameAddress source, ReadOnlySpan<RegionRun> runs)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(source);
        ResourceContractRules.ValidateRuns(runs);
        _state = new(frame, source, runs.ToArray());
    }
    private State Get() => _state ?? throw new ObjectDisposedException(nameof(FrozenRegionResource));
    public CoordinateFrame Frame { get { lock (_gate) return Get().Frame; } }
    public ImageFrameAddress SourceFrame { get { lock (_gate) return Get().Source; } }
    public IRegionReadLease AcquireReader() { lock (_gate) return new Reader(Get()); }
    public void Dispose() { lock (_gate) _state = null; }

    private sealed class Reader(State state) : IRegionReadLease
    {
        private readonly object _gate = new();
        private State? _state = state;
        private State Get() => _state ?? throw new ObjectDisposedException(nameof(IRegionReadLease));
        public CoordinateFrame Frame { get { lock (_gate) return Get().Frame; } }
        public ImageFrameAddress SourceFrame { get { lock (_gate) return Get().Source; } }
        public int RunCount { get { lock (_gate) return Get().Runs.Length; } }
        public bool IsEmpty => RunCount == 0;
        public ReadOnlyMemory<RegionRun> ReadRuns(int offset, int maximumCount)
        {
            lock (_gate)
            {
                var runs = Get().Runs;
                ResourceContractRules.ValidateBatch(offset, maximumCount, runs.Length);
                return runs.AsMemory(offset, Math.Min(maximumCount, runs.Length - offset));
            }
        }
        public void Dispose() { lock (_gate) _state = null; }
    }
}
