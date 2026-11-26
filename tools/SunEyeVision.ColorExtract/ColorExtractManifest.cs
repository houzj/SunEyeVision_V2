using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>
/// 色彩提取的声明唯一真源（原件 <c>IMVSColorExtractModuAlgorithm.xml</c> +
/// <c>IMVSColorExtractModuAlgorithmTab.xml</c> + <c>IMVSColorExtractModuDisplay.xml</c>）。
/// </summary>
internal static class ColorExtractManifest
{
    public static ToolManifest Value { get; } = Build();

    private static IReadOnlyList<ToolResultField> ImageChildren() => new[]
    {
        new ToolResultField(ColorExtractOutput.ImageData, "图像数据", TypeRef.ResourceOf(ResourceKind.Image)),
        new ToolResultField(ColorExtractOutput.ImageWidth, "图像宽度", DataType.Integer),
        new ToolResultField(ColorExtractOutput.ImageHeight, "图像高度", DataType.Integer),
        new ToolResultField(ColorExtractOutput.ImageFormat, "图像像素格式", DataType.Integer)
    };

    private static IReadOnlyList<ToolResultField> RoiChildren() => new[]
    {
        new ToolResultField(ColorExtractOutput.RoiCenter, "矩形中心", ColorExtractOutput.CenterType),
        new ToolResultField(ColorExtractOutput.RoiWidth, "矩形宽度", DataType.Number),
        new ToolResultField(ColorExtractOutput.RoiHeight, "矩形高度", DataType.Number),
        new ToolResultField(ColorExtractOutput.RoiAngle, "矩形角度", DataType.Number)
    };

    private static ToolManifest Build() => new(
        ColorExtractTool.Id, "颜色抽取",
        "按颜色区间提取符合范围的像素并输出提取面积与外接矩形",
        "color-extract", "颜色处理", 10,
        new[]
        {
            // 原厂 InputColorRange：'#' 分隔区间、',' 分隔 9 项，按文本保存。
            new ToolParameterDeclaration(ColorExtractFields.ColorRange, "颜色区间",
                ColorExtractFields.RangeSection, DataType.Text, ColorExtractRanges.DefaultSerialized,
                propertyName: nameof(ColorExtractSettings.ColorRange),
                displayType: DeclaredFieldTemplateKey.String),
            new ToolParameterDeclaration(ColorExtractFields.AreaLimitEnabled, "颜色面积判断",
                ColorExtractFields.LimitSection, DataType.Boolean, false,
                propertyName: nameof(ColorExtractSettings.AreaLimitEnabled),
                displayType: DeclaredFieldTemplateKey.Boolean),
            new ToolParameterDeclaration(ColorExtractFields.AreaLimitLow, "面积下限",
                ColorExtractFields.LimitSection, DataType.Integer, 1L,
                minimum: ColorExtractFields.MinimumAreaLimit,
                maximum: ColorExtractFields.MaximumAreaLimit, step: 1, decimals: 0,
                storage: NumericStorage.Int32, propertyName: nameof(ColorExtractSettings.AreaLimitLow),
                displayType: DeclaredFieldTemplateKey.Integer),
            new ToolParameterDeclaration(ColorExtractFields.AreaLimitHigh, "面积上限",
                ColorExtractFields.LimitSection, DataType.Integer, (long)ColorExtractFields.MaximumAreaLimit,
                minimum: ColorExtractFields.MinimumAreaLimit,
                maximum: ColorExtractFields.MaximumAreaLimit, step: 1, decimals: 0,
                storage: NumericStorage.Int32, propertyName: nameof(ColorExtractSettings.AreaLimitHigh),
                displayType: DeclaredFieldTemplateKey.Integer)
        },
        new ToolResultDeclaration(null,
            new[]
            {
                new ToolResultField(ColorExtractOutput.Image, "提取图像", ColorExtractOutput.ImageType,
                    children: ImageChildren()),
                new ToolResultField(ColorExtractOutput.TotalArea, "提取总面积", DataType.Number,
                    storage: NumericStorage.Float32),
                new ToolResultField(ColorExtractOutput.Roi, "提取区域外接矩形", ColorExtractOutput.RoiType,
                    nullable: true, children: RoiChildren()),
                new ToolResultField(ColorExtractOutput.ModuleRunTime, "模块耗时 (ms)", DataType.Number,
                    storage: NumericStorage.Float32),
                new ToolResultField(ColorExtractOutput.ModuleStatus, "模块状态", DataType.Integer,
                    storage: NumericStorage.Int32, showInResultPanel: false)
            },
            slots: null,
            // 列标识沿用原投影键，保证已保存方案的列选择不变。
            columns: new[]
            {
                new ToolResultColumn(ColorExtractOutput.Image, "提取图像", key: "DEFAULT"),
                new ToolResultColumn(ColorExtractOutput.TotalArea, "提取总面积", 0, key: "area"),
                new ToolResultColumn(ColorExtractOutput.Roi, "提取区域外接矩形", key: "roi"),
                new ToolResultColumn(ColorExtractOutput.ModuleRunTime, "模块耗时", 3, "ms", key: "time")
            },
            kind: ResultProjectionKind.PropertyList,
            shape: ToolResultShape.PropertyList),
        new ToolDisplayDeclaration(new[]
        {
            // 原厂 ResultShow 的四个 ImageViewConfig；ok/ng 与线宽取自 Display.xml。
            new ToolDisplayItem(ToolDisplayRole.Inspection, "检测区域",
                groupKey: "check-zone",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.ReferencePoint, "基准点",
                groupKey: "fixture-init-point",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.RunPoint, "运行点",
                groupKey: "fixture-run-point",
                ok: new ResultDisplayColor(255, 0, 0), ng: new ResultDisplayColor(255, 0, 0), width: 1),
            new ToolDisplayItem(ToolDisplayRole.Inspection, "扇环区域",
                groupKey: "check-zone-annular",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2)
        }),
        ToolInputTemplate.Image,
        sections: new[]
        {
            // 证据 IMVSColorExtractModuAlgorithmTab.xml：
            //   Tab_ROI Area / Tab_Run Params / Tab_Check Result 三个 Category 标 CanTriggerRun="TRUE"；
            //   Tab_Check Result 另标 SubscribeVisible="False"。
            // 本方 ROI 分组由面板承载（无独立参数分组），故只对可挂载的两组设置。
            new ToolSectionDeclaration(ColorExtractFields.RunPage, ColorExtractFields.RangeSection,
                canTriggerRun: true),
            new ToolSectionDeclaration(ColorExtractFields.ResultPage, ColorExtractFields.LimitSection,
                canTriggerRun: true, allowReference: false)
        },
        visibility: new[]
        {
            new FieldVisibilityRule(ColorExtractFields.AreaLimitEnabled, new[] { "true" },
                new[] { ColorExtractFields.AreaLimitLow, ColorExtractFields.AreaLimitHigh })
        },
        pairs: new[]
        {
            new ToolPairGroupDeclaration("proc.area-limit", ColorExtractFields.AreaLimitLow,
                ColorExtractFields.AreaLimitHigh, label: "面积上限")
        },
        inputPresentations: new[]
        {
            new ToolInputPresentation(ColorExtractTool.ImageInput, "图像输入", "基本参数", "图像输入")
        });
}
