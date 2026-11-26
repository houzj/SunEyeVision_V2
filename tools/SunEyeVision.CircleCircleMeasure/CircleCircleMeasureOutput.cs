using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleCircleMeasure.Runtime;

/// <summary>
/// 圆圆测量的输出声明，与 docs/07-工具逆向/tools/imvsc2cmeasuremodu/parameters.json 的 Output 段
/// 15 项逐项对应：ModuRunTime/ModuStatus、MeasureCircle1/2、MeasureCircleAnnulus1/2、
/// Intersection 1/2、Angle、Dist、C2CType、DetectCaliperBox1/2、Output Position Correction Info、
/// MeasureCalibMatrix(Visible=false)。另加结果页单行集合 Rows（原厂为扁平通道，显示需要同身份行）。
/// 输出只声明通道；算法数值取证为 not-established，本批不产出测量值。
/// </summary>
public sealed class CircleCircleMeasureOutput : IToolOutput, IDisposable
{
    public const string Status = "output.module-status";
    public const string RunTime = "output.module-time-ms";
    public const string Angle = "output.angle";
    public const string Dist = "output.dist";
    public const string C2cType = "output.c2c-type";
    public const string Circle1 = "output.measure-circle-1";
    public const string Circle2 = "output.measure-circle-2";
    public const string Annulus1 = "output.measure-annulus-1";
    public const string Annulus2 = "output.measure-annulus-2";
    public const string Intersection1 = "output.intersection-1";
    public const string Intersection2 = "output.intersection-2";
    public const string Caliper1 = "output.detect-caliper-1";
    public const string Caliper2 = "output.detect-caliper-2";
    public const string Fixture = "output.position-correction";
    public const string CalibMatrix = "output.measure-calib-matrix";
    public const string Rows = "output.rows";

    public const string RowIndex = "row-index";
    public const string JudgmentPassed = "judgment-passed";
    public const string Region = "region";

    private static TypeRef PointType { get; } = FixtureDeclarations.Point;

    private static TypeRef CircleType { get; } = TypeRef.RecordOf("circle-circle-measure.circle", new[]
    {
        new Port("center", PointType),
        new Port("radius", DataType.Number, Storage: NumericStorage.Float32)
    });

    private static TypeRef AnnulusType { get; } = TypeRef.RecordOf("circle-circle-measure.annulus", new[]
    {
        new Port("center", PointType),
        new Port("inner-radius", DataType.Number, Storage: NumericStorage.Float32),
        new Port("outer-radius", DataType.Number, Storage: NumericStorage.Float32),
        new Port("start-angle", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-extent", DataType.Number, Storage: NumericStorage.Float32)
    });

    private static TypeRef CaliperType { get; } = TypeRef.RecordOf("circle-circle-measure.caliper", new[]
    {
        new Port("center", PointType),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });

    private static TypeRef RowType { get; } = TypeRef.RecordOf("circle-circle-measure.row", new[]
    {
        new Port(RowIndex, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(Angle, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Dist, DataType.Number, Storage: NumericStorage.Float32),
        new Port(C2cType, DataType.Text),
        new Port(Circle1, CircleType),
        new Port(Circle2, CircleType),
        new Port(Intersection1, PointType),
        new Port(Intersection2, PointType),
        new Port(Caliper1, CaliperType),
        new Port(Caliper2, CaliperType),
        new Port(Region, TypeRef.ResourceOf(ResourceKind.Region), Nullable: true),
        new Port(JudgmentPassed, DataType.Boolean)
    });

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Angle, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Dist, DataType.Number, Storage: NumericStorage.Float32),
        new Port(C2cType, DataType.Text),
        new Port(Circle1, CircleType, Nullable: true),
        new Port(Circle2, CircleType, Nullable: true),
        new Port(Annulus1, AnnulusType, Nullable: true),
        new Port(Annulus2, AnnulusType, Nullable: true),
        new Port(Intersection1, PointType, Nullable: true),
        new Port(Intersection2, PointType, Nullable: true),
        new Port(Caliper1, CaliperType, Nullable: true),
        new Port(Caliper2, CaliperType, Nullable: true),
        new Port(Fixture, DataType.Text, Nullable: true),
        new Port(CalibMatrix, TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64)), Nullable: true),
        new Port(Rows, TypeRef.SequenceOf(RowType, new CountConstraint(1, 1)))
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(Status, "模块状态", isModuleExecutionSummary: true),
        new OutputDisplayNode(RunTime, "模块耗时 (ms)", isModuleExecutionSummary: true),
        new OutputDisplayNode(Angle, "角度"),
        new OutputDisplayNode(Dist, "距离"),
        new OutputDisplayNode(C2cType, "圆圆类型"),
        new OutputDisplayNode(Circle1, "测量圆1"),
        new OutputDisplayNode(Circle2, "测量圆2"),
        new OutputDisplayNode(Intersection1, "交点1"),
        new OutputDisplayNode(Intersection2, "交点2"),
        new OutputDisplayNode(Caliper1, "卡尺框1"),
        new OutputDisplayNode(Caliper2, "卡尺框2"),
        new OutputDisplayNode(Rows, "圆圆测量行")
    });

    private readonly InputValues _values;
    private IOutputResourceScope? _owned;

    internal CircleCircleMeasureOutput(IReadOnlyDictionary<string, object?> values, IOutputResourceScope owned)
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
