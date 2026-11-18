using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.MachineLearningClassifier.Runtime;

/// <summary>
/// ML 分类结果端口，按原厂 parameters.json.outputs：ModuRunTime/ModuStatus/PredictNum/
/// LabelsIndex[]/LabelName[]/ROI/位置修正输出（Visible=false，本阶段不声明）。
/// 结果页显示组按 resources.json：FixtureInitPoint/FixtureRunPoint/ResultText/CheckZone。
/// 算法未取证，端口只声明类型，不对数值语义作推断。
/// </summary>
public sealed class MachineLearningClassifierOutput : IToolOutput
{
    public const string Status = "output.status";
    public const string RunTime = "output.module-time-ms";
    public const string PredictNum = "output.predict-num";
    public const string LabelsIndex = "output.labels-index";
    public const string LabelName = "output.label-name";
    public const string Roi = "output.roi";
    public const string CenterX = "center-x";
    public const string CenterY = "center-y";
    public const string Width = "width";
    public const string Height = "height";
    public const string Angle = "angle";

    public static TypeRef RoiType { get; } = TypeRef.RecordOf("MachineLearningClassifier.roi", new[]
    {
        new Port(CenterX, DataType.Number, Storage: NumericStorage.Float32),
        new Port(CenterY, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Width, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Height, DataType.Number, Storage: NumericStorage.Float32),
        new Port(Angle, DataType.Number, Storage: NumericStorage.Float32)
    });

    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(Status, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(RunTime, DataType.Number, Storage: NumericStorage.Float32),
        new Port(PredictNum, DataType.Integer, Storage: NumericStorage.Int32),
        new Port(LabelsIndex, TypeRef.SequenceOf(DataType.Integer)),
        new Port(LabelName, TypeRef.SequenceOf(DataType.Text)),
        new Port(Roi, RoiType)
    });

    public static IReadOnlyList<OutputDisplayNode> Display { get; } = OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(PredictNum, "预测数量"),
        new OutputDisplayNode(LabelsIndex, "标签索引"),
        new OutputDisplayNode(LabelName, "标签名称"),
        new OutputDisplayNode(Roi, "检测区域", new[]
        {
            new OutputDisplayNode(CenterX, "中心 X"), new OutputDisplayNode(CenterY, "中心 Y"),
            new OutputDisplayNode(Width, "宽度"), new OutputDisplayNode(Height, "高度"),
            new OutputDisplayNode(Angle, "角度")
        }),
        new OutputDisplayNode(RunTime, "模块耗时 (ms)"),
        new OutputDisplayNode(Status, "模块状态", displayInResultPanel: false)
    });

    private readonly InputValues _values;

    internal MachineLearningClassifierOutput(IReadOnlyDictionary<string, object?> values)
    {
        _values = new(Ports, values);
    }

    public IReadOnlyDictionary<string, object?> ToValues() => _values.CopyValues();
}
