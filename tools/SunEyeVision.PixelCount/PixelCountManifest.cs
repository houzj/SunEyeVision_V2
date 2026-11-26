using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// PixelCount 的声明真源（参数 + 分组 + 显隐 + 成对 + ROI 组 + 结果槽位 + 显示项）。
/// 对应原厂 <c>IMVSPixelCountModuVA.xml</c> / <c>...Algorithm.xml</c> /
/// <c>...AlgorithmTab.xml</c> / <c>...Display.xml</c> 四份声明。
/// </summary>
internal static class PixelCountManifest
{
    /// <summary>原厂 Display.xml 模板 <c>ModuleStatus:{0}PixelNum:{1} ,Rate{2}</c> 的中文资源值。</summary>
    public const string TextContent = "模块状态:{0}像素数量:{1} ,比率{2}";

    public static ToolManifest Value { get; } = Build();

    private static ToolManifest Build() => new(
        // 目录身份沿用原件：名称、描述、分类与序号逐字对齐。
        "PixelCount", "像素统计", "统计图像中落在各灰度区间的像素数与占比，并按数量/比率判定",
        "pixel-count", "测量", 9,
        BuildParameters(),
        new ToolResultDeclaration(
            PixelCountOutput.Rows,
            new[]
            {
                new ToolResultField(PixelCountOutput.RangeIndex, "区间序号", DataType.Integer,
                    storage: NumericStorage.Int32),
                new ToolResultField(PixelCountOutput.ThresholdMin, "阈值下限", DataType.Integer,
                    storage: NumericStorage.Int32),
                new ToolResultField(PixelCountOutput.ThresholdMax, "阈值上限", DataType.Integer,
                    storage: NumericStorage.Int32),
                new ToolResultField(PixelCountOutput.RangeCount, "像素数", DataType.Integer,
                    storage: NumericStorage.Int32),
                new ToolResultField(PixelCountOutput.RangeRatio, "比率", DataType.Number,
                    storage: NumericStorage.Float32),
                new ToolResultField(PixelCountOutput.Rectangle, "检测区域", PixelCountOutput.RectangleType),
                new ToolResultField(PixelCountOutput.Region, "像素区域", TypeRef.ResourceOf(ResourceKind.Region)),
                new ToolResultField(PixelCountOutput.JudgmentPassed, "判定", DataType.Boolean)
            },
            new ToolResultSlots(PixelCountOutput.RangeIndex, PixelCountOutput.Rectangle,
                PixelCountOutput.Region, PixelCountOutput.RangeRatio, PixelCountOutput.JudgmentPassed),
            columns: new[]
            {
                new ToolResultColumn(PixelCountOutput.RangeIndex, "序号", 0),
                new ToolResultColumn(PixelCountOutput.ThresholdMin, "灰度下限", 0),
                new ToolResultColumn(PixelCountOutput.ThresholdMax, "灰度上限", 0),
                new ToolResultColumn(PixelCountOutput.RangeCount, "像素数", 0),
                new ToolResultColumn(PixelCountOutput.RangeRatio, "比率", 4),
                new ToolResultColumn(PixelCountOutput.JudgmentPassed, "判定", key: "judgment")
            },
            selectedRectangleColor: new ResultDisplayColor(0, 0, 255)),
        new ToolDisplayDeclaration(new[]
        {
            // 原厂 ImageViewConfig 顺序：CheckZone、FixtureInitPoint、FixtureRunPoint、ResutlText。
            new ToolDisplayItem(ToolDisplayRole.Inspection, "检测区域", groupKey: "roi",
                ok: new ResultDisplayColor(0, 0, 255), ng: new ResultDisplayColor(255, 0, 0),
                width: 2, showMeasurements: true),
            new ToolDisplayItem(ToolDisplayRole.ReferencePoint, "基准点", groupKey: "reference-point",
                width: 1, fixedJudgment: true, pointLabel: "Fixture BasePoint"),
            new ToolDisplayItem(ToolDisplayRole.RunPoint, "运行点", groupKey: "run-point",
                width: 1, fixedJudgment: true, pointLabel: "Fixture RunPoint"),
            // 文本项只有眼睛、没有样式笔。
            new ToolDisplayItem(ToolDisplayRole.Text, "文本", groupKey: "text",
                textContent: TextContent)
        }),
        ToolInputTemplate.ImageRoiMask,
        sections: new[]
        {
            // 证据 IMVSPixelCountModuAlgorithmTab.xml：
            //   Category Tab_ROI Area / Tab_Run Params / Tab_Check Result 均标 CanTriggerRun="TRUE"；
            //   Tab_Check Result 另标 SubscribeVisible="False"（判定区不提供订阅来源入口）。
            new ToolSectionDeclaration("基本参数", "ROI区域", vmCategory: "Tab_ROI Area", canTriggerRun: true),
            new ToolSectionDeclaration("运行参数", "阈值范围", vmCategory: "Tab_Run Params", canTriggerRun: true),
            new ToolSectionDeclaration("结果显示", "结果判定", vmCategory: "ResultShow",
                canTriggerRun: true, allowReference: false)
        },
        visibility: new[]
        {
            new FieldVisibilityRule("limit.count.enabled", new[] { "true" },
                new[] { "limit.count.low", "limit.count.high" }),
            new FieldVisibilityRule("limit.ratio.enabled", new[] { "true" },
                new[] { "limit.ratio.low", "limit.ratio.high" })
        },
        pairs: BuildPairs(),
        // 原厂只把 ImageSource 呈现为独立输入行；ROI/掩膜由 ROI 组与 Assist 组承载，避免重复成行。
        inputPresentations: new[]
        {
            new ToolInputPresentation(PixelCountTool.ImageInput, "输入源", "基本参数", "图像输入")
        },
        roiGroups: new[] { BuildRoiGroup() },
        extraInputs: BuildExtraInputs(),
        // 行端口之外的输出（原厂 Output 段的标量/图像/区域端口）。
        extraOutputs: PixelCountOutput.Ports.Where(port => port.Id != PixelCountOutput.Rows).ToArray(),
        manualReferenceSources: new[]
        {
            PixelCountOutput.Status, PixelCountOutput.RunTime,
            PixelCountOutput.Count, PixelCountOutput.Ratio
        });

    /// <summary>组合体输入：本地 ROI 文档、ROI 几何端口、Assist 端口（原厂 Algorithm.xml 的额外 Input）。</summary>
    private static IReadOnlyList<Port> BuildExtraInputs() => new[]
    {
        new Port(PixelCountLocalRoi.InputId, RoiDocumentValue.Declaration,
            Required: false, EditModes: FieldEditModes.Constant)
    }.Concat(PixelCountRoi.Ports).Concat(PixelCountAssist.Ports).ToArray();

    private static IReadOnlyList<ToolParameterDeclaration> BuildParameters()
    {
        var parameters = new List<ToolParameterDeclaration>
        {
            new("roi.area-type", "ROI区域类型", "ROI区域", DataType.Integer, 0L,
                minimum: 0, maximum: 1, step: 1, decimals: 0, storage: NumericStorage.Int32,
                displayType: "Integer", propertyName: nameof(PixelCountSettings.RoiAreaType)),
            new("roi.output-mask", "输出掩膜", "ROI区域", DataType.Boolean, false,
                displayType: "Boolean", propertyName: nameof(PixelCountSettings.OutputMaskEnabled)),
            new("roi.position-correction", "位置修正", "ROI区域", DataType.Boolean, false,
                displayType: "Boolean", propertyName: nameof(PixelCountSettings.PositionCorrectionEnabled)),
            new("range.count", "区间数量", "阈值范围", DataType.Integer, 1L,
                minimum: 1, maximum: 32, step: 1, decimals: 0, storage: NumericStorage.Int32,
                displayType: "PixelCountRangeList", propertyName: nameof(PixelCountSettings.RangeCount)),
            new("limit.count.enabled", "数量判断", "结果判定", DataType.Boolean, false,
                tipsOn: "已启用", tipsOff: "未启用", displayType: "BooleanTips",
                propertyName: nameof(PixelCountSettings.CountLimitEnabled)),
            new("limit.count.low", "数量范围下限", "结果判定", DataType.Integer, 0L,
                minimum: 0, maximum: PixelCountFields.MaximumCountLimit, step: 1, decimals: 0,
                storage: NumericStorage.Int32, displayType: "IntegerBettween",
                propertyName: nameof(PixelCountSettings.CountLimitLow)),
            new("limit.count.high", "数量范围上限", "结果判定", DataType.Integer, PixelCountFields.MaximumCountLimit,
                minimum: 0, maximum: PixelCountFields.MaximumCountLimit, step: 1, decimals: 0,
                storage: NumericStorage.Int32, displayType: "Integer",
                propertyName: nameof(PixelCountSettings.CountLimitHigh)),
            new("limit.ratio.enabled", "比率判断", "结果判定", DataType.Boolean, false,
                tipsOn: "已启用", tipsOff: "未启用", displayType: "BooleanTips",
                propertyName: nameof(PixelCountSettings.RatioLimitEnabled)),
            new("limit.ratio.low", "比率范围下限", "结果判定", DataType.Number, 0d,
                minimum: 0, maximum: PixelCountFields.MaximumRatioLimit, step: PixelCountFields.RatioIncrement,
                decimals: 2, displayType: "FloatBettween",
                propertyName: nameof(PixelCountSettings.RatioLimitLow)),
            new("limit.ratio.high", "比率范围上限", "结果判定", DataType.Number, 1d,
                minimum: 0, maximum: PixelCountFields.MaximumRatioLimit, step: PixelCountFields.RatioIncrement,
                decimals: 2, displayType: "Float",
                propertyName: nameof(PixelCountSettings.RatioLimitHigh))
        };
        // 32 组区间槽位不是 C# 属性，不能进反射型参数表；由 PixelCountTool.ExtendSettings 追加。
        return parameters;
    }

    private static IReadOnlyList<ToolPairGroupDeclaration> BuildPairs()
    {
        var pairs = new List<ToolPairGroupDeclaration>
        {
            new("limit.count.range", "limit.count.low", "limit.count.high", label: "数量范围"),
            new("limit.ratio.range", "limit.ratio.low", "limit.ratio.high", label: "比率范围")
        };
        // 槽位成对由 PixelCountTool.ExtendSettings 直接写进 FieldLayout。
        return pairs;
    }

    /// <summary>
    /// 原厂 <c>RoiSelectGroup</c> + <c>ROISelecter</c> 的参数页组合：
    /// 绘制/继承单选、矩形与扇环字段、区域类型（图形/图像）、掩膜与屏蔽区。
    /// </summary>
    private static RoiGroupDeclaration BuildRoiGroup() => new(
        "ROI区域",
        "input.roi.inheritance-mode",
        "Local",
        new[] { "Rectangle", "Annulus" },
        "input.roi.rectangle",
        "input.roi.annulus",
        new[]
        {
            new RoiParameterRowDeclaration("input.roi.rectangle.center-x", "ROI中心X"),
            new RoiParameterRowDeclaration("input.roi.rectangle.center-y", "ROI中心Y"),
            new RoiParameterRowDeclaration("input.roi.rectangle.width", "ROI宽度"),
            new RoiParameterRowDeclaration("input.roi.rectangle.height", "ROI高度"),
            new RoiParameterRowDeclaration("input.roi.rectangle.angle-degrees", "ROI角度")
        },
        new[]
        {
            new RoiParameterRowDeclaration("input.roi.annulus.center-x", "圆中心X"),
            new RoiParameterRowDeclaration("input.roi.annulus.center-y", "圆中心Y"),
            new RoiParameterRowDeclaration("input.roi.annulus.inner-radius", "内径"),
            new RoiParameterRowDeclaration("input.roi.annulus.outer-radius", "外径"),
            new RoiParameterRowDeclaration("input.roi.annulus.start-degrees", "起始角"),
            new RoiParameterRowDeclaration("input.roi.annulus.sweep-degrees", "角度范围")
        },
        "input.roi.area-type",
        "ROI区域类型",
        new[]
        {
            new RoiChoiceDeclaration(0, "图形类型"),
            new RoiChoiceDeclaration(1, "图像类型")
        },
        "input.roi.mask-image",
        "输入掩膜",
        "input.roi.output-mask",
        "输出掩膜",
        "input.roi.mask-source",
        "屏蔽区",
        "input.roi.mask-inherited",
        // 原厂 ROISelecter：单模式 Mul 适用全部形状；屏蔽区只支持多边形（ROISelecter_Extern）。
        new RoiSelectDeclaration("Mul", new[] { "Box", "SectorRing", "Polygon", "Sector" },
            maximumCount: 16, fullScreenEnable: true,
            avoidSelectType: "Avoid", avoidShapes: new[] { "Polygon" }));
}
