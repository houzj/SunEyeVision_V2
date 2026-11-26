using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.MachineLearningClassifier.Runtime;

/// <summary>
/// ML 分类（原件 IMVSMachineLearningClassifierModu）Runtime 入口。UI 骨架阶段实现身份、四页签/字段声明、
/// 输入/结果端口与呈现；算法执行由会话明确报"待补证"诊断（见 MachineLearningClassifierSession）。
/// </summary>
public sealed class MachineLearningClassifierTool : IToolDefinition<MachineLearningClassifierSettings, MachineLearningClassifierOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider
{
    public const string Id = "MachineLearningClassifier";
    public const string ImageInput = "input.image";
    public const string RoiInput = "input.roi";
    public const string FixtureInput = "input.fixture";

    public string ToolId => Id;

    public ToolCatalogDescriptor Catalog { get; } = new(
        "ML分类", "基于机器学习的图像特征分类（算法与模型格式待补证）",
        "ml-classifier", "识别", 30, "DEFAULT");

    public ToolSchema SettingsSchema => MachineLearningClassifierFields.Schema;

    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), MachineLearningClassifierOutput.Ports,
        outputDisplay: MachineLearningClassifierOutput.Display);

    // 原厂输出为标量 PredictNum + 数组 LabelsIndex/LabelName + ROI，没有"行集合"记录结构；
    // UI 骨架阶段按属性列表呈现，避免臆造行身份（算法未取证）。
    public ResultProjectionDescriptor ResultProjection { get; } = new(
        ResultProjectionKind.PropertyList, ResultCardinality.One, new[]
        {
            new ResultColumnDescriptor("predict-num", "预测数量", new(MachineLearningClassifierOutput.PredictNum), DecimalPlaces: 0),
            new ResultColumnDescriptor("labels-index", "标签索引", new(MachineLearningClassifierOutput.LabelsIndex)),
            new ResultColumnDescriptor("label-name", "标签名称", new(MachineLearningClassifierOutput.LabelName)),
            new ResultColumnDescriptor("time", "模块耗时", new(MachineLearningClassifierOutput.RunTime))
        });

    public ToolSchema Describe() => Schema;

    public static ToolSchema Schema { get; } = new(new[]
    {
        new Port(ImageInput, TypeRef.ResourceOf(ResourceKind.Image), EditModes: FieldEditModes.Reference),
        new Port(RoiInput, TypeRef.ResourceOf(ResourceKind.Region), Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(FixtureInput, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true, EditModes: FieldEditModes.Reference)
    }, MachineLearningClassifierOutput.Ports, new ToolPresentation(Array.Empty<FieldPresentation>()),
        workflowImageInputId: ImageInput, outputDisplay: MachineLearningClassifierOutput.Display);

    public InputValues ReadSettings(object settings) => MachineLearningClassifierFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => MachineLearningClassifierFields.Materialize(values);

    private static MachineLearningClassifierSettings RequireSettings(object settings) => settings as MachineLearningClassifierSettings
        ?? throw new ArgumentException("ML 分类设置类型不正确。");

    public IReadOnlyList<Diagnostic> Validate(MachineLearningClassifierSettings settings) => settings.Validate();

    public ToolCapabilities Capabilities() => ToolCapabilities.None;

    public IToolSession<MachineLearningClassifierSettings, MachineLearningClassifierOutput> CreateSession() => new MachineLearningClassifierSession();
}
