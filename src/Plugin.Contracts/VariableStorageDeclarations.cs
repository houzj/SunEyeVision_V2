namespace SunEyeVision.Plugin.Contracts;

[Flags] public enum VariableStorageShapes { Scalar=1, Sequence=2 }
public enum VariablePersistenceKind { Integer, Double, String, Boolean, Binary, Transform, Roi, StoredRaster, DeclaredNullOnly }
public sealed record VariableStorageDeclaration(string Token, VariablePersistenceKind Kind, VariableStorageShapes Shapes,
    bool CanDeclareNull, bool CanPersistNonNull, bool CanTextEdit, bool CanBind, Port? ScalarPort, Port? SequencePort);

/// <summary>Persistent value declarations; runtime ROI and spatial declarations are unchanged.</summary>
public static partial class VariableValueDeclarations
{
    public static IReadOnlyDictionary<string,string> DisplayNames { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["Integer"]="整型", ["String"]="字符串", ["Boolean"]="布尔", ["Binary"]="字节序列",
            ["Double"]="双精度", ["Image"]="图像", ["ROI"]="图形", ["Transform"]="变换矩阵",
            ["Circle"]="圆", ["RotatedRect"]="旋转矩形", ["Point"]="点", ["Polygon"]="多边形",
            ["Line"]="直线", ["Annulus"]="圆环", ["Arc"]="圆弧"
        });
    private static readonly TypeRef Number=TypeRef.ScalarOf(ScalarKind.Float64);
    private static readonly TypeRef Text=TypeRef.ScalarOf(ScalarKind.Text);
    private static readonly TypeRef Boolean=TypeRef.ScalarOf(ScalarKind.Boolean);
    private static readonly TypeRef Integer=TypeRef.ScalarOf(ScalarKind.Int64);
    public static TypeRef StoredRoiPoint { get; }=TypeRef.RecordOf("stored.roi.point",new[]{new Port("x",Number),new Port("y",Number)});
    public static TypeRef StoredRoiShape { get; }=TypeRef.RecordOf("stored.roi.shape",new[]{
        new Port("kind",Text),new Port("center",StoredRoiPoint,false),new Port("direction-x",Number,false),new Port("direction-y",Number,false),
        new Port("radius",Number,false),new Port("inner-radius",Number,false),new Port("outer-radius",Number,false),
        new Port("width",Number,false),new Port("height",Number,false),new Port("angle-degrees",Number,false),
        new Port("start-radians",Number,false),new Port("end-radians",Number,false),new Port("major-arc",Boolean,false),
        new Port("vertices",TypeRef.SequenceOf(StoredRoiPoint),false),new Port("closed",Boolean,false),
        new Port("fill-color-argb",Integer,Storage:NumericStorage.UInt32),new Port("stroke-color-argb",Integer,Storage:NumericStorage.UInt32),
        new Port("stroke-thickness",Number),new Port("opacity",Number)});
    public static TypeRef StoredRoiSequence { get; }=TypeRef.SequenceOf(StoredRoiShape);
    public static TypeRef StoredTransform { get; }=TypeRef.RecordOf("stored.affine2d",new[]{
        new Port("m11",Number),new Port("m12",Number),new Port("m21",Number),new Port("m22",Number),new Port("offset-x",Number),new Port("offset-y",Number)});

    public static bool TryGetStorageDeclaration(string token,out VariableStorageDeclaration declaration)
    {
        declaration=null!;
        if(string.IsNullOrWhiteSpace(token))return false;
        var sequence=token.EndsWith("[]",StringComparison.Ordinal);
        var name=sequence?token[..^2]:token;
        TryGetPort(name,out var scalarPort);TryGetPort(name+"[]",out var sequencePort);
        var kind=name switch { "Binary"=>VariablePersistenceKind.Binary,"Transform"=>VariablePersistenceKind.Transform,
            "ROI"=>VariablePersistenceKind.Roi,"Image"=>VariablePersistenceKind.StoredRaster,
            "Circle" or "RotatedRect" or "Point" or "Polygon" or "Line" or "Annulus" or "Arc" or "Float"=>VariablePersistenceKind.DeclaredNullOnly,
            _ when scalarPort?.Type.Scalar==ScalarKind.Int64 && name=="Integer"=>VariablePersistenceKind.Integer,
            _ when scalarPort?.Type.Scalar==ScalarKind.Float64 && name=="Double"=>VariablePersistenceKind.Double,
            _ when scalarPort?.Type.Scalar==ScalarKind.Text && name=="String"=>VariablePersistenceKind.String,
            _ when scalarPort?.Type.Scalar==ScalarKind.Boolean && name=="Boolean"=>VariablePersistenceKind.Boolean,
            _=>(VariablePersistenceKind?)null };
        if(kind is null || sequence && name is not ("Integer" or "Double" or "String" or "Boolean" or "Float"))return false;
        var pure=kind is VariablePersistenceKind.Integer or VariablePersistenceKind.Double or VariablePersistenceKind.String or VariablePersistenceKind.Boolean;
        var shapes=sequence?VariableStorageShapes.Sequence:kind switch { VariablePersistenceKind.Roi=>VariableStorageShapes.Sequence,
            VariablePersistenceKind.StoredRaster or VariablePersistenceKind.DeclaredNullOnly=>VariableStorageShapes.Scalar,
            _=>VariableStorageShapes.Scalar|VariableStorageShapes.Sequence };
        declaration=new(token,kind.Value,shapes,true,kind!=VariablePersistenceKind.DeclaredNullOnly,pure,pure,scalarPort,sequencePort);
        return true;
    }

    public static void ValidateStoredTransform(RecordValue value)
    { if(value.Declaration!=StoredTransform)throw new ArgumentException("Stored transform declaration differs."); }

    public static void ValidateStoredRoi(RecordValue value)
    {
        if(value.Declaration!=StoredRoiShape)throw new ArgumentException("Stored ROI declaration differs.");
        var required=value.Get<string>("kind") switch {
            "none"=>Array.Empty<string>(),"point"=>new[]{"center"},"circle"=>new[]{"center","radius"},
            "rectangle"=>new[]{"center","width","height","angle-degrees"},"line"=>new[]{"center","direction-x","direction-y"},
            "annulus"=>new[]{"center","inner-radius","outer-radius","start-radians","end-radians"},
            "arc"=>new[]{"center","radius","start-radians","end-radians","major-arc"},"polygon"=>new[]{"vertices","closed"},
            _=>throw new ArgumentException("Unknown stored ROI kind.") };
        var allowed=required.Concat(new[]{"kind","fill-color-argb","stroke-color-argb","stroke-thickness","opacity"}).ToHashSet(StringComparer.Ordinal);
        if(required.Any(key=>!value.Contains(key))||value.CopyFields().Keys.Any(key=>!allowed.Contains(key)))throw new ArgumentException("Stored ROI fields differ from its kind.");
    }
}
