using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleFind.Runtime;

/// <summary>
/// 圆查找输出对象。本阶段原生算法未取证，Session 不会产出伪造的圆检测数值；
/// 仅当具备真实 HALCON 检测结果时才构造本对象。结果资源所有权随调用作用域释放。
/// </summary>
public sealed class CircleFindOutput : IToolOutput, IDisposable
{
    private readonly InputValues _values;
    private IOutputResourceScope? _owned;

    internal CircleFindOutput(IReadOnlyDictionary<string, object?> values, IOutputResourceScope owned)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new InputValues(CircleFindSchema.Outputs, values);
        _owned = owned ?? throw new ArgumentNullException(nameof(owned));
    }

    public IReadOnlyDictionary<string, object?> ToValues()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owned) is null, this);
        return _values.CopyValues();
    }

    public void Dispose() => Interlocked.Exchange(ref _owned, null)?.Dispose();

    internal static RecordValue FreezeBox(double centerX, double centerY, double width, double height, double angleDegrees)
        => new(CircleFindSchema.BoxType, new Dictionary<string, object?>
        {
            ["center"] = new RecordValue(FixtureDeclarations.Point,
                new Dictionary<string, object?> { ["x"] = (double)(float)centerX, ["y"] = (double)(float)centerY }),
            ["width"] = (double)(float)width,
            ["height"] = (double)(float)height,
            ["angle-degrees"] = (double)(float)angleDegrees
        });

    internal static RecordValue FreezeRow(long id, double centerX, double centerY, double radius, double fitError,
        RecordValue rectangle, IReadOnlyRegion? region, bool passed)
        => new(CircleFindSchema.ResultType, new Dictionary<string, object?>
        {
            ["element-id"] = id,
            ["center-x"] = (double)(float)centerX,
            ["center-y"] = (double)(float)centerY,
            ["radius"] = (double)(float)radius,
            ["fit-error"] = (double)(float)fitError,
            ["circle"] = CircleFindSchema.Circle(centerX, centerY, radius),
            ["centre-point"] = CircleFindSchema.Point(centerX, centerY),
            ["rectangle"] = rectangle,
            ["region"] = region,
            ["judgment-passed"] = passed
        });
}
