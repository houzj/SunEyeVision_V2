using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.DataClassification.Runtime;

/// <summary>
/// 数据分类（原件 DataClassificationModule）Runtime 入口。UI 骨架阶段实现身份、页签/字段声明、
/// 结果端口与呈现；算法执行由会话明确报"待补证"诊断（见 DataClassificationSession）。
/// </summary>
public sealed class DataClassificationTool : IToolDefinition<DataClassificationSettings, DataClassificationOutput>,
    IToolSettingsFields, IToolCatalogProvider, IToolResultPresentationProvider
{
    public const string Id = "DataClassification";

    public string ToolId => Id;

    public ToolCatalogDescriptor Catalog { get; } = new(
        "数据分类", "订阅上游模块输出，按条件把数据归入若干类别后输出（算法语义待补证）",
        "data-classification", "逻辑工具", 40, "DEFAULT");

    public ToolSchema SettingsSchema => DataClassificationFields.Schema;

    public ToolSchema ResultSchema { get; } = new(Array.Empty<Port>(), DataClassificationOutput.Ports,
        outputDisplay: DataClassificationOutput.Display);

    public ResultProjectionDescriptor ResultProjection { get; } = new(
        ResultProjectionKind.PropertyList, ResultCardinality.One, new[]
        {
            new ResultColumnDescriptor("status", "模块状态", new(DataClassificationOutput.Status)),
            new ResultColumnDescriptor("time", "模块耗时", new(DataClassificationOutput.RunTime))
        });

    public ToolSchema Describe() => Schema;

    public static ToolSchema Schema { get; } = new(Array.Empty<Port>(), DataClassificationOutput.Ports,
        new ToolPresentation(Array.Empty<FieldPresentation>()), outputDisplay: DataClassificationOutput.Display);

    public InputValues ReadSettings(object settings) => DataClassificationFields.Read(RequireSettings(settings));

    public object MaterializeSettings(InputValues values) => DataClassificationFields.Materialize(values);

    private static DataClassificationSettings RequireSettings(object settings) => settings as DataClassificationSettings
        ?? throw new ArgumentException("数据分类设置类型不正确。");

    public IReadOnlyList<Diagnostic> Validate(DataClassificationSettings settings) => settings.Validate();

    public ToolCapabilities Capabilities() => ToolCapabilities.None;

    public IToolSession<DataClassificationSettings, DataClassificationOutput> CreateSession()
        => new DataClassificationSession();
}
