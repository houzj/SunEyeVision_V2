using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量的参数页字段身份与布局。
/// 原件 15 个 <c>ParamItem</c> 逐条对应（五类判据 × Enable/LimitLow/LimitHigh）：
/// 证据 docs/07-工具逆向/tools/imvsintensitymeasuremodu/parameters.json:12-278
/// （<c>StdLimitHigh</c>=255、<c>MeanLimitLow</c>=0、<c>MaxValueLimitHigh</c>=255、<c>StdLimitEnable</c>=False、
/// <c>ContrastLimitEnable</c>=False、<c>ContrastLimitLow</c>=0、<c>ContrastLimitHigh</c>=255）。
/// 原件工具栏只有两个页签（<c>Tab_Basic Params</c>、<c>ResultShow</c>，见 resources.json 的 uiTabs），
/// 判据归入「结果显示」页的「结果判断」分区；判据落在哪个页签未取到逐控件绑定证据，结论标未确认。
/// </summary>
public static class IntensityMeasureFields
{
    public const string MaxValueLimitEnabled = "limit.max-value.enabled";
    public const string MaxValueLimitLow = "limit.max-value.low";
    public const string MaxValueLimitHigh = "limit.max-value.high";
    public const string MinValueLimitEnabled = "limit.min-value.enabled";
    public const string MinValueLimitLow = "limit.min-value.low";
    public const string MinValueLimitHigh = "limit.min-value.high";
    public const string MeanLimitEnabled = "limit.mean.enabled";
    public const string MeanLimitLow = "limit.mean.low";
    public const string MeanLimitHigh = "limit.mean.high";
    public const string StdLimitEnabled = "limit.std.enabled";
    public const string StdLimitLow = "limit.std.low";
    public const string StdLimitHigh = "limit.std.high";
    public const string ContrastLimitEnabled = "limit.contrast.enabled";
    public const string ContrastLimitLow = "limit.contrast.low";
    public const string ContrastLimitHigh = "limit.contrast.high";
    /// <summary>ROI区域组的分区锚点：行被抑制，只负责建立原厂分类标题。</summary>
    public const string RoiSectionAnchor = "section.roi";
    /// <summary>原厂 AssistGroup 的启用开关「位置修正」。</summary>
    public const string PositionCorrectionEnabled = "roi.position-correction";

    /// <summary>原件 <c>IntegerBettween</c>/<c>FloatBettween</c> 的 MinValue/MaxValue：灰度 8 位量化范围 0..255。</summary>
    public const long MinimumGray = 0;
    public const long MaximumGray = 255;
    public const double NumberIncrement = 0.01d;

    /// <summary>原件第二个页签 <c>ResultShow</c>：判据参数与显示项所在页。</summary>
    public const string ResultPage = "结果显示";
    /// <summary>原件 <c>ResultShow</c> 页的「结果判断」分类。</summary>
    public const string LimitSection = "结果判断";

}
