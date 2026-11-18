using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleFind.Runtime;

/// <summary>
/// 圆查找输出与结果声明，逐项对照原厂 <c>parameters.json:935-1875</c> 的 16 项 Output
/// 与托管 <c>CircleFindResult.cs:17-109</c>。本阶段只保留有托管结果模型证据的核心输出
/// （圆/拟合误差/轮廓点/检测区/状态/耗时）+ 结果行；原生未取证的输出（位置修正、盲区多边形串、
/// 卡尺框明细等）列入交接单未确认项，不作为伪造数值产出。
/// </summary>
public static class CircleFindSchema
{
    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;
    public const string ImageInput = "input.image";
    public const string RoiInput = "input.roi";
    public const string FixtureInput = "input.fixture";

    public const string Status = "output.status";
    public const string RunTime = "output.run-time";
    public const string CenterX = "output.center-x";
    public const string CenterY = "output.center-y";
    public const string Radius = "output.radius";
    public const string FitError = "output.fit-error";
    public const string Rows = "circlefind.rows";

    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef CircleType { get; } = TypeRef.GeometryOf(GeometryKind.Circle, ImageFrame);
    /// <summary>输出圆的外接旋转矩形，供结果页叠加矩形需求（原件 OutputCircle/DetectRect 的外接框）。</summary>
    public static TypeRef BoxType { get; } = TypeRef.RecordOf("circlefind.box", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });

    /// <summary>一条圆查找结果：序号、圆心、半径、拟合误差、圆几何、外接矩形、区域、判定。</summary>
    public static TypeRef ResultType { get; } = TypeRef.RecordOf("circlefind.result", new[]
    {
        new Port("element-id", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("center-x", DataType.Number, Storage: NumericStorage.Float32),
        new Port("center-y", DataType.Number, Storage: NumericStorage.Float32),
        new Port("radius", DataType.Number, Storage: NumericStorage.Float32),
        new Port("fit-error", DataType.Number, Storage: NumericStorage.Float32),
        new Port("circle", CircleType),
        new Port("centre-point", PointType),
        new Port("rectangle", BoxType),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true),
        new Port("judgment-passed", DataType.Boolean)
    });
    public static TypeRef RowsType { get; } = TypeRef.SequenceOf(ResultType);

    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(CenterX, DataType.Number, Storage: NumericStorage.Float32),
        new Port(CenterY, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Radius, DataType.Number, Storage: NumericStorage.Float32),
        new Port(FitError, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Rows, RowsType)
    });

    public static IReadOnlyList<OutputDisplayNode> OutputDisplay { get; } = Array.AsReadOnly(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时", isModuleExecutionSummary: true),
        new OutputDisplayNode(CenterX, "圆心X"),
        new OutputDisplayNode(CenterY, "圆心Y"),
        new OutputDisplayNode(Radius, "半径"),
        new OutputDisplayNode(FitError, "拟合误差"),
        new OutputDisplayNode(Rows, "圆查找结果行")
    });

    internal static PointGeometry Point(double x, double y) => new(ImageFrame, new Point2(x, y));

    internal static CircleGeometry Circle(double centerX, double centerY, double radius) => new(ImageFrame, new Point2(centerX, centerY), radius);
}
