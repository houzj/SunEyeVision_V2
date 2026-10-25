using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One managed frozen frame and its actual pixel Domain. Each reader owns an independent retain.</summary>
public sealed class FrozenImageResource : IReadOnlyImage, IDisposable
{
    private readonly Retain _root;
    private FrozenImageResource(Retain root) => _root = root;
    public FrozenImageResource RetainSnapshot() => new(_root.Fork());
    public FrozenImageResource(ImageLayout layout, ImageFrameAddress frame, CoordinateFrame domainFrame,
        ReadOnlySpan<byte> pixels, ReadOnlySpan<RegionRun> domain)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(domainFrame);
        if(domainFrame.Domain!=CoordinateDomain.Image || domainFrame.Unit!=CoordinateUnit.Pixel)
            throw new ArgumentException("Image Domain must use image/pixel coordinates.",nameof(domainFrame));
        if(pixels.Length!=layout.BufferLength)throw new ArgumentException("Pixel length must include exactly the declared stride padding.",nameof(pixels));
        ResourceContractRules.ValidateRuns(domain);
        foreach(var run in domain)
            if(run.Row>=layout.Height || run.LastColumnInclusive>=layout.Width)
                throw new ArgumentException("Image Domain must be inside the captured pixel grid.",nameof(domain));
        _root=new Retain(new State(layout,frame,domainFrame,pixels.ToArray(),domain.ToArray()));
    }
    public int Width=>Layout.Width;
    public int Height=>Layout.Height;
    public ImageLayout Layout=>_root.Get().Layout;
    public ImageFrameAddress Frame=>_root.Get().Frame;
    public IReadOnlyRegion EffectiveDomain {get{_root.Get();return new RegionView(_root);}}
    public byte[] CopyPixels()=>_root.Get().Pixels!.ToArray();
    public IImagePixelReadLease AcquirePixels()=>new PixelReader(_root.Fork());
    public void Dispose()=>_root.Dispose();

    private sealed class State(ImageLayout layout,ImageFrameAddress frame,CoordinateFrame domainFrame,byte[] pixels,RegionRun[] runs)
    {
        public readonly ImageLayout Layout=layout;
        public readonly ImageFrameAddress Frame=frame;
        public readonly CoordinateFrame DomainFrame=domainFrame;
        public readonly byte[] Pixels=pixels;
        public readonly RegionRun[] Runs=runs;
        private readonly object _gate=new();
        private int _references=1;
        public void AddReference()
        {
            lock(_gate){if(_references==0)throw new ObjectDisposedException(nameof(FrozenImageResource));checked{_references++;}}
        }
        public void Release()
        {
            // No disposed retain keeps State reachable. Existing returned spans keep managed storage safe.
            lock(_gate){_references--;}
        }
    }
    private sealed class Retain(State state):IDisposable
    {
        private readonly object _gate=new();
        private State? _state=state;
        public State Get(){lock(_gate)return _state??throw new ObjectDisposedException(nameof(FrozenImageResource));}
        public Retain Fork()
        {
            lock(_gate)
            {
                var current=_state??throw new ObjectDisposedException(nameof(FrozenImageResource));
                current.AddReference();return new Retain(current);
            }
        }
        public void Dispose()
        {
            State? release;
            lock(_gate){release=_state;_state=null;}
            release?.Release();
        }
    }
    private sealed class RegionView(Retain retain):IReadOnlyRegion
    {
        public CoordinateFrame Frame=>retain.Get().DomainFrame;
        public ImageFrameAddress SourceFrame=>retain.Get().Frame;
        public IRegionReadLease AcquireReader()=>new RegionReader(retain.Fork());
    }
    private sealed class PixelReader(Retain retain):IImagePixelReadLease
    {
        public ImageLayout Layout=>retain.Get().Layout;
        public ImageFrameAddress Frame=>retain.Get().Frame;
        public IReadOnlyRegion EffectiveDomain {get{retain.Get();return new RegionView(retain);}}
        public ReadOnlySpan<byte> GetRow(int row)
        {
            var state=retain.Get();
            if(row<0 || row>=state.Layout.Height)throw new ArgumentOutOfRangeException(nameof(row));
            return state.Pixels!.AsSpan(checked(row*state.Layout.RowStrideBytes),state.Layout.RowStrideBytes);
        }
        public IRegionReadLease AcquireDomainReader()=>new RegionReader(retain.Fork());
        public void Dispose()=>retain.Dispose();
    }
    private sealed class RegionReader(Retain retain):IRegionReadLease
    {
        public CoordinateFrame Frame=>retain.Get().DomainFrame;
        public ImageFrameAddress SourceFrame=>retain.Get().Frame;
        public bool IsEmpty=>RunCount==0;
        public int RunCount=>retain.Get().Runs!.Length;
        public ReadOnlyMemory<RegionRun> ReadRuns(int offset,int maximumCount)
        {
            var runs=retain.Get().Runs!;
            ResourceContractRules.ValidateBatch(offset,maximumCount,runs.Length);
            return runs.AsMemory(offset,Math.Min(maximumCount,runs.Length-offset));
        }
        public void Dispose()=>retain.Dispose();
    }
}
