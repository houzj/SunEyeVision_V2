using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量的声明唯一真源（原件 <c>IMVSIntensityMeasureModuAlgorithm.xml</c> +
/// <c>AlgorithmTab.xml</c> + <c>Display.xml</c>）。
/// </summary>
internal static class IntensityMeasureManifest
{
    public static ToolManifest Value { get; } = Build();

    /// <summary>五组判定：开关 + 上下限（下限负责区间显示名）。</summary>
    private static IEnumerable<ToolParameterDeclaration> Limits()
    {
        static ToolParameterDeclaration Enabled(string id, string label, string property) => new(
            id, label, IntensityMeasureFields.LimitSection, DataType.Boolean, false,
            propertyName: property, displayType: DeclaredFieldTemplateKey.Boolean);

        static ToolParameterDeclaration Bound(string id, string label, string property, TypeRef type,
            string defaultValue) => new(
            id, label, IntensityMeasureFields.LimitSection, type,
            type == DataType.Integer ? long.Parse(defaultValue) : double.Parse(defaultValue),
            minimum: IntensityMeasureFields.MinimumGray, maximum: IntensityMeasureFields.MaximumGray,
            step: type == DataType.Integer ? 1 : IntensityMeasureFields.NumberIncrement,
            decimals: type == DataType.Integer ? 0 : 2,
            storage: type == DataType.Integer ? NumericStorage.Int32 : null,
            propertyName: property, displayType: DeclaredFieldTemplateKey.Integer);

        // 最大值 / 最小值按整数（灰度级）声明，均值/标准差/对比度按浮点。
        yield return Enabled(IntensityMeasureFields.MaxValueLimitEnabled, "最大值判断", nameof(IntensityMeasureSettings.MaxValueLimitEnabled));
        yield return Bound(IntensityMeasureFields.MaxValueLimitLow, "最大值范围下限", nameof(IntensityMeasureSettings.MaxValueLimitLow), DataType.Integer, "0");
        yield return Bound(IntensityMeasureFields.MaxValueLimitHigh, "最大值范围上限", nameof(IntensityMeasureSettings.MaxValueLimitHigh), DataType.Integer, "255");
        yield return Enabled(IntensityMeasureFields.MinValueLimitEnabled, "最小值判断", nameof(IntensityMeasureSettings.MinValueLimitEnabled));
        yield return Bound(IntensityMeasureFields.MinValueLimitLow, "最小值范围下限", nameof(IntensityMeasureSettings.MinValueLimitLow), DataType.Integer, "0");
        yield return Bound(IntensityMeasureFields.MinValueLimitHigh, "最小值范围上限", nameof(IntensityMeasureSettings.MinValueLimitHigh), DataType.Integer, "255");
        yield return Enabled(IntensityMeasureFields.MeanLimitEnabled, "均值判断", nameof(IntensityMeasureSettings.MeanLimitEnabled));
        yield return Bound(IntensityMeasureFields.MeanLimitLow, "均值范围下限", nameof(IntensityMeasureSettings.MeanLimitLow), DataType.Number, "0");
        yield return Bound(IntensityMeasureFields.MeanLimitHigh, "均值范围上限", nameof(IntensityMeasureSettings.MeanLimitHigh), DataType.Number, "255");
        yield return Enabled(IntensityMeasureFields.StdLimitEnabled, "标准差判断", nameof(IntensityMeasureSettings.StdLimitEnabled));
        yield return Bound(IntensityMeasureFields.StdLimitLow, "标准差范围下限", nameof(IntensityMeasureSettings.StdLimitLow), DataType.Number, "0");
        yield return Bound(IntensityMeasureFields.StdLimitHigh, "标准差范围上限", nameof(IntensityMeasureSettings.StdLimitHigh), DataType.Number, "255");
        yield return Enabled(IntensityMeasureFields.ContrastLimitEnabled, "对比度判断", nameof(IntensityMeasureSettings.ContrastLimitEnabled));
        yield return Bound(IntensityMeasureFields.ContrastLimitLow, "对比度范围下限", nameof(IntensityMeasureSettings.ContrastLimitLow), DataType.Number, "0");
        yield return Bound(IntensityMeasureFields.ContrastLimitHigh, "对比度范围上限", nameof(IntensityMeasureSettings.ContrastLimitHigh), DataType.Number, "255");
    }

    private static IReadOnlyList<ToolResultField> RectChildren() => new[]
    {
        new ToolResultField("center", "矩形中心", FixtureDeclarations.Point),
        new ToolResultField("width", "矩形宽度", DataType.Number),
        new ToolResultField("height", "矩形高度", DataType.Number),
        new ToolResultField("angle-degrees", "矩形角度", DataType.Number)
    };

    private static IReadOnlyList<ToolResultField> MaskChildren() => new[]
    {
        new ToolResultField(IntensityMeasureOutput.ImageData, "图像数据", TypeRef.ResourceOf(ResourceKind.Image)),
        new ToolResultField(IntensityMeasureOutput.ImageWidth, "图像宽度", DataType.Integer),
        new ToolResultField(IntensityMeasureOutput.ImageHeight, "图像高度", DataType.Integer),
        new ToolResultField(IntensityMeasureOutput.ImageFormat, "图像像素格式", DataType.Integer)
    };

    private static ToolManifest Build() => new(
        IntensityMeasureTool.Id, "亮度测量",
        "统计检测区域内灰度的最小值、最大值、均值、标准差与直方图对比度，并按上下限判定",
        "intensity-measure", "测量", 9,
        Limits()
            .Concat(new[]
            {
                // 位置修正开关（AssistGroup）；「ROI区域」分组锚点没有 C# 属性，
                // 由工具的 ExtendSettings 追加，不进参数表。
                new ToolParameterDeclaration(IntensityMeasureFields.PositionCorrectionEnabled, "位置修正",
                    "ROI区域", DataType.Boolean, false,
                    propertyName: nameof(IntensityMeasureSettings.PositionCorrectionEnabled),
                    displayType: DeclaredFieldTemplateKey.Boolean)
            })
            .ToArray(),
        new ToolResultDeclaration(IntensityMeasureOutput.Rows,
            new[]
            {
                new ToolResultField(IntensityMeasureOutput.Status, "模块状态", DataType.Integer,
                    storage: NumericStorage.Int32, showInResultPanel: false),
                new ToolResultField(IntensityMeasureOutput.RunTime, "模块耗时 (ms)", DataType.Number,
                    storage: NumericStorage.Float32),
                new ToolResultField(IntensityMeasureOutput.LumMin, "亮度最小值", DataType.Integer,
                    storage: NumericStorage.Int32),
                new ToolResultField(IntensityMeasureOutput.LumMax, "亮度最大值", DataType.Integer,
                    storage: NumericStorage.Int32),
                new ToolResultField(IntensityMeasureOutput.LumMean, "亮度均值", DataType.Number),
                new ToolResultField(IntensityMeasureOutput.LumStd, "亮度标准差", DataType.Number),
                new ToolResultField(IntensityMeasureOutput.HistContrast, "直方图对比度", DataType.Number),
                new ToolResultField(IntensityMeasureOutput.Histogram, "灰度直方图",
                    IntensityMeasureOutput.HistogramType),
                new ToolResultField(IntensityMeasureOutput.Roi, "检测区域", IntensityMeasureOutput.RectangleType,
                    nullable: true, children: RectChildren()),
                new ToolResultField(IntensityMeasureOutput.Mask, "输出掩膜", IntensityMeasureOutput.MaskType,
                    nullable: true, children: MaskChildren())
            },
            // 显示合同指向额外的行端口（原厂 Display.xml 与 Output 段是两份声明）。
            slots: new ToolResultSlots(
                IntensityMeasureOutput.RowIndex,
                IntensityMeasureOutput.Rectangle,
                IntensityMeasureOutput.Region,
                IntensityMeasureOutput.LumMean,
                IntensityMeasureOutput.JudgmentPassed),
            columns: new[]
            {
                new ToolResultColumn(IntensityMeasureOutput.Status, "模块状态", key: "module-status"),
                new ToolResultColumn(IntensityMeasureOutput.RunTime, "模块耗时", 3, "ms", key: "module-time"),
                new ToolResultColumn(IntensityMeasureOutput.LumMin, "亮度最小值", 0, key: "lum-min"),
                new ToolResultColumn(IntensityMeasureOutput.LumMax, "亮度最大值", 0, key: "lum-max"),
                new ToolResultColumn(IntensityMeasureOutput.LumMean, "亮度均值", 3, key: "lum-mean"),
                new ToolResultColumn(IntensityMeasureOutput.LumStd, "亮度标准差", 3, key: "lum-std"),
                new ToolResultColumn(IntensityMeasureOutput.HistContrast, "直方图对比度", 3, key: "hist-contrast"),
                new ToolResultColumn(IntensityMeasureOutput.Histogram, "灰度直方图", key: "lum-histogram"),
                new ToolResultColumn(IntensityMeasureOutput.Roi, "检测区域", key: "roi"),
                new ToolResultColumn(IntensityMeasureOutput.Mask, "输出掩膜", key: "mask")
            },
            kind: ResultProjectionKind.PropertyList,
            selectedRectangleColor: new ResultDisplayColor(0, 0, 255),
            shape: ToolResultShape.PropertyList),
        new ToolDisplayDeclaration(new[]
        {
            // 原厂 Display.xml：亮度区域/检测区域/基准点/运行点/扇环区域五个图形项，
            // 外加「图像显示 → 文本」的纯可见性开关。
            // 亮度区域是主图形；检测区域（roi）才是 Inspection，扇环走 Annulus。
            new ToolDisplayItem(ToolDisplayRole.Graphic, IntensityMeasureResultDisplay.RegionTitle,
                groupKey: "lum-region",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.Inspection, IntensityMeasureResultDisplay.RoiTitle,
                groupKey: "roi",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.ReferencePoint, "基准点",
                groupKey: "reference-point",
                ok: new ResultDisplayColor(102, 255, 0), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.RunPoint, "运行点",
                groupKey: "run-point",
                ok: new ResultDisplayColor(255, 0, 0), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            new ToolDisplayItem(ToolDisplayRole.Annulus, "扇环检测区域",
                groupKey: "annulus",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0), width: 2),
            // 原厂该工具文本组没有总开关行；可见性由独立的 text-overlay 项承载。
            new ToolDisplayItem(ToolDisplayRole.Text, "文本", groupKey: "text", showVisible: false,
                textContent: IntensityMeasureResultDisplay.DefaultTextContent),
            new ToolDisplayItem(ToolDisplayRole.Text, "文本", groupKey: "text-overlay", visibleOnly: true)
        }),
        ToolInputTemplate.ImageRoiMask,
        sections: new[]
        {
            // 证据 IMVSIntensityMeasureModuAlgorithmTab.xml：
            //   Category Name="Tab_ROI Area"     CanTriggerRun="TRUE"
            //   Category Name="Tab_Check Result" CanTriggerRun="TRUE" SubscribeVisible="False"
            new ToolSectionDeclaration("基本参数", "ROI区域", canTriggerRun: true),
            new ToolSectionDeclaration(IntensityMeasureFields.ResultPage, IntensityMeasureFields.LimitSection,
                canTriggerRun: true, allowReference: false)
        },
        visibility: new[]
        {
            new FieldVisibilityRule(IntensityMeasureFields.MaxValueLimitEnabled, new[] { "true" },
                new[] { IntensityMeasureFields.MaxValueLimitLow, IntensityMeasureFields.MaxValueLimitHigh }),
            new FieldVisibilityRule(IntensityMeasureFields.MinValueLimitEnabled, new[] { "true" },
                new[] { IntensityMeasureFields.MinValueLimitLow, IntensityMeasureFields.MinValueLimitHigh }),
            new FieldVisibilityRule(IntensityMeasureFields.MeanLimitEnabled, new[] { "true" },
                new[] { IntensityMeasureFields.MeanLimitLow, IntensityMeasureFields.MeanLimitHigh }),
            new FieldVisibilityRule(IntensityMeasureFields.StdLimitEnabled, new[] { "true" },
                new[] { IntensityMeasureFields.StdLimitLow, IntensityMeasureFields.StdLimitHigh }),
            new FieldVisibilityRule(IntensityMeasureFields.ContrastLimitEnabled, new[] { "true" },
                new[] { IntensityMeasureFields.ContrastLimitLow, IntensityMeasureFields.ContrastLimitHigh })
        },
        pairs: new[]
        {
            new ToolPairGroupDeclaration("limit.max-value", IntensityMeasureFields.MaxValueLimitLow,
                IntensityMeasureFields.MaxValueLimitHigh, label: "最大值范围"),
            new ToolPairGroupDeclaration("limit.min-value", IntensityMeasureFields.MinValueLimitLow,
                IntensityMeasureFields.MinValueLimitHigh, label: "最小值范围"),
            new ToolPairGroupDeclaration("limit.mean", IntensityMeasureFields.MeanLimitLow,
                IntensityMeasureFields.MeanLimitHigh, label: "均值范围"),
            new ToolPairGroupDeclaration("limit.std", IntensityMeasureFields.StdLimitLow,
                IntensityMeasureFields.StdLimitHigh, label: "标准差范围"),
            new ToolPairGroupDeclaration("limit.contrast", IntensityMeasureFields.ContrastLimitLow,
                IntensityMeasureFields.ContrastLimitHigh, label: "对比度范围")
        },
        inputPresentations: new[]
        {
            new ToolInputPresentation(IntensityMeasureTool.ImageInput, "输入源", "基本参数", "图像输入")
        },
        extraInputs: new[]
        {
            new Port(IntensityMeasureRoi.LocalDocumentId, RoiDocumentValue.Declaration,
                Required: false, EditModes: FieldEditModes.Constant)
        }.Concat(IntensityMeasureRoi.Ports).Concat(IntensityMeasureAssist.Ports).ToArray(),
        extraOutputs: new[] { new Port(IntensityMeasureOutput.Rows, IntensityMeasureOutput.RowsType) },
        manualReferenceSources: new[]
        {
            IntensityMeasureOutput.Status, IntensityMeasureOutput.RunTime,
            IntensityMeasureOutput.LumMin, IntensityMeasureOutput.LumMax,
            IntensityMeasureOutput.LumMean, IntensityMeasureOutput.LumStd,
            IntensityMeasureOutput.HistContrast
        });
}
