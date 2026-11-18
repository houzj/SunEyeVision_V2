using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DataClassification.Runtime;

/// <summary>
/// 数据分类的结果端口。原厂 parameters.json.outputs 只有两项：ModuRunTime(float)、ModuStatus(int)。
/// 分类输出是动态 IO（订阅关系决定），不属于固定端口集合；算法语义未取证，本阶段只声明这两项固定端口。
/// </summary>
public sealed class DataClassificationOutput : IToolOutput
{
    public const string Status = "output.status";
    public const string RunTime = "output.module-time-ms";

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32)
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(Status, "模块状态"),
        new OutputDisplayNode(RunTime, "模块耗时 (ms)")
    });

    private readonly InputValues _values;

    internal DataClassificationOutput(IReadOnlyDictionary<string, object?> values)
    {
        _values = new(Ports, values);
    }

    public IReadOnlyDictionary<string, object?> ToValues() => _values.CopyValues();
}
