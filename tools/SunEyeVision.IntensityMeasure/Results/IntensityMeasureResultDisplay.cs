using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 原件「结果显示」页的图像/文本显示项。原件该页由 5 个 <c>ImageViewConfig</c> 组成
/// （<c>SensorFiled</c>、<c>FixtureInitPoint</c>、<c>FixtureRunPoint</c>、<c>ResutlText</c>、
/// <c>CheckZoneAnnular</c>，见 docs/07-工具逆向/tools/imvsintensitymeasuremodu/ui-preview.json:67-86）：
/// 本方实现「亮度区域」「检测区域」两个图形分组与「文本」七行；位置修正的基准点/运行点
/// （<c>FixtureInitPoint</c>/<c>FixtureRunPoint</c>）与 <c>SensorFiled</c> 随位置修正能力一起未实现，
/// 见交接单未实现项。这些字段只随工具持久化，不是算法参数，也不进入执行输入。
/// </summary>
public static class IntensityMeasureResultDisplay
{
    public const string Region = "display.lum-region";
    public const string Roi = "display.roi";
    public const string Text = "display.text";
    public const string ReferencePoint = "display.reference-point";
    public const string RunPoint = "display.run-point";
    public const string Annulus = "display.annulus";
    /// <summary>原厂「图像显示 → 文本」行：只控制文字叠加可见性。</summary>
    public const string TextOverlayVisible = "display.text-overlay.visible";
    public static string Field(string group, string role) => group + "." + role;

    public const string RegionTitle = "亮度区域";
    public const string RoiTitle = "检测区域";

    /// <summary>原厂文本项默认模板：模块状态与五项统计量，占位符由结果文本表达式解析。</summary>
    public const string DefaultTextContent = "模块状态:{模块状态}最小值:{最小值} 最大值:{最大值} 均值:{均值} 标准差:{标准差} 对比度:{对比度}";
}
