namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>
/// 划片拆分的稳定设置身份与取值边界（原件 <c>IMVSDivideImageModu</c> 的四个算法参数）。
/// 参数页、结果、投影的声明在 <see cref="DivideImageManifest"/>；
/// 这里只放被设置校验、编辑器与编辑期准入共同引用的常量，不再自带 Schema/读写。
/// </summary>
public static class DivideImageFields
{
    /// <summary>原厂参数 <c>NumX</c>：X 方向划片数量，DefaultValue=1。</summary>
    public const string Columns = "proc.num-x";
    /// <summary>原厂参数 <c>NumY</c>：Y 方向划片数量，DefaultValue=1。</summary>
    public const string Rows = "proc.num-y";
    /// <summary>原厂参数 <c>OverlaprateX</c>：X 方向重叠率（百分比），DefaultValue=0。</summary>
    public const string OverlapX = "proc.overlap-rate-x";
    /// <summary>原厂参数 <c>OverlaprateY</c>：Y 方向重叠率（百分比），DefaultValue=0。</summary>
    public const string OverlapY = "proc.overlap-rate-y";

    /// <summary>原件 AlgorithmTab 把四个划片数值控件放在 <c>Tab_Run Params</c>。</summary>
    public const string Page = "运行参数";
    public const string Section = "划片参数";
    /// <summary>原厂 AssistGroup 开关，位于基本参数/ROI区域末尾。</summary>
    public const string PositionCorrectionEnabled = "roi.position-correction";

    /// <summary>重叠率下界（无重叠）。</summary>
    public const int MinimumOverlapPercent = 0;
    /// <summary>
    /// 重叠率上界 100：原厂 <c>IMVSDivideImageModuAlgorithmTab.xml</c> 声明
    /// <c>OverlaprateX/Y</c> 范围为 0~100，默认 0（验收 D2 证据）。
    /// </summary>
    public const int MaximumOverlapPercent = 100;

    /// <summary>单方向划片数上界 50：原厂 <c>NumX/NumY</c> 声明范围 1~50（同上证据）。</summary>
    public const int MaximumDivisionsPerAxis = 50;
}
