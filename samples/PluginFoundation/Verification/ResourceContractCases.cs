using SunEyeVision.Plugin.Contracts;

/// <summary>Focused S1 contract counterexamples. Parent verification runner explicitly invokes Run; no native/runtime proof.</summary>
internal static class ResourceContractCases
{
    public static void Run()
    {
        Reject(()=>new ImageLayout(2,1,5,PixelLayout.Bgr24));
        Reject(()=>new ImageLayout(int.MaxValue,2,int.MaxValue,PixelLayout.Bgra32));
        var padded=new ImageLayout(2,3,8,PixelLayout.Bgr24);
        Require(padded.BufferLength==24,"padded layout lost stride");
        ResourceContractRules.ValidateRuns(new[]{new RegionRun(0,0,2),new RegionRun(0,3,4)});
        Reject(()=>ResourceContractRules.ValidateRuns(new[]{new RegionRun(0,0,2),new RegionRun(0,2,4)}));
        Reject(()=>ResourceContractRules.ValidateBatch(0,4097,1));
        Reject(()=>ResourceContractRules.ValidateBatch(2,1,1));
        Reject(()=>new RegionRun(-1,0,1));
        Reject(()=>new RegionRun(0,-1,1));
        Reject(()=>ResourceContractRules.ValidateBatch(0,1,-1));
        ResourceContractRules.ValidateBatch(0,1,0);
        var logical=new CoordinateFrame("image:input",CoordinateDomain.Image,CoordinateUnit.Pixel);
        var frame=new ImageFrameAddress(Guid.NewGuid(),"input");
        var region=new EmptyRegion(logical,frame);
        var values=new InputValues(new[]{new Port("r",TypeRef.ResourceOf(ResourceKind.Region))},new Dictionary<string,object?>{{"r",region}});
        using(var reader=values.Read("r").AcquireRegionReader())Require(reader.IsEmpty && reader.ReadRuns(0,1).Length==0,"valid empty rejected");
        Require(values.Regions().Single()==region,"region ownership enumeration missing");
        Reject(()=>ValueRules.Freeze(new Port("r",TypeRef.ResourceOf(ResourceKind.Region)),new object()));
        var affine=new Dictionary<string,object?>{{"m11",1d},{"m12",0d},{"m21",0d},{"m22",1d},
            {"offset-x",2d},{"offset-y",3d},{"from","image:input"},{"to","image:output"}};
        SpatialDeclarations.ValidateAffine2D(new(SpatialDeclarations.Affine2D,affine));
        affine["m22"]=0d;
        Reject(()=>SpatialDeclarations.ValidateAffine2D(new(SpatialDeclarations.Affine2D,affine)));
        var boundary=SpatialDeclarations.Boundary(logical);
        SpatialDeclarations.ValidateBoundary(new(boundary,new Dictionary<string,object?>{{"paths",Array.Empty<object>()}}),logical);
        var pathType=boundary.Fields.Single().Type.Element!;
        RecordValue Path(long id,long? parent,string role)
        {
            var fields=new Dictionary<string,object?>{{"path-id",id},{"role",role},{"closed",true},
                {"points",new object[]{new PointGeometry(logical,new(0,0)),new PointGeometry(logical,new(4,0)),new PointGeometry(logical,new(0,4))}}};
            if(parent.HasValue)fields["parent-path-id"]=parent.Value;
            return new(pathType,fields);
        }
        RecordValue Boundary(params RecordValue[] paths)=>new(boundary,new Dictionary<string,object?>{{"paths",paths}});
        SpatialDeclarations.ValidateBoundary(Boundary(Path(10,null,"outer"),Path(11,10,"hole")),logical);
        Reject(()=>SpatialDeclarations.ValidateBoundary(Boundary(Path(11,null,"hole")),logical));
        Reject(()=>SpatialDeclarations.ValidateBoundary(Boundary(Path(10,11,"outer"),Path(11,10,"hole")),logical));
        Reject(()=>SpatialDeclarations.ValidateBoundary(Boundary(Path(10,null,"outer"),Path(10,null,"outer")),logical));
        Reject(()=>SpatialDeclarations.ValidateBoundary(Boundary(Path(11,99,"hole")),logical));
    }
    private static void Require(bool valid,string reason){if(!valid)throw new InvalidOperationException(reason);}
    private static void Reject(Action action)
    {
        try{action();}catch(ArgumentException){return;}catch(OverflowException){return;}
        throw new InvalidOperationException("Expected contract rejection.");
    }
    private sealed class EmptyRegion(CoordinateFrame frame,ImageFrameAddress source):IReadOnlyRegion
    {
        public CoordinateFrame Frame=>frame; public ImageFrameAddress SourceFrame=>source;
        public IRegionReadLease AcquireReader()=>new EmptyReader(frame,source);
    }
    private sealed class EmptyReader(CoordinateFrame frame,ImageFrameAddress source):IRegionReadLease
    {
        private bool disposed;
        public CoordinateFrame Frame {get{Check();return frame;}}
        public ImageFrameAddress SourceFrame {get{Check();return source;}}
        public bool IsEmpty {get{Check();return true;}}
        public int RunCount {get{Check();return 0;}}
        public ReadOnlyMemory<RegionRun> ReadRuns(int offset,int maximumCount)
        {Check();ResourceContractRules.ValidateBatch(offset,maximumCount,0);return ReadOnlyMemory<RegionRun>.Empty;}
        public void Dispose()=>disposed=true;
        private void Check(){if(disposed)throw new ObjectDisposedException(nameof(EmptyReader));}
    }
}
