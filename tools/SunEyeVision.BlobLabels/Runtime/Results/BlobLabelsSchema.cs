using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// 输出与结果声明。端口顺序与原件 26 项输出一致（ModuRunTime、ModuStatus … Output Position Correction Info），
/// 另加一个仅供结果页使用的行集合端口 <see cref="Rows"/>：原件的数组输出没有逐元素身份，
/// 结果页需要同一行内的矩形/区域/轮廓，故按 Blob 工具的既有做法声明记录行。
/// </summary>
public static class BlobLabelsSchema
{
    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;
    public static string ImageInput => "input.image";
    public static string MaskInput => "input.mask";
    public static string InspectionRoiInput => "input.roi";
    public static string AnnulusRoiInput => "input.annulus-roi";
    public static string MaskRegionInput => "input.mask-region";
    public static string FixtureInput => "input.fixture";

    public const string Status = "output.status";
    public const string RunTime = "output.run-time";
    public const string BlobStatuses = "output.blob-statuses";
    public const string Count = "output.count";
    public const string Areas = "output.areas";
    public const string TotalArea = "output.total-area";
    public const string Centroids = "output.centroids";
    public const string MinimumBoundingRectangles = "output.minimum-bounding-rectangles";
    public const string BlobAngles = "output.blob-angles";
    public const string Perimeters = "output.perimeters";
    public const string LongAxes = "output.long-axes";
    public const string ShortAxes = "output.short-axes";
    public const string Circularities = "output.circularities";
    public const string Rectangularities = "output.rectangularities";
    public const string Scores = "output.scores";
    public const string BinaryImage = "output.binary-image";
    public const string BlobImage = "output.blob-image";
    public const string OutputMask = "output.mask";
    public const string InspectionRectangles = "output.inspection-rectangles";
    public const string InspectionAnnuli = "output.inspection-annuli";
    public const string ContourPoints = "output.contour-points";
    public const string ClassLabels = "output.class-labels";
    public const string LabelValues = "output.label-values";
    public const string BlindPolygon = "output.blind-polygon";
    public const string BlindPolygonString = "output.blind-polygon-string";
    public const string OutputFixture = "output.fixture";
    public const string Rows = "bloblabel.rows";

    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef BoundsType { get; } = TypeRef.RecordOf("bloblabel.bounds", new[]
    {
        new Port("origin", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage:NumericStorage.Float32),
        new Port("height", DataType.Number, Storage:NumericStorage.Float32)
    });
    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("bloblabel.rectangle", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage:NumericStorage.Float32),
        new Port("height", DataType.Number, Storage:NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage:NumericStorage.Float32)
    });
    public static TypeRef AnnulusType { get; } = TypeRef.RecordOf("bloblabel.annulus", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("inner-radius", DataType.Number, Storage:NumericStorage.Float32),
        new Port("outer-radius", DataType.Number, Storage:NumericStorage.Float32),
        new Port("start-degrees", DataType.Number, Storage:NumericStorage.Float32),
        new Port("sweep-degrees", DataType.Number, Storage:NumericStorage.Float32)
    });
    public static TypeRef ResultType { get; } = TypeRef.RecordOf("bloblabel.result", new[]
    {
        new Port("element-id", DataType.Integer),
        new Port("label", DataType.Integer),
        new Port("area", DataType.Number),
        new Port("perimeter", DataType.Number),
        new Port("centroid-x", DataType.Number),
        new Port("centroid-y", DataType.Number),
        new Port("centroid", PointType),
        new Port("circularity", DataType.Number),
        new Port("rectangularity", DataType.Number),
        new Port("bounds", BoundsType),
        new Port("oriented-bounds", RectangleType),
        new Port("long-axis", DataType.Number),
        new Port("short-axis", DataType.Number),
        new Port("contour", TypeRef.SequenceOf(PointType), false),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region)),
        new Port("judgment-passed", DataType.Boolean),
        new Port("failed-limits", TypeRef.SequenceOf(DataType.Text), false)
    });
    public static TypeRef RowsType { get; } = TypeRef.SequenceOf(ResultType);
    public static TypeRef ContourInfoType { get; } = TypeRef.RecordOf("bloblabel.contour-points", new[]
    {
        new Port("x", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("y", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("score", DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port("blob-index", TypeRef.SequenceOf(DataType.Integer), Storage:NumericStorage.Int32),
        new Port("count", DataType.Integer, Storage:NumericStorage.Int32)
    });
    private static TypeRef ImageRecord(string name) => TypeRef.RecordOf(name, new[]
    {
        new Port("data", TypeRef.ResourceOf(ResourceKind.Image)),
        new Port("width", DataType.Integer, Storage:NumericStorage.Int32),
        new Port("height", DataType.Integer, Storage:NumericStorage.Int32),
        new Port("pixel-format", DataType.Integer, Storage:NumericStorage.Int32)
    });
    public static TypeRef BlobImageType { get; } = ImageRecord("bloblabel.image");
    public static TypeRef BinaryImageType { get; } = ImageRecord("bloblabel.binary-image");

    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[]
    {
        new Port(RunTime, DataType.Number, Storage:NumericStorage.Float32),
        new Port(Status, DataType.Integer, Storage:NumericStorage.Int32),
        new Port(BlobStatuses, TypeRef.SequenceOf(DataType.Integer), Storage:NumericStorage.Int32),
        new Port(Count, DataType.Integer, Storage:NumericStorage.Int32),
        new Port(Areas, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(TotalArea, DataType.Number, Storage:NumericStorage.Float32),
        new Port(Centroids, TypeRef.SequenceOf(PointType)),
        new Port(MinimumBoundingRectangles, TypeRef.SequenceOf(RectangleType)),
        new Port(BlobAngles, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Perimeters, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(LongAxes, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(ShortAxes, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Circularities, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Rectangularities, DataType.NumberArray, Storage:NumericStorage.Float32),
        new Port(Scores, TypeRef.SequenceOf(DataType.Number), Nullable:true, Storage:NumericStorage.Float32),
        new Port(BinaryImage, BinaryImageType, Nullable:true),
        new Port(BlobImage, BlobImageType, Nullable:true),
        new Port(OutputMask, BinaryImageType, Nullable:true),
        new Port(InspectionRectangles, TypeRef.SequenceOf(RectangleType), Nullable:true),
        new Port(InspectionAnnuli, TypeRef.SequenceOf(AnnulusType), Nullable:true),
        new Port(ContourPoints, ContourInfoType, Nullable:true),
        new Port(ClassLabels, TypeRef.SequenceOf(DataType.Text), Nullable:true),
        new Port(LabelValues, TypeRef.SequenceOf(DataType.Integer), Nullable:true),
        new Port(BlindPolygon, TypeRef.SequenceOf(PointType), Nullable:true),
        new Port(BlindPolygonString, TypeRef.SequenceOf(DataType.Text), Nullable:true),
        new Port(OutputFixture, FixtureDeclarations.Info, Nullable:true),
        new Port(Rows, RowsType)
    });

    public static IReadOnlyList<OutputDisplayNode> OutputDisplay { get; } = Array.AsReadOnly(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary:true),
        new OutputDisplayNode(RunTime, "模块耗时", isModuleExecutionSummary:true),
        new OutputDisplayNode(Count, "Blob个数"),
        new OutputDisplayNode(TotalArea, "总面积"),
        new OutputDisplayNode(Areas, "面积数组"),
        new OutputDisplayNode(BlobStatuses, "Blob状态"),
        new OutputDisplayNode(Centroids, "质心"),
        new OutputDisplayNode(MinimumBoundingRectangles, "最小外接矩形"),
        new OutputDisplayNode(BlobAngles, "Blob角度"),
        new OutputDisplayNode(Perimeters, "周长数组"),
        new OutputDisplayNode(LongAxes, "长轴"),
        new OutputDisplayNode(ShortAxes, "短轴"),
        new OutputDisplayNode(Circularities, "圆形度"),
        new OutputDisplayNode(Rectangularities, "矩形度"),
        new OutputDisplayNode(BinaryImage, "二值图", isAuxiliaryImage:true),
        new OutputDisplayNode(BlobImage, "Blob图像", isAuxiliaryImage:true),
        new OutputDisplayNode(OutputMask, "输出掩膜", isAuxiliaryImage:true),
        new OutputDisplayNode(ContourPoints, "轮廓点"),
        new OutputDisplayNode(Rows, "Blob结果行")
    });

    internal static RecordValue FreezePoint(Point2 point) => new(FixtureDeclarations.Point,
        new Dictionary<string, object?> { ["x"] = (double)(float)point.X, ["y"] = (double)(float)point.Y });

    internal static RecordValue FreezeRectangle(double centerX, double centerY, double width, double height, double angleDegrees)
        => new(RectangleType, new Dictionary<string, object?>
        {
            ["center"] = FreezePoint(new(centerX, centerY)),
            ["width"] = (double)(float)width,
            ["height"] = (double)(float)height,
            ["angle-degrees"] = (double)(float)angleDegrees
        });

    internal static RecordValue FreezeBounds(double x, double y, double width, double height)
        => new(BoundsType, new Dictionary<string, object?>
        {
            ["origin"] = FreezePoint(new(x, y)),
            ["width"] = (double)(float)width,
            ["height"] = (double)(float)height
        });

    internal static RecordValue FreezeImage(TypeRef declaration, ImageLayout layout, int width, int height, IReadOnlyImage data)
        => new(declaration, new Dictionary<string, object?>
        {
            ["data"] = data,
            ["width"] = (long)width,
            ["height"] = (long)height,
            ["pixel-format"] = (long)layout.Format
        });
}
