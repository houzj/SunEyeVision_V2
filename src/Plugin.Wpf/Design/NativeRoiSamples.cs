using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Design;

/// <summary>Shared preview-only geometry and fixed-frame image implementation, not business defaults.</summary>
internal static class NativeRoiSamples
{
    internal static IReadOnlyList<RoiEditItem> CreateItems()
    {
            var sector=RoiGeometryEditing.Values(RoiGeometryEditing.Create("annulus",new(300,150),new(400,150)));
            sector["kind"]="sector";sector["start-radians"]=.3;sector["end-radians"]=2.3;
            return new[]{
                new RoiEditItem("detect",Guid.NewGuid(),"矩形",RoiGeometryEditing.Create("rectangle",new(100,70),new(380,240))),
                new RoiEditItem("mask",Guid.NewGuid(),"圆环",RoiGeometryEditing.Create("annulus",new(300,150),new(400,150))),
                new RoiEditItem("detect",Guid.NewGuid(),"多边形",RoiGeometryEditing.Create("polygon",new(0,0),new(0,0),new[]{new Point2(90,80),new Point2(360,60),new Point2(440,230),new Point2(150,270)})),
                new RoiEditItem("mask",Guid.NewGuid(),"扇形",RoiGeometryEditing.Checked(sector))};
    }
    // Each lease retains immutable managed state independently; no runtime image factory or native handle.
    private sealed record ImageState(ImageLayout Layout,ImageFrameAddress Frame,byte[] Pixels,RegionRun[] Runs);
    internal sealed class Image : IReadOnlyImage,IDisposable
    {
        private ImageState? _state;
        internal Image()
        {
            var layout=new ImageLayout(640,320,648,PixelLayout.Gray8);var pixels=new byte[layout.BufferLength];
            for(var y=0;y<layout.Height;y++)for(var x=0;x<layout.RowStrideBytes;x++)pixels[y*layout.RowStrideBytes+x]=x>=layout.Width?(byte)233:(byte)(45+(x/32+y/32)%2*25);
            _state=new(layout,new(Guid.NewGuid(),"library-roi-design"),pixels,Enumerable.Range(0,320).Select(y=>new RegionRun(y,0,639)).ToArray());
            ResourceContractRules.ValidateImage(this);
        }
        private ImageState State=>_state??throw new ObjectDisposedException(nameof(Image));
        public ImageLayout Layout=>State.Layout;public ImageFrameAddress Frame=>State.Frame;
        public int Width=>Layout.Width;public int Height=>Layout.Height;
        public IReadOnlyRegion EffectiveDomain {get{_=State;return new Domain(()=>State);}}
        public byte[] CopyPixels()=>State.Pixels.ToArray();
        public IImagePixelReadLease AcquirePixels()=>new Pixels(State);
        public void Dispose()=>_state=null;
    }
    private sealed class Domain(Func<ImageState> state):IReadOnlyRegion
    {
        public CoordinateFrame Frame {get {_=state();return RoiDeclarations.ImageFrame;}}
        public ImageFrameAddress SourceFrame=>state().Frame;
        public IRegionReadLease AcquireReader()=>new Runs(state());
    }
    private sealed class Pixels(ImageState state):IImagePixelReadLease
    {
        private ImageState? _state=state;
        private ImageState State=>_state??throw new ObjectDisposedException(nameof(Pixels));
        public ImageLayout Layout=>State.Layout;public ImageFrameAddress Frame=>State.Frame;
        public IReadOnlyRegion EffectiveDomain {get{_=State;return new Domain(()=>State);}}
        public ReadOnlySpan<byte> GetRow(int row)
        {
            var s=State;if(row<0||row>=s.Layout.Height)throw new ArgumentOutOfRangeException(nameof(row));
            return s.Pixels.AsSpan(row*s.Layout.RowStrideBytes,s.Layout.RowStrideBytes);
        }
        public IRegionReadLease AcquireDomainReader()=>new Runs(State);
        public void Dispose()=>_state=null;
    }
    private sealed class Runs(ImageState state):IRegionReadLease
    {
        private ImageState? _state=state;
        private ImageState State=>_state??throw new ObjectDisposedException(nameof(Runs));
        public CoordinateFrame Frame {get{_=State;return RoiDeclarations.ImageFrame;}}
        public ImageFrameAddress SourceFrame=>State.Frame;
        public bool IsEmpty=>State.Runs.Length==0;public int RunCount=>State.Runs.Length;
        public ReadOnlyMemory<RegionRun> ReadRuns(int offset,int maximumCount)
        {
            var s=State;ResourceContractRules.ValidateBatch(offset,maximumCount,s.Runs.Length);
            return s.Runs.AsMemory(offset,Math.Min(maximumCount,s.Runs.Length-offset));
        }
        public void Dispose()=>_state=null;
    }
}
