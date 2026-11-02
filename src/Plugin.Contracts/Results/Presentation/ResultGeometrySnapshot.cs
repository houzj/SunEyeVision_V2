namespace SunEyeVision.Plugin.Contracts.Results.Presentation;

/// <summary>Optional non-subscribable geometry produced by the same successful invocation as the tool output.</summary>
public interface IToolResultGeometryOutput
{
    ResultGeometrySnapshot ResultGeometry { get; }
}
public enum ResultGeometryRole { Inspection, Mask, Annulus }
public enum ResultGeometryCoverage { None, FullDomain, Explicit, Unavailable }
public sealed class ResultGeometryGroup
{
    public ResultGeometryGroup(ResultGeometryRole role,ResultGeometryCoverage coverage,IEnumerable<RecordValue> shapes)
    {
        if(!Enum.IsDefined(role)||!Enum.IsDefined(coverage))throw new ArgumentException("Unknown result geometry role or coverage.");
        Role=role;Coverage=coverage;
        var frozen=shapes.Select(shape=>shape.Declaration==RoiDeclarations.Shape
            ?new RecordValue(RoiDeclarations.Shape,shape.CopyFields()):throw new ArgumentException("Result shape declaration differs.")).ToArray();
        if(coverage!=ResultGeometryCoverage.Explicit&&frozen.Length!=0)throw new ArgumentException("Non-explicit coverage cannot contain nominal shapes.");
        foreach(var shape in frozen)Validate(shape);
        Shapes=Array.AsReadOnly(frozen);
    }
    private static void Validate(RecordValue shape)
    {
        double Number(string id){var value=shape.Get<double>(id);if(!double.IsFinite(value))throw new ArgumentException("Result geometry must be finite.");return value;}
        void Point(PointGeometry point){if(point.Frame!=RoiDeclarations.ImageFrame||!double.IsFinite(point.Point.X)||!double.IsFinite(point.Point.Y))throw new ArgumentException("Result geometry point differs from pixel declaration.");}
        var kind=shape.Get<string>("kind");
        if(kind=="polygon")
        {
            var points=shape.Get<SequenceValue>("vertices").Cast<PointGeometry>().ToArray();
            foreach(var point in points)Point(point);
            if(points.Select(point=>point.Point).Distinct().Count()<3||!shape.Get<bool>("closed"))throw new ArgumentException("Result polygon is incomplete.");
            return;
        }
        Point(shape.Get<PointGeometry>("center"));
        if(kind=="rectangle")
        {if(Number("width")/2<=0||Number("height")/2<=0)throw new ArgumentException("Result rectangle size is invalid.");_=Number("angle-degrees");return;}
        if(kind is not ("annulus" or "sector"))throw new ArgumentException("Unsupported result geometry kind.");
        if(Number("inner-radius")<0||Number("outer-radius")<=Number("inner-radius"))throw new ArgumentException("Result annulus radii are invalid.");
        var sweep=Number("end-radians")-Number("start-radians");
        if(!double.IsFinite(sweep)||Math.Abs(sweep)<1e-12||Math.Abs(sweep)>2*Math.PI+1e-9||kind=="sector"&&sweep<=0)throw new ArgumentException("Result annulus sweep is invalid.");
    }
    public ResultGeometryRole Role {get;}
    public ResultGeometryCoverage Coverage {get;}
    public IReadOnlyList<RecordValue> Shapes {get;}
}
/// <summary>Immutable pure values; the enclosing publication controls authority and resource lifetime.</summary>
public sealed class ResultGeometrySnapshot
{
    public ResultGeometrySnapshot(ImageFrameAddress sourceFrame,CoordinateFrame coordinates,bool passed,IEnumerable<ResultGeometryGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);ArgumentNullException.ThrowIfNull(coordinates);
        SourceFrame=sourceFrame;Coordinates=coordinates;Passed=passed;
        var frozen=groups.Select(group=>new ResultGeometryGroup(group.Role,group.Coverage,group.Shapes)).ToArray();
        if(frozen.Select(group=>group.Role).Distinct().Count()!=frozen.Length)throw new ArgumentException("Duplicate result geometry role.");
        if(frozen.Any(group=>group.Shapes.Count!=0)&&coordinates!=RoiDeclarations.ImageFrame)throw new ArgumentException("Nominal shape coordinate declaration differs from the snapshot.");
        Groups=Array.AsReadOnly(frozen.OrderBy(group=>group.Role).ToArray());
    }
    public ImageFrameAddress SourceFrame {get;}
    public CoordinateFrame Coordinates {get;}
    public bool Passed {get;}
    public IReadOnlyList<ResultGeometryGroup> Groups {get;}
}
