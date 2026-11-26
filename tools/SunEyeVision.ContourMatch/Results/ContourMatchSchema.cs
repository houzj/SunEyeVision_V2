using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 输出与结果声明。输入端口与原件一致（图像 + 搜索区域 + 位置修正）；搜索结果按行声明，行内携带匹配点、
/// 匹配框、区域、轮廓与判定，供结果页叠加与判定表使用。
/// 原件 <c>ContourPoints/ContourIndex</c> 与 <c>MatchOutline/MatchOutlineNum</c> 是同一组轮廓点的两种打包，
/// 本工具统一由 <see cref="OutlinePoints"/>（x/y/match-index/count）承载，差异记录在交接单。
/// </summary>
public static class ContourMatchSchema
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
    public const string Scales = "output.scales";
    /// <summary>原厂 MatchModelIndex（int[]）：本方单模板，恒为 0。</summary>
    public const string ModelIndex = "output.model-index";
    /// <summary>原厂 MatchModelName（string[]）：本方单模板，取训练样本名称。</summary>
    public const string ModelName = "output.model-name";
    /// <summary>原厂 MatchScaleX/MatchScaleY（float[]）：HALCON 形状模板仅等比，等于 MatchScale。</summary>
    public const string ScaleX = "output.scale-x";
    public const string ScaleY = "output.scale-y";
    /// <summary>原厂 SkewX（float[]）：横切角度语义未实现，恒为 0。</summary>
    public const string SkewX = "output.skew-x";
    /// <summary>原厂 ROI（检测区域）：本次搜索区域的外接框。</summary>
    public const string DetectRoi = "output.roi";
    public const string Boxes = "output.boxes";
    public const string OutlinePoints = "output.outline-points";
    public const string OutputFixture = "output.fixture";
    public const string Rows = "contourmatch.rows";

    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef BoxType { get; } = TypeRef.RecordOf("contourmatch.box", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });
    /// <summary>原件 MatchOutlinePoints 的 info 打包：逐点坐标 + 归属匹配序号 + 逐点分数。</summary>
    public static TypeRef OutlineInfoType { get; } = TypeRef.RecordOf("contourmatch.outline-info", new[]
    {
        new Port("x", DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port("y", DataType.NumberArray, Storage: NumericStorage.Float32),
        // 逐点分数：0=匹配点、1=中间态、其余=未匹配（原件 MatchOutlineScore 语义）。
        new Port("score", DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port("match-index", TypeRef.SequenceOf(DataType.Integer), Storage: NumericStorage.Int32)
    });
    /// <summary>原件 MatchOutlinePoints：count + info，与 Blob 的 CONTOURPOINTS 同一打包形态。</summary>
    public static TypeRef OutlineSetType { get; } = TypeRef.RecordOf("contourmatch.outline-points", new[]
    {
        new Port("count", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("info", OutlineInfoType)
    });
    public static TypeRef ResultType { get; } = TypeRef.RecordOf("contourmatch.result", new[]
    {
        new Port("element-id", DataType.Integer),
        new Port("match-point", PointType),
        new Port("center-x", DataType.Number),
        new Port("center-y", DataType.Number),
        new Port("angle-degrees", DataType.Number),
        new Port("scale", DataType.Number, Required: false, Nullable: true),
        new Port("score", DataType.Number),
        new Port("box", BoxType),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region)),
        new Port("outline", TypeRef.SequenceOf(PointType), false),
        new Port("judgment-passed", DataType.Boolean),
        new Port("failed-limits", TypeRef.SequenceOf(DataType.Text), false)
    });
    public static TypeRef RowsType { get; } = TypeRef.SequenceOf(ResultType);

    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[]
    {
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(Count, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(ModelIndex, TypeRef.SequenceOf(DataType.Integer), Storage: NumericStorage.Int32),
        new Port(ModelName, TypeRef.SequenceOf(DataType.Text)),
        new Port(Scores, DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port(MatchPoints, TypeRef.SequenceOf(PointType)),
        new Port(Angles, DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port(Scales, DataType.NumberArray, Nullable: true, Storage: NumericStorage.Float32),
        new Port(ScaleX, DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port(ScaleY, DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port(SkewX, DataType.NumberArray, Storage: NumericStorage.Float32),
        new Port(DetectRoi, BoxType, Nullable: true),
        new Port(Boxes, TypeRef.SequenceOf(BoxType)),
        new Port(OutlinePoints, OutlineSetType, Nullable: true),
        new Port(OutputFixture, FixtureDeclarations.Info, Nullable: true),
        new Port(Rows, RowsType)
    });

    public static IReadOnlyList<OutputDisplayNode> OutputDisplay { get; } = Array.AsReadOnly(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时", isModuleExecutionSummary: true),
        new OutputDisplayNode(Count, "匹配个数"),
        new OutputDisplayNode(ModelIndex, "模型索引"),
        new OutputDisplayNode(ModelName, "模型名称"),
        new OutputDisplayNode(Scores, "匹配分数"),
        new OutputDisplayNode(MatchPoints, "匹配点"),
        new OutputDisplayNode(Angles, "匹配角度"),
        new OutputDisplayNode(Scales, "匹配尺度"),
        new OutputDisplayNode(ScaleX, "X匹配尺度"),
        new OutputDisplayNode(ScaleY, "Y匹配尺度"),
        new OutputDisplayNode(SkewX, "横切角度"),
        new OutputDisplayNode(DetectRoi, "检测区域"),
        new OutputDisplayNode(Boxes, "匹配框"),
        new OutputDisplayNode(OutlinePoints, "轮廓点"),
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
