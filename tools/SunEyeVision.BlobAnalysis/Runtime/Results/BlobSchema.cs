using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Existing stable output IDs and Contracts records, without SDK reflection or visual types.</summary>
public static class BlobSchema
{
    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;
    public static string Field(int number) => "field:" + Guid.Parse("8ca10000-0000-4000-8000-" + number.ToString("D12", System.Globalization.CultureInfo.InvariantCulture)).ToString("N");
    public static string BlobsPort => Field(100);
    public static string RegionPort => Field(101);
    public static string MaskImagePort => Field(103);
    public static string InnerRectanglesPort => Field(104);
    public static string MaskPolygonsPort => Field(105);
    public static string MaskPolygonStringsPort => Field(124);
    public static string DetectionRectanglesPort => Field(106);
    public static string DetectionAnnuliPort => Field(107);
    public static string ContourPointsPort => Field(108);
    public static string ModuleStatusPort => Field(4);
    public static string ModuleRunTimePort => Field(123);
    public static string BlobStatusesPort => Field(109);
    public static string MinGrayValuesPort => Field(110);
    public static string MaxGrayValuesPort => Field(111);
    public static string GrayContrastsPort => Field(112);
    public static string PrincipalAnglesPort => Field(113);
    public static string OuterRectanglesPort => Field(114);
    public static string LongAxesPort => Field(115);
    public static string ShortAxesPort => Field(116);
    public static string CentroidPointsPort => Field(117);
    public static string AxisAlignedRectanglesPort => Field(118);
    public static string RectangleTopLeftXPort => Field(119);
    public static string RectangleTopLeftYPort => Field(120);
    public static string RectangleBottomRightXPort => Field(121);
    public static string RectangleBottomRightYPort => Field(122);
    public static TypeRef CentroidPointsType { get; } = TypeRef.SequenceOf(FixtureDeclarations.Point);
    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef BoundsType { get; } = TypeRef.RecordOf("blob.bounds", new[]
    {
        new Port("origin", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage:NumericStorage.Float32),
        new Port("height", DataType.Number, Storage:NumericStorage.Float32)
    });
    public static TypeRef AxisAlignedRectanglesType { get; } = TypeRef.SequenceOf(BoundsType);
    public static TypeRef OrientedRectanglesType { get; } = TypeRef.SequenceOf(BlobRoiInheritance.RectangleType);
    public static TypeRef BoundaryType { get; } = SpatialDeclarations.Boundary(ImageFrame);
    public static TypeRef BlobType { get; } = TypeRef.RecordOf("blob.result", new[] {
        new Port("element-id", DataType.Integer), new Port("label", DataType.Integer), new Port("area", DataType.Number), new Port("perimeter", DataType.Number),
        new Port("centroid-x", DataType.Number), new Port("centroid-y", DataType.Number), new Port("centroid", PointType),
        new Port("circularity", DataType.Number), new Port("rectangularity", DataType.Number), new Port("bounds", BoundsType),
        new Port("oriented-bounds",BlobRoiInheritance.RectangleType), new Port("long-axis", DataType.Number), new Port("short-axis", DataType.Number),
        new Port("rotated-bounds", TypeRef.SequenceOf(PointType, CountConstraint.Exactly(4)), false),
        new Port("contour", TypeRef.SequenceOf(PointType), false), new Port("contour-lossy", DataType.Boolean, false), new Port("judgment-passed", DataType.Boolean),
        new Port("boundary", BoundaryType, false), new Port("outline-points", TypeRef.SequenceOf(PointType), false),
        new Port("inner-rectangle", TypeRef.GeometryOf(GeometryKind.OrientedRectangle, ImageFrame), false),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region)) });
    public static TypeRef BlobsType { get; } = TypeRef.SequenceOf(BlobType);
    private static TypeRef ImageRecord(string name)=>TypeRef.RecordOf(name,new[]{
        new Port("data",TypeRef.ResourceOf(ResourceKind.Image)),
        new Port("width",DataType.Integer,Storage:NumericStorage.Int32),
        new Port("height",DataType.Integer,Storage:NumericStorage.Int32),
        new Port("pixel-format",DataType.Integer,Storage:NumericStorage.Int32)});
    public static TypeRef BlobImageType {get;}=ImageRecord("blob.image");
    public static TypeRef BinaryImageType {get;}=ImageRecord("blob.binary-image");
    public static TypeRef MaskImageType {get;}=ImageRecord("blob.mask-image");
    public static TypeRef ContourInfoType { get; } = TypeRef.RecordOf("blob.contour-info", new[]
    {
        new Port("x", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("y", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("score", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("blob-index", TypeRef.SequenceOf(DataType.Integer), Storage:NumericStorage.Int32)
    });
    public static TypeRef ContourPointsType { get; } = TypeRef.RecordOf("blob.contour-points", new[]
    {
        new Port("info", ContourInfoType, Nullable:true),
        new Port("count", DataType.Integer, Storage:NumericStorage.Int32)
    });
    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[] {
        new Port(ModuleStatusPort, DataType.Integer, Storage:NumericStorage.Int32),
        new Port(ModuleRunTimePort, DataType.Number, Storage:NumericStorage.Float32),
        new Port(BlobStatusesPort, TypeRef.SequenceOf(DataType.Integer), Storage:NumericStorage.Int32),
        new Port(Field(5), BlobImageType, Nullable:true),
        new Port(Field(6), BinaryImageType, Nullable:true),
        new Port(Field(8), DataType.Integer, Nullable:true), new Port(Field(9), DataType.Integer, Nullable:true), new Port(Field(10), DataType.Text), new Port(Field(102), DataType.Text),
        new Port(Field(11), DataType.Integer, Storage:NumericStorage.Int32),
        new Port(Field(12), DataType.Number, Storage:NumericStorage.Float32), new Port(Field(13), DataType.Number),
        new Port(Field(14), DataType.Number), new Port(Field(15), DataType.Number), new Port(Field(16), DataType.Number),
        new Port(Field(17), DataType.NumberArray), new Port(Field(18), DataType.NumberArray),
        new Port(Field(19), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(20), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(CentroidPointsPort, CentroidPointsType),
        new Port(Field(23), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(24), DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(AxisAlignedRectanglesPort, AxisAlignedRectanglesType),
        new Port(RectangleTopLeftXPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(RectangleTopLeftYPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(RectangleBottomRightXPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(RectangleBottomRightYPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Field(27), TypeRef.SequenceOf((TypeRef)DataType.Text)),
        new Port(Field(30),DataType.Number),new Port(Field(31),DataType.Number),
        new Port(BlobsPort, BlobsType), new Port(RegionPort, TypeRef.ResourceOf(ResourceKind.Region)),
        new Port(MaskImagePort, MaskImageType, Nullable:true), new Port(InnerRectanglesPort, OrientedRectanglesType),
        new Port(MaskPolygonsPort, BlobMaskInheritance.Polygons),
        new Port(MaskPolygonStringsPort, BlobMaskInheritance.PolygonStrings),
        new Port(DetectionRectanglesPort, OrientedRectanglesType, Nullable:true),
        new Port(DetectionAnnuliPort, TypeRef.SequenceOf(BlobRoiInheritance.AnnulusType), Nullable:true),
        new Port(ContourPointsPort, ContourPointsType, Nullable:true),
        new Port(MinGrayValuesPort, DataType.NumberArray, Nullable:true, Storage:NumericStorage.Float32),
        new Port(MaxGrayValuesPort, DataType.NumberArray, Nullable:true, Storage:NumericStorage.Float32),
        new Port(GrayContrastsPort, DataType.NumberArray, Nullable:true, Storage:NumericStorage.Float32),
        new Port(PrincipalAnglesPort, DataType.NumberArray, Nullable:true, Storage:NumericStorage.Float32),
        new Port(OuterRectanglesPort, OrientedRectanglesType),
        new Port(LongAxesPort, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(ShortAxesPort, DataType.NumberArray, Storage:NumericStorage.Float32)
    });

    public static IReadOnlyList<OutputDisplayNode> OutputDisplay {get;}=BlobOutputDisplay.Create();

    internal static RecordValue FreezePoint(Point2 point) => new(FixtureDeclarations.Point,
        new Dictionary<string, object?> { ["x"] = (double)(float)point.X, ["y"] = (double)(float)point.Y });

    internal static RecordValue FreezeAxisAlignedBounds(BlobBounds bounds) => new(BoundsType,
        new Dictionary<string, object?>
        {
            ["origin"] = FreezePoint(new(bounds.X, bounds.Y)),
            ["width"] = (double)(float)bounds.Width,
            ["height"] = (double)(float)bounds.Height
        });

    internal static RecordValue FreezeBounds(BlobOrientedBounds bounds) => new(BlobRoiInheritance.RectangleType,
        new Dictionary<string, object?>
        {
            ["center"] = FreezePoint(new(bounds.CenterX,bounds.CenterY)),
            ["width"] = (double)(float)bounds.Width, ["height"] = (double)(float)bounds.Height,
            ["angle-degrees"] = (double)(float)bounds.AngleDegrees
        });

    internal static RecordValue FreezeDetectionAnnulus(BlobAnnulus annulus) => new(BlobRoiInheritance.AnnulusType,
        new Dictionary<string, object?>
        {
            ["center"] = FreezePoint(new(annulus.CenterX,annulus.CenterY)),
            ["inner-radius"] = (double)(float)annulus.InnerRadius, ["outer-radius"] = (double)(float)annulus.OuterRadius,
            ["start-degrees"] = (double)(float)annulus.StartAngle, ["sweep-degrees"] = (double)(float)annulus.AngleRange
        });

    internal static IEnumerable<Point2> OutlinePoints(BlobMeasurement blob) =>
        blob.UnorderedContour ?? blob.Boundary.SelectMany(path => path.Points);

    public static RecordValue Freeze(BlobMeasurement blob, bool showOutline = true, CancellationToken cancellation = default)
    {
        var values = new Dictionary<string, object?> {
            ["element-id"] = blob.ElementId, ["label"] = (long)blob.Label, ["area"] = blob.Area, ["perimeter"] = blob.Perimeter,
            ["centroid-x"] = blob.CentroidX, ["centroid-y"] = blob.CentroidY, ["centroid"] = new PointGeometry(ImageFrame, new(blob.CentroidX, blob.CentroidY)),
            ["circularity"] = blob.Circularity, ["rectangularity"] = blob.Rectangularity,
            ["bounds"] = FreezeAxisAlignedBounds(blob.Bounds),
            ["oriented-bounds"] = FreezeBounds(blob.OrientedBounds),
            ["long-axis"] = (double)BlobNumericSemantics.LongAxis(blob.OrientedBounds),
            ["short-axis"] = (double)BlobNumericSemantics.ShortAxis(blob.OrientedBounds),
            ["judgment-passed"] = blob.JudgmentPassed, ["region"] = blob.Region,
            ["rotated-bounds"] = new SequenceValue(TypeRef.SequenceOf(PointType, CountConstraint.Exactly(4)), blob.RotatedBounds.Select(p => (object?)new PointGeometry(ImageFrame, p)))
        };
        if (blob.InnerRectangle is { } rectangle)
            values["inner-rectangle"] = new OrientedRectangleGeometry(ImageFrame,
                new(rectangle.CenterX, rectangle.CenterY), rectangle.Width, rectangle.Height, rectangle.AngleDegrees * Math.PI / 180);
        SequenceValue PointSet(IEnumerable<Point2> points) => new(TypeRef.SequenceOf(PointType),
            points.Select((point, index) =>
            {
                if ((index & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                return new PointGeometry(ImageFrame, point);
            }));
        var boundaryPoints = blob.Boundary.Select(path => PointSet(path.Points)).ToArray();
        if (blob.UnorderedContour is { } unordered)
        {
            values["contour"] = PointSet(unordered);
            values["contour-lossy"] = false;
        }
        else if (blob.Boundary.Count != 0)
        {
            values["contour-lossy"] = blob.Boundary.Count != 1;
            for (var index = 0; index < blob.Boundary.Count; index++)
            {
                if (blob.Boundary[index] is not { Role: "outer", ParentId: null }) continue;
                values["contour"] = boundaryPoints[index];
                break;
            }
        }
        if (showOutline && (blob.UnorderedContour is not null || blob.Boundary.Count != 0))
            values["outline-points"] = blob.UnorderedContour is not null ? values["contour"]
                : boundaryPoints.Length == 1 ? boundaryPoints[0]
                : new SequenceValue(TypeRef.SequenceOf(PointType), boundaryPoints.SelectMany(points => points));
        if (blob.Boundary.Count != 0)
        {
            var pathType = BoundaryType.Fields.Single(x => x.Id == "paths").Type.Element!;
            var paths = blob.Boundary.Select((path, index) => {
                var fields = new Dictionary<string, object?> { ["path-id"] = path.Id, ["role"] = path.Role, ["closed"] = true,
                    ["points"] = boundaryPoints[index] };
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
