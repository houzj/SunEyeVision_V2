using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>
/// 划片拆分的声明唯一真源（原件 <c>IMVSDivideImageModuAlgorithm.xml</c> +
/// <c>IMVSDivideImageModuAlgorithmTab.xml</c>）。
/// </summary>
internal static class DivideImageManifest
{
    public static ToolManifest Value { get; } = Build();

    /// <summary>
    /// 组合体输入：本地 ROI 文档 + ROI 几何端口 + Assist 端口
    /// （与手工 <c>Describe</c> 逐项一致，顺序不变）。
    /// </summary>
    private static IReadOnlyList<Port> ExtraInputs() => new[]
    {
        new Port(DivideImageTool.RoiInput, TypeRef.ResourceOf(ResourceKind.Region),
            Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(DivideImageRoi.LocalDocumentId, RoiDocumentValue.Declaration,
            Required: false, EditModes: FieldEditModes.Constant)
    }.Concat(DivideImageRoi.Ports).Concat(DivideImageAssist.Ports).ToArray();

    /// <summary>矩形记录的结果树子节点（与原件 <c>ROIBOX</c> 的四个成员逐项一致）。</summary>
    private static IReadOnlyList<ToolResultField> RectChildren() => new[]
    {
        new ToolResultField(DivideImageOutput.RectCenter, "矩形中心", DivideImageOutput.CenterType),
        new ToolResultField(DivideImageOutput.RectWidth, "矩形宽度", DataType.Number),
        new ToolResultField(DivideImageOutput.RectHeight, "矩形高度", DataType.Number),
        new ToolResultField(DivideImageOutput.RectAngle, "矩形角度", DataType.Number)
    };

    private static ToolManifest Build() => new(
        DivideImageTool.Id, "划片拆分",
        "按X/Y方向划片数量与重叠率计算分片矩形，输出分片矩形集合与分片个数",
        "divide-image", "拆分组合", 10,
        new[]
        {
            // 原厂 AssistGroup 开关，位于基本参数/ROI区域末尾。
            new ToolParameterDeclaration(DivideImageFields.PositionCorrectionEnabled, "位置修正",
                "ROI区域", DataType.Boolean, false,
                propertyName: nameof(DivideImageSettings.PositionCorrectionEnabled),
                displayType: DeclaredFieldTemplateKey.Boolean),
            new ToolParameterDeclaration(DivideImageFields.Columns, "X方向划片数量",
                DivideImageFields.Section, DataType.Integer, 1L,
                minimum: 1, maximum: DivideImageFields.MaximumDivisionsPerAxis, step: 1, decimals: 0,
                storage: NumericStorage.Int32, propertyName: nameof(DivideImageSettings.Columns),
                displayType: DeclaredFieldTemplateKey.Integer),
            new ToolParameterDeclaration(DivideImageFields.Rows, "Y方向划片数量",
                DivideImageFields.Section, DataType.Integer, 1L,
                minimum: 1, maximum: DivideImageFields.MaximumDivisionsPerAxis, step: 1, decimals: 0,
                storage: NumericStorage.Int32, propertyName: nameof(DivideImageSettings.Rows),
                displayType: DeclaredFieldTemplateKey.Integer),
            new ToolParameterDeclaration(DivideImageFields.OverlapX, "X方向重叠率 (%)",
                DivideImageFields.Section, DataType.Integer, 0L,
                minimum: DivideImageFields.MinimumOverlapPercent,
                maximum: DivideImageFields.MaximumOverlapPercent, step: 1, decimals: 0,
                storage: NumericStorage.Int32, propertyName: nameof(DivideImageSettings.OverlapX),
                displayType: DeclaredFieldTemplateKey.Integer),
            new ToolParameterDeclaration(DivideImageFields.OverlapY, "Y方向重叠率 (%)",
                DivideImageFields.Section, DataType.Integer, 0L,
                minimum: DivideImageFields.MinimumOverlapPercent,
                maximum: DivideImageFields.MaximumOverlapPercent, step: 1, decimals: 0,
                storage: NumericStorage.Int32, propertyName: nameof(DivideImageSettings.OverlapY),
                displayType: DeclaredFieldTemplateKey.Integer)
        },
        new ToolResultDeclaration(null,
            new[]
            {
                new ToolResultField(DivideImageOutput.Roi, "检测框", DivideImageOutput.RectType,
                    children: RectChildren()),
                new ToolResultField(DivideImageOutput.Rects, "分片矩形", DivideImageOutput.RectsType,
                    children: RectChildren()),
                new ToolResultField(DivideImageOutput.Count, "分片个数", DataType.Integer,
                    storage: NumericStorage.Int32),
                new ToolResultField(DivideImageOutput.ModuleRunTime, "模块耗时 (ms)", DataType.Number,
                    storage: NumericStorage.Float32),
                new ToolResultField(DivideImageOutput.ModuleStatus, "模块状态", DataType.Integer,
                    storage: NumericStorage.Int32, showInResultPanel: false)
            },
            slots: null,
            // 列标识沿用原投影键，保证已保存方案的列选择不变。
            columns: new[]
            {
                new ToolResultColumn(DivideImageOutput.Roi, "检测框", key: "roi"),
                new ToolResultColumn(DivideImageOutput.Rects, "分片矩形", key: "rects"),
                new ToolResultColumn(DivideImageOutput.Count, "分片个数", 0, key: "count"),
                new ToolResultColumn(DivideImageOutput.ModuleRunTime, "模块耗时", 3, "ms", key: "time")
            },
            kind: ResultProjectionKind.PropertyList,
            shape: ToolResultShape.PropertyList),
        // 本工具没有 ImageViewConfig：结果树直接由结果字段生成，显示配置为空。
        new ToolDisplayDeclaration(Array.Empty<ToolDisplayItem>()),
        ToolInputTemplate.Image,
        nodeDisplayType: "IMAGE",
        sections: new[]
        {
            // 证据 IMVSDivideImageModuAlgorithmTab.xml：两个 Category 标 CanTriggerRun="TRUE"
            // （无 SubscribeVisible 标记，故不关闭订阅入口）。
            new ToolSectionDeclaration("基本参数", "ROI区域", canTriggerRun: true),
            new ToolSectionDeclaration(DivideImageFields.Page, DivideImageFields.Section, canTriggerRun: true)
        },
        inputPresentations: new[]
        {
            new ToolInputPresentation(DivideImageTool.ImageInput, "输入源", "基本参数", "图像输入")
        },
        extraInputs: ExtraInputs());
}
