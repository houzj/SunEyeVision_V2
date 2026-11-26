namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>
/// 色彩提取的稳定设置身份与取值边界。
/// 参数页、结果、投影与显示配置的声明在 <see cref="ColorExtractManifest"/>；
/// 这里只放被设置校验、编辑器与编辑期准入共同引用的常量，不再自带 Schema/读写。
/// </summary>
public static class ColorExtractFields
{
    /// <summary>原厂 InputColorRange：ColorExtractList 序列化文本（'#' 分隔区间，',' 分隔 9 项）。</summary>
    public const string ColorRange = "proc.color-range";
    /// <summary>原厂 AreaLimitEnable。</summary>
    public const string AreaLimitEnabled = "proc.area-limit-enabled";
    /// <summary>原厂 RangeAreaLimit 下限（AreaLimitLow）。</summary>
    public const string AreaLimitLow = "proc.area-limit-low";
    /// <summary>原厂 RangeAreaLimit 上限（AreaLimitHigh）。</summary>
    public const string AreaLimitHigh = "proc.area-limit-high";

    /// <summary>
    /// 面积下界 0：原厂 <c>IMVSColorExtractModuAlgorithmTab.xml</c> 声明
    /// <c>AreaLimitLow/AreaLimitHigh</c> 范围均为 0~9999999（验收 D2 证据）。
    /// </summary>
    public const int MinimumAreaLimit = 0;
    /// <summary>原厂 AreaLimitHigh DefaultValue=9999999。</summary>
    public const int MaximumAreaLimit = 9999999;

    // 原厂 AlgorithmTab 页签：Tab_Basic Params / Tab_Run Params / ResultShow。
    public const string BasicPage = "基本参数";
    public const string RunPage = "运行参数";
    public const string ResultPage = "结果显示";
    public const string RangeSection = "区间列表";
    public const string LimitSection = "结果判断";
}
