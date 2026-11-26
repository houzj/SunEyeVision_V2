using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.LineFind.Runtime;

/// <summary>
/// 直线查找输出与结果声明，逐项对照 <c>parameters.json:765-1500</c> 的 15 项 Output
/// 与托管 <c>LineFindResult.cs</c>（OutputLine/Midpoint/LineAngle/LineFitError/LinearContourPoint/ROI 等）。
/// 原生算法未取证，未确认输出（盲区多边形串、位置修正）不伪造数值。
/// </summary>
public static class LineFindSchema
{
    public static CoordinateFrame ImageFrame => RoiDeclarations.ImageFrame;
    public const string ImageInput = "input.image";
    public const string RoiInput = "input.roi";
    public const string FixtureInput = "input.fixture";

    public const string Status = "output.status";
    public const string RunTime = "output.run-time";
    public const string LineAngle = "output.line-angle";
    public const string LineFitError = "output.line-fit-error";
    public const string Rows = "linefind.rows";

    public static TypeRef PointType { get; } = TypeRef.GeometryOf(GeometryKind.Point, ImageFrame);
    public static TypeRef LineType { get; } = TypeRef.GeometryOf(GeometryKind.Line, ImageFrame);
    public static TypeRef BoxType { get; } = TypeRef.RecordOf("linefind.box", new[]
    {
        new Port("center", FixtureDeclarations.Point),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });

    /// <summary>一条直线查找结果：序号、起点、终点、中点、角度、拟合误差、线几何、外接矩形、判定。</summary>
    public static TypeRef ResultType { get; } = TypeRef.RecordOf("linefind.result", new[]
    {
        new Port("element-id", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("start-x", DataType.Number, Storage: NumericStorage.Float32),
        new Port("start-y", DataType.Number, Storage: NumericStorage.Float32),
        new Port("end-x", DataType.Number, Storage: NumericStorage.Float32),
        new Port("end-y", DataType.Number, Storage: NumericStorage.Float32),
        new Port("mid-x", DataType.Number, Storage: NumericStorage.Float32),
        new Port("mid-y", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32),
        new Port("fit-error", DataType.Number, Storage: NumericStorage.Float32),
        new Port("line", LineType),
        new Port("mid-point", PointType),
        new Port("rectangle", BoxType),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true),
        new Port("judgment-passed", DataType.Boolean)
    });
    public static TypeRef RowsType { get; } = TypeRef.SequenceOf(ResultType);

    public static IReadOnlyList<Port> Outputs { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(LineAngle, DataType.Number, Storage: NumericStorage.Float32),
        new Port(LineFitError, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Rows, RowsType)
    });

    public static IReadOnlyList<OutputDisplayNode> OutputDisplay { get; } = Array.AsReadOnly(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时", isModuleExecutionSummary: true),
        new OutputDisplayNode(LineAngle, "直线角度"),
        new OutputDisplayNode(LineFitError, "拟合误差"),
        new OutputDisplayNode(Rows, "直线查找结果行")
    });

    internal static PointGeometry Point(double x, double y) => new(ImageFrame, new Point2(x, y));
    internal static LineGeometry Line(double sx, double sy, double ex, double ey) => new(ImageFrame, new Point2(sx, sy), new Point2(ex, ey));
}
