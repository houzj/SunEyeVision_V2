using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PointPointMeasure.Runtime;

/// <summary>
/// 点点测量的输出声明，与 docs/07-工具逆向/tools/imvsp2pmeasuremodu/parameters.json:415-580 的
/// Output 段逐项对应：ModuRunTime、ModuStatus、MeasurePoint1、MeasurePoint2、Midpoint、
/// P2P1Angle、P2P1Dist、MeasureCalibMatrix（Visible=false）。另加结果页单行集合 Rows。
/// </summary>
public sealed class PointPointMeasureOutput : IToolOutput, IDisposable
{
    public const string Status = "output.module-status";
    public const string RunTime = "output.module-time-ms";
    public const string MeasurePoint1 = "output.measure-point-1";
    public const string MeasurePoint2 = "output.measure-point-2";
    public const string Midpoint = "output.midpoint";
    public const string Angle = "output.p2p-angle";
    public const string Dist = "output.p2p-dist";
    public const string CalibMatrix = "output.measure-calib-matrix";
    public const string Rows = "output.rows";

    public const string RowIndex = "row-index";
    public const string JudgmentPassed = "judgment-passed";
    public const string Region = "region";
    public const string Line = "line";

    private static TypeRef PointType { get; } = FixtureDeclarations.Point;

    private static TypeRef LineType { get; } = TypeRef.RecordOf("point-point-measure.line", new[]
    {
        new Port("center", PointType),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });

    private static TypeRef RowType { get; } = TypeRef.RecordOf("point-point-measure.row", new[]
    {
        new Port(RowIndex, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(MeasurePoint1, PointType),
        new Port(MeasurePoint2, PointType),
        new Port(Midpoint, PointType),
        new Port(Angle, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Dist, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Line, LineType, Nullable: true),
        new Port(Region, TypeRef.ResourceOf(ResourceKind.Region), Nullable: true),
        new Port(JudgmentPassed, DataType.Boolean)
    });

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(MeasurePoint1, PointType),
        new Port(MeasurePoint2, PointType),
        new Port(Midpoint, PointType),
        new Port(Angle, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Dist, DataType.Number, Storage: NumericStorage.Float32),
        new Port(CalibMatrix, TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)), Nullable: true),
        new Port(Rows, TypeRef.SequenceOf(RowType, new CountConstraint(1, 1)))
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时 (ms)", isModuleExecutionSummary: true),
        new OutputDisplayNode(MeasurePoint1, "测量点1"),
        new OutputDisplayNode(MeasurePoint2, "测量点2"),
        new OutputDisplayNode(Midpoint, "中点"),
        new OutputDisplayNode(Angle, "角度"),
        new OutputDisplayNode(Dist, "距离"),
        new OutputDisplayNode(Rows, "点点测量行")
    });

    private readonly InputValues _values;
    private IOutputResourceScope? _owned;

    internal PointPointMeasureOutput(IReadOnlyDictionary<string, object?> values, IOutputResourceScope owned)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new InputValues(Ports, values);
        _owned = owned ?? throw new ArgumentNullException(nameof(owned));
    }

    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null, this);
        return _values.CopyValues();
    }

    public void Dispose() => Interlocked.Exchange(ref _owned, null)?.Dispose();
}
