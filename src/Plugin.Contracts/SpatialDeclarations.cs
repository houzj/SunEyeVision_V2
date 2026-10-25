namespace SunEyeVision.Plugin.Contracts;

/// <summary>Approved spatial declarations expressed entirely through the existing record/sequence type system.</summary>
public static class SpatialDeclarations
{
    private static readonly string[] AffineNumbers={"m11","m12","m21","m22","offset-x","offset-y"};
    public static TypeRef Affine2D {get;}=TypeRef.RecordOf("spatial.affine2d",
        AffineNumbers.Select(id=>new Port(id,TypeRef.ScalarOf(ScalarKind.Float64)))
            .Concat(new[]{new Port("from",TypeRef.ScalarOf(ScalarKind.Text)),new Port("to",TypeRef.ScalarOf(ScalarKind.Text))}));

    /// <summary>x'=x*m11+y*m21+offset-x; y'=x*m12+y*m22+offset-y.
    /// Logical frame labels do not replace same-snapshot source frame identity checks by the host.</summary>
    public static void ValidateAffine2D(RecordValue value,string? expectedFrom=null,string? expectedTo=null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if(value.Declaration!=Affine2D)throw new ArgumentException("Expected spatial.affine2d declaration.",nameof(value));
        var from=value.Get<string>("from");var to=value.Get<string>("to");
        if(string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) ||
            expectedFrom is not null && from!=expectedFrom || expectedTo is not null && to!=expectedTo)
            throw new ArgumentException("Affine logical frame identities do not match.",nameof(value));
        foreach(var field in AffineNumbers)
            if(!double.IsFinite(value.Get<double>(field)))throw new ArgumentException("Affine coefficients must be finite.",nameof(value));
        double determinant=value.Get<double>("m11")*value.Get<double>("m22")-value.Get<double>("m12")*value.Get<double>("m21");
        if(!double.IsFinite(determinant) || determinant==0)throw new ArgumentException("Affine transform must be safely invertible.",nameof(value));
    }

    public static TypeRef Boundary(CoordinateFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var path=TypeRef.RecordOf("blob.path",new[]{
            new Port("path-id",TypeRef.ScalarOf(ScalarKind.Int64)),
            new Port("parent-path-id",TypeRef.ScalarOf(ScalarKind.Int64),Required:false),
            new Port("role",TypeRef.ScalarOf(ScalarKind.Text)),
            new Port("closed",TypeRef.ScalarOf(ScalarKind.Boolean)),
            new Port("points",TypeRef.SequenceOf(TypeRef.GeometryOf(GeometryKind.Point,frame))) });
        return TypeRef.RecordOf("blob.boundary",new[]{new Port("paths",TypeRef.SequenceOf(path))});
    }

    /// <summary>Checks complete topology without smoothing or choosing a lossy single outer contour.
    /// Identities are local to one Blob result element. Empty boundary is valid empty.</summary>
    public static void ValidateBoundary(RecordValue value,CoordinateFrame frame)
    {
        ArgumentNullException.ThrowIfNull(value);
        if(value.Declaration!=Boundary(frame))throw new ArgumentException("Expected blob.boundary declaration.",nameof(value));
        var paths=value.Get<SequenceValue>("paths").Cast<RecordValue>().ToArray();
        var indexed=new Dictionary<long,RecordValue>();
        foreach(var path in paths)
        {
            long id=path.Get<long>("path-id");
            if(!indexed.TryAdd(id,path))throw new ArgumentException("Boundary path identities must be unique.",nameof(value));
            string role=path.Get<string>("role");
            if(role!="outer" && role!="hole")throw new ArgumentException("Boundary role must be outer or hole.",nameof(value));
            // FD07 declares a sequence without a minimum; degenerate/single-pixel boundaries
            // retain their exact canonical samples. Region remains the area truth.
            if(role=="hole" && !path.Contains("parent-path-id"))throw new ArgumentException("A hole requires a parent path.",nameof(value));
        }
        foreach(var path in paths)
        {
            var seen=new HashSet<long>{path.Get<long>("path-id")};
            var current=path;
            while(current.Contains("parent-path-id"))
            {
                long parentId=current.Get<long>("parent-path-id");
                if(!seen.Add(parentId) || !indexed.TryGetValue(parentId,out var parent))
                    throw new ArgumentException("Boundary parent must exist and be acyclic.",nameof(value));
                if(parent.Get<string>("role")==current.Get<string>("role"))
                    throw new ArgumentException("Nested outer/hole roles must alternate.",nameof(value));
                current=parent;
            }
            if(current.Get<string>("role")!="outer")throw new ArgumentException("Boundary roots must be outer paths.",nameof(value));
        }
    }
}
