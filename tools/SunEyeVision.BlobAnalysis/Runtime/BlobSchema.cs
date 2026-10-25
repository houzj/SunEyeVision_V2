using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Existing stable output IDs and Contracts records, without SDK reflection or visual types.</summary>
public static class BlobSchema
{
    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;
    public static string Field(int number) => "field:" + Guid.Parse("8ca10000-0000-4000-8000-" + number.ToString("D12", System.Globalization.CultureInfo.InvariantCulture)).ToString("N");
    public static string BlobsPort => Field(100);
    public static string RegionPort => Field(101);
    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef BoundsType { get; } = TypeRef.RecordOf("blob.bounds", new[] { new Port("x", DataType.Number), new Port("y", DataType.Number), new Port("width", DataType.Number), new Port("height", DataType.Number) });
    public static TypeRef BoundaryType { get; } = SpatialDeclarations.Boundary(ImageFrame);
    public static TypeRef BlobType { get; } = TypeRef.RecordOf("blob.result", new[] {
        new Port("element-id", DataType.Integer), new Port("label", DataType.Integer), new Port("area", DataType.Number), new Port("perimeter", DataType.Number),
        new Port("centroid-x", DataType.Number), new Port("centroid-y", DataType.Number), new Port("centroid", PointType),
        new Port("circularity", DataType.Number), new Port("rectangularity", DataType.Number), new Port("bounds", BoundsType),
        new Port("rotated-bounds", TypeRef.SequenceOf(PointType, CountConstraint.Exactly(4)), false),
        new Port("contour", TypeRef.SequenceOf(PointType), false), new Port("contour-lossy", DataType.Boolean, false), new Port("judgment-passed", DataType.Boolean),
        new Port("boundary", BoundaryType, false), new Port("region", TypeRef.ResourceOf(ResourceKind.Region)) });
    public static TypeRef BlobsType { get; } = TypeRef.SequenceOf(BlobType);
    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[] {
        new Port(Field(4), DataType.Boolean),
        new Port(Field(5), TypeRef.ResourceOf(ResourceKind.Image), Nullable:true),
        new Port(Field(6), TypeRef.ResourceOf(ResourceKind.Image), Nullable:true),
        new Port(Field(8), DataType.Integer), new Port(Field(9), DataType.Integer), new Port(Field(10), DataType.Text),
        new Port(Field(11), DataType.Integer), new Port(Field(12), DataType.Number), new Port(Field(13), DataType.Number),
        new Port(Field(14), DataType.Number), new Port(Field(15), DataType.Number), new Port(Field(16), DataType.Number),
        new Port(Field(17), DataType.NumberArray), new Port(Field(18), DataType.NumberArray),
        new Port(Field(19), DataType.NumberArray), new Port(Field(20), DataType.NumberArray),
        new Port(Field(21), DataType.NumberArray), new Port(Field(22), DataType.NumberArray),
        new Port(Field(23), DataType.NumberArray), new Port(Field(24), DataType.NumberArray),
        new Port(Field(25), DataType.NumberArray), new Port(Field(26), DataType.NumberArray),
        new Port(Field(27), TypeRef.SequenceOf((TypeRef)DataType.Text)),
        new Port(Field(30),DataType.Number),new Port(Field(31),DataType.Number),
        new Port(BlobsPort, BlobsType), new Port(RegionPort, TypeRef.ResourceOf(ResourceKind.Region))
    });

    public static RecordValue Freeze(BlobMeasurement blob)
    {
        var values = new Dictionary<string, object?> {
            ["element-id"] = blob.ElementId, ["label"] = (long)blob.Label, ["area"] = blob.Area, ["perimeter"] = blob.Perimeter,
            ["centroid-x"] = blob.CentroidX, ["centroid-y"] = blob.CentroidY, ["centroid"] = new PointGeometry(ImageFrame, new(blob.CentroidX, blob.CentroidY)),
            ["circularity"] = blob.Circularity, ["rectangularity"] = blob.Rectangularity,
            ["bounds"] = new RecordValue(BoundsType, new Dictionary<string, object?> { ["x"] = blob.Bounds.X, ["y"] = blob.Bounds.Y, ["width"] = blob.Bounds.Width, ["height"] = blob.Bounds.Height }),
            ["judgment-passed"] = blob.JudgmentPassed, ["region"] = blob.Region,
            ["rotated-bounds"] = new SequenceValue(TypeRef.SequenceOf(PointType, CountConstraint.Exactly(4)), blob.RotatedBounds.Select(p => (object?)new PointGeometry(ImageFrame, p)))
        };
        if (blob.Boundary.Count != 0)
        {
            values["contour-lossy"]=blob.Boundary.Count!=1;
            // Explicit legacy projection: one unsmoothed outer path. Complete paths/holes remain in boundary.
            if(blob.Boundary.FirstOrDefault(p=>p.Role=="outer"&&p.ParentId is null) is { } outer)
                values["contour"]=new SequenceValue(TypeRef.SequenceOf(PointType),outer.Points.Select(p=>(object?)new PointGeometry(ImageFrame,p)));
            var pathType = BoundaryType.Fields.Single(x => x.Id == "paths").Type.Element!;
            var paths = blob.Boundary.Select(path => {
                var fields = new Dictionary<string, object?> { ["path-id"] = path.Id, ["role"] = path.Role, ["closed"] = true,
                    ["points"] = new SequenceValue(TypeRef.SequenceOf(PointType), path.Points.Select(p => (object?)new PointGeometry(ImageFrame, p))) };
                if (path.ParentId is { } parent) fields["parent-path-id"] = parent;
                return (object?)new RecordValue(pathType, fields);
            });
            var boundary = new RecordValue(BoundaryType, new Dictionary<string, object?> { ["paths"] = new SequenceValue(TypeRef.SequenceOf(pathType), paths) });
            SpatialDeclarations.ValidateBoundary(boundary, ImageFrame);
            values["boundary"] = boundary;
        }
        return new(BlobType, values);
    }
}
