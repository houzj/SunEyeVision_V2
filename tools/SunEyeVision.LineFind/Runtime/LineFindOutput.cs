using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.LineFind.Runtime;

/// <summary>
/// 直线查找输出对象。本阶段原生算法未取证，Session 不产出伪造的直线数值；
/// 仅当具备真实 HALCON 检测结果时才构造。结果资源所有权随调用作用域释放。
/// </summary>
public sealed class LineFindOutput : IToolOutput, IDisposable
{
    private readonly InputValues _values;
    private IOutputResourceScope? _owned;

    internal LineFindOutput(IReadOnlyDictionary<string, object?> values, IOutputResourceScope owned)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new InputValues(LineFindSchema.Outputs, values);
        _owned = owned ?? throw new ArgumentNullException(nameof(owned));
    }

    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null, this);
        return _values.CopyValues();
    }

    public void Dispose() => Interlocked.Exchange(ref _owned, null)?.Dispose();

    internal static RecordValue FreezeBox(double centerX, double centerY, double width, double height, double angleDegrees)
        => new(LineFindSchema.BoxType, new Dictionary<string, object?>
        {
            ["center"] = new RecordValue(FixtureDeclarations.Point,
                new Dictionary<string, object?> { ["x"] = (double)(float)centerX, ["y"] = (double)(float)centerY }),
            ["width"] = (double)(float)width,
            ["height"] = (double)(float)height,
            ["angle-degrees"] = (double)(float)angleDegrees
        });

    internal static RecordValue FreezeRow(long id, LineGeometry line, double angle, double fitError, RecordValue rectangle, bool passed)
    {
        var mid = new Point2((line.Start.X + line.End.X) / 2d, (line.Start.Y + line.End.Y) / 2d);
        return new(LineFindSchema.ResultType, new Dictionary<string, object?>
        {
            ["element-id"] = id,
            ["start-x"] = (double)(float)line.Start.X, ["start-y"] = (double)(float)line.Start.Y,
            ["end-x"] = (double)(float)line.End.X, ["end-y"] = (double)(float)line.End.Y,
            ["mid-x"] = (double)(float)mid.X, ["mid-y"] = (double)(float)mid.Y,
            ["angle-degrees"] = (double)(float)angle,
            ["fit-error"] = (double)(float)fitError,
            ["line"] = line,
            ["mid-point"] = LineFindSchema.Point(mid.X, mid.Y),
            ["rectangle"] = rectangle,
            ["region"] = null,
            ["judgment-passed"] = passed
        });
    }
}
