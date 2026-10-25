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
        new Port("contour", TypeRef.SequenceOf(PointType), false), new Port("judgment-passed", DataType.Boolean),
        new Port("boundary", BoundaryType, false), new Port("region", TypeRef.ResourceOf(ResourceKind.Region)) });
    public static TypeRef BlobsType { get; } = TypeRef.SequenceOf(BlobType);

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
