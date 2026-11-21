using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>
/// 输出与结果声明。输入端口与原件一致（图像 + 搜索区域 + 位置修正）；搜索结果按行声明，行内携带匹配点、
/// 匹配框、区域与判定，供结果页叠加与判定表使用。
/// 原件 <c>BlindPolygon</c>、<c>OutputMask</c> 与多模板（<c>MatchModelIndex/Name</c>）未迁移，
/// 因语义未取证且本工具单模板，差异记录在交接单。
/// </summary>
public static class GrayMatchSchema
{
    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;
    public const string ImageInput = "input.image";
    public const string RoiInput = "input.roi";
    public const string FixtureInput = "input.fixture";

    public const string Status = "output.status";
    public const string RunTime = "output.run-time";
    public const string Count = "output.count";
    public const string Scores = "output.scores";
    public const string MatchPoints = "output.match-points";
    public const string Angles = "output.angles";
    public const string Boxes = "output.boxes";
    public const string OutputFixture = "output.fixture";
    public const string Rows = "graymatch.rows";

    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef BoxType { get; } = TypeRef.RecordOf("graymatch.box", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });
    public static TypeRef ResultType { get; } = TypeRef.RecordOf("graymatch.result", new[]
    {
        new Port("element-id", DataType.Integer),
        new Port("match-point", PointType),
        new Port("center-x", DataType.Number),
        new Port("center-y", DataType.Number),
        new Port("angle-degrees", DataType.Number),
        new Port("score", DataType.Number),
        new Port("box", BoxType),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region)),
        new Port("judgment-passed", DataType.Boolean),
        new Port("failed-limits", TypeRef.SequenceOf(DataType.Text), false)
    });
    public static TypeRef RowsType { get; } = TypeRef.SequenceOf(ResultType);

    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[]
    {
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(Count, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(Scores, DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port(MatchPoints, TypeRef.SequenceOf(PointType)),
        new Port(Angles, DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port(Boxes, TypeRef.SequenceOf(BoxType)),
        new Port(OutputFixture, FixtureDeclarations.Info, Nullable: true),
        new Port(Rows, RowsType)
    });

    public static IReadOnlyList<OutputDisplayNode> OutputDisplay { get; } = Array.AsReadOnly(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时", isModuleExecutionSummary: true),
        new OutputDisplayNode(Count, "匹配个数"),
        new OutputDisplayNode(Scores, "匹配分数"),
        new OutputDisplayNode(MatchPoints, "匹配点"),
        new OutputDisplayNode(Angles, "匹配角度"),
        new OutputDisplayNode(Boxes, "匹配框"),
        new OutputDisplayNode(OutputFixture, "位置修正"),
        new OutputDisplayNode(Rows, "匹配结果行")
    });

    internal static RecordValue FreezePoint(double x, double y) => new(FixtureDeclarations.Point,
        new Dictionary<string, object?> { ["x"] = (double)(float)x, ["y"] = (double)(float)y });

    internal static PointGeometry Point(double x, double y) => new(ImageFrame, new Point2(x, y));

    internal static RecordValue FreezeBox(double centerX, double centerY, double width, double height, double angleDegrees)
        => new(BoxType, new Dictionary<string, object?>
        {
            ["center"] = FreezePoint(centerX, centerY),
            ["width"] = (double)(float)width,
            ["height"] = (double)(float)height,
            ["angle-degrees"] = (double)(float)angleDegrees
        });
}
