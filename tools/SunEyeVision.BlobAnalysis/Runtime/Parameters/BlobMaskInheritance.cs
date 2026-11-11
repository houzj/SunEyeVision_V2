using System.Globalization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blind-region subscription uses one comma-delimited coordinate string per polygon.</summary>
public static class BlobMaskInheritance
{
    public const string ModeId = "input.roi.mask.inherit";
    public const string SourceId = "input.roi.mask.polygon-strings";
    internal const int MaximumVertices = 32;
    private const int MaximumPayloadLength = 1023;
    public static TypeRef PolygonStrings { get; } = TypeRef.SequenceOf(DataType.Text);
    public static TypeRef Polygons { get; } = TypeRef.SequenceOf(TypeRef.GeometryOf(GeometryKind.Polygon, RoiDeclarations.ImageFrame));
    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(ModeId, DataType.Boolean, false, EditModes:FieldEditModes.Constant),
        new Port(SourceId, PolygonStrings, false, EditModes:FieldEditModes.Reference)
    });
    public static bool IsInherited(ParameterSetting setting) => setting switch
    {
        ConstantSetting {Value:bool enabled} => enabled,
        UnsetSetting => false,
        _ => throw new ArgumentException("屏蔽区创建方式必须是固定值。")
    };

    public static RecordValue? Materialize(InputValues inputs, RecordValue? roi)
    {
        if (!inputs.Contains(ModeId) || !inputs.Get<bool>(ModeId)) return roi;
        var source = inputs.Get<SequenceValue>(SourceId);
        if (source.Count > BlobLocalRoi.MaximumInheritedCount)
            throw new ArgumentException($"继承屏蔽区最多支持 {BlobLocalRoi.MaximumInheritedCount} 个多边形。");
        var rows = source.Cast<string>().Select((polygon,index) =>
        {
            var id = new Guid(0x00a9ef00 + index,0x6985,0x4e44,0xa8,0x82,0xf9,0x16,0x75,0x2a,0x53,0x91);
            var vertices = new SequenceValue(TypeRef.SequenceOf(BlobLocalRoi.PointType), ReadVertices(polygon, index + 1));
            var shape = new RecordValue(BlobLocalRoi.ShapeType,new Dictionary<string,object?> {["kind"]="polygon",["vertices"]=vertices});
            BlobLocalRoi.ValidateShape(shape);
            return BlobLocalRoi.Item(id,"继承屏蔽区"+(index+1),shape);
        }).ToArray();
        var inspection = (roi ?? BlobLocalRoi.Empty).Get<RecordValue>(BlobLocalRoi.Inspection);
        return BlobLocalRoi.Root(inspection,BlobLocalRoi.Group(rows.Length==0?BlobLocalRoi.None:BlobLocalRoi.Explicit,rows));
    }

    private static IEnumerable<RecordValue> ReadVertices(string polygon, int index)
    {
        if (polygon.Length > MaximumPayloadLength) throw new ArgumentException($"继承屏蔽区 {index} 的坐标文本超过长度上限。");
        var coordinates = polygon.Split(',');
        if (coordinates.Length < 6 || coordinates.Length > MaximumVertices * 2 || coordinates.Length % 2 != 0)
            throw new ArgumentException($"继承屏蔽区 {index} 需要 3 至 {MaximumVertices} 对坐标。");
        for (var i = 0; i < coordinates.Length; i += 2)
            yield return BlobLocalRoi.Point(Parse(coordinates[i]), Parse(coordinates[i + 1]));

        float Parse(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && float.IsFinite((float)number)) return (float)number;
            throw new ArgumentException($"继承屏蔽区 {index} 包含无效坐标。");
        }
    }

    internal static string Encode(PolygonGeometry polygon)
    {
        var payload = string.Join(",", polygon.Vertices.SelectMany(point => new[] { Format(point.X), Format(point.Y) }));
        if (payload.Length > MaximumPayloadLength) throw new ArgumentException("屏蔽区坐标文本超过订阅长度上限。");
        return payload;

        static string Format(double coordinate)
        {
            var value = (float)coordinate;
            if (!float.IsFinite(value)) throw new ArgumentException("屏蔽区坐标超出单精度范围。");
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }

    internal static SequenceValue Publish(RecordValue? roi, RecordValue? pose)
    {
        var rows = BlobLocalRoi.Items(roi ?? BlobLocalRoi.Empty,BlobLocalRoi.Mask);
        var matrix = pose is null ? null : new[]{"m11","m12","m21","m22","offset-x","offset-y"}.Select(pose.Get<double>).ToArray();
        Point2 Point(RecordValue value)
        {
            var x = value.Get<double>("x");var y = value.Get<double>("y");
            return matrix is null ? new(x,y) : new(x*matrix[0]+y*matrix[2]+matrix[4],x*matrix[1]+y*matrix[3]+matrix[5]);
        }
        return new SequenceValue(Polygons,rows.Where(row=>row.Get<bool>("enabled"))
            .Select(row=>row.Get<RecordValue>("shape")).Where(shape=>shape.Get<string>("kind")=="polygon")
            .Select(shape=>(object?)new PolygonGeometry(BlobSchema.ImageFrame,shape.Get<SequenceValue>("vertices").Cast<RecordValue>().Select(Point))));
    }
}
