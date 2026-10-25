namespace SunEyeVision.Plugin.Contracts;
public enum PixelLayout { Gray8, Bgr24, Bgra32 }
/// <summary>Positive stride. Copies and row reads include row padding.</summary>
public sealed record ImageLayout
{
    public ImageLayout(int width, int height, int rowStrideBytes, PixelLayout format)
    {
        if(width<=0 || height<=0)throw new ArgumentOutOfRangeException(nameof(width));
        if(!Enum.IsDefined(format))throw new ArgumentOutOfRangeException(nameof(format));
        int bpp=format switch {PixelLayout.Gray8=>1,PixelLayout.Bgr24=>3,_=>4};
        if(rowStrideBytes<checked(width*bpp))throw new ArgumentOutOfRangeException(nameof(rowStrideBytes));
        Width=width;Height=height;RowStrideBytes=rowStrideBytes;Format=format;
        BufferLength=checked(rowStrideBytes*height);
    }
    public int Width {get;} public int Height {get;} public int RowStrideBytes {get;}
    public PixelLayout Format {get;} public int BufferLength {get;}
}
/// <summary>Physical frozen provenance, distinct from logical CoordinateFrame.</summary>
public sealed record ImageFrameAddress
{
    public ImageFrameAddress(Guid frameId,string sourceId,RunIdentity? producerRun=null,string? sourcePortId=null,long? schemaRevision=null)
    {
        if(frameId==Guid.Empty || string.IsNullOrWhiteSpace(sourceId))throw new ArgumentException("Frame/source identity is required.");
        if(sourcePortId is not null && string.IsNullOrWhiteSpace(sourcePortId))throw new ArgumentException("Source port cannot be empty.");
        if(schemaRevision<0)throw new ArgumentOutOfRangeException(nameof(schemaRevision));
        FrameId=frameId;SourceId=sourceId;ProducerRun=producerRun;SourcePortId=sourcePortId;SchemaRevision=schemaRevision;
    }
    public Guid FrameId {get;} public string SourceId {get;} public RunIdentity? ProducerRun {get;}
    public string? SourcePortId {get;} public long? SchemaRevision {get;}
}
/// <summary>Borrowed frozen image. Legacy adapters must provide the actual Domain, never a dimension-derived fallback.</summary>
public interface IReadOnlyImage
{
    int Width {get;} int Height {get;} ImageLayout Layout {get;} ImageFrameAddress Frame {get;}
    IReadOnlyRegion EffectiveDomain {get;}
    /// <summary>Independent copy, exactly Layout.BufferLength bytes including stride padding.</summary>
    byte[] CopyPixels();
    IImagePixelReadLease AcquirePixels();
}
/// <summary>Independent retain of one frozen image and Domain. GetRow includes padding and is valid only until disposal.
/// All reads after disposal reject. AcquireDomainReader captures this frame, never latest.</summary>
public interface IImagePixelReadLease : IDisposable
{
    ImageLayout Layout {get;} ImageFrameAddress Frame {get;} IReadOnlyRegion EffectiveDomain {get;}
    ReadOnlySpan<byte> GetRow(int row);
    IRegionReadLease AcquireDomainReader();
}
public readonly record struct RegionRun
{
    public RegionRun(int row,int firstColumn,int lastColumnInclusive)
    {
        if(row<0 || firstColumn<0 || lastColumnInclusive<firstColumn)throw new ArgumentOutOfRangeException(nameof(firstColumn));
        Row=row;FirstColumn=firstColumn;LastColumnInclusive=lastColumnInclusive;
    }
    public int Row {get;} public int FirstColumn {get;} public int LastColumnInclusive {get;}
}
/// <summary>Empty runs are valid empty, never Unset. Native handles never escape.</summary>
public interface IReadOnlyRegion
{
    CoordinateFrame Frame {get;} ImageFrameAddress SourceFrame {get;}
    IRegionReadLease AcquireReader();
}
/// <summary>Independent owner/frame retain. Runs are ordered/nonoverlapping. ReadRuns returns at most maximumCount,
/// with maximumCount in [1,4096], offset in [0,RunCount]; at RunCount returns empty.
/// Data is borrowed until disposal; disposed reads reject; Dispose is idempotent.</summary>
public interface IRegionReadLease : IDisposable
{
    CoordinateFrame Frame {get;} ImageFrameAddress SourceFrame {get;}
    bool IsEmpty {get;} int RunCount {get;}
    ReadOnlyMemory<RegionRun> ReadRuns(int offset,int maximumCount);
}
public interface IOutputResources
{
    IReadOnlyImage CreateGray8(int width,int height,ReadOnlySpan<byte> pixels);
    IReadOnlyImage RetainInput(IReadOnlyImage borrowedInput);
    /// <summary>Copies pixels and retains the explicit same-frame actual Domain. No dimensions-based inference.</summary>
    IReadOnlyImage CreateImage(ImageLayout layout,ImageFrameAddress frame,IReadOnlyRegion effectiveDomain,ReadOnlySpan<byte> pixels);
    IReadOnlyRegion CreateRegion(CoordinateFrame frame,ImageFrameAddress sourceFrame,ReadOnlySpan<RegionRun> runs);
    IReadOnlyRegion RetainRegion(IReadOnlyRegion borrowedRegion);
}

/// <summary>Admission checks; native implementations remain responsible for same-owner freezing and leases.</summary>
public static class ResourceContractRules
{
    public const int MaximumRegionBatch=4096;
    public static void ValidateImage(IReadOnlyImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if(image.Layout is null || image.Frame is null || image.EffectiveDomain is null ||
            image.Width!=image.Layout.Width || image.Height!=image.Layout.Height || image.EffectiveDomain.SourceFrame!=image.Frame)
            throw new ArgumentException("Image layout, physical frame and actual Domain must agree.",nameof(image));
        ValidateRegion(image.EffectiveDomain);
    }
    public static void ValidateRegion(IReadOnlyRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if(region.Frame is null || region.SourceFrame is null || region.Frame.Domain!=CoordinateDomain.Image || region.Frame.Unit!=CoordinateUnit.Pixel)
            throw new ArgumentException("Pixel regions require image/pixel coordinates and physical provenance.",nameof(region));
    }
    public static void ValidateRuns(ReadOnlySpan<RegionRun> runs)
    {
        for(int i=0;i<runs.Length;i++)
        {
            var run=runs[i];
            if(run.Row<0 || run.FirstColumn<0 || run.LastColumnInclusive<run.FirstColumn)
                throw new ArgumentException("Invalid region run.",nameof(runs));
            if(i>0 && (run.Row<runs[i-1].Row || run.Row==runs[i-1].Row && run.FirstColumn<=runs[i-1].LastColumnInclusive))
                throw new ArgumentException("Region runs must be ordered and nonoverlapping.",nameof(runs));
        }
    }
    public static void ValidateBatch(int offset,int maximumCount,int runCount)
    {
        if(runCount<0)throw new ArgumentOutOfRangeException(nameof(runCount));
        if(offset<0 || offset>runCount)throw new ArgumentOutOfRangeException(nameof(offset));
        if(maximumCount<=0 || maximumCount>MaximumRegionBatch)throw new ArgumentOutOfRangeException(nameof(maximumCount));
    }
}
