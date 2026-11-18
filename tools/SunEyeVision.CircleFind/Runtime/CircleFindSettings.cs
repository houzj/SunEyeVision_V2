using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleFind.Runtime;

/// <summary>
/// 圆查找的唯一持久化设置。字段名、默认值与范围全部取自原厂证据：
/// 默认值取 <c>parameters.json</c>（原件 ParamRoot 28 项），范围/枚举取托管反编译
/// <c>CircleFindParam.cs:13-80,616-645</c>。原生算法本体未取证，算法语义待补证。
/// </summary>
public sealed record CircleFindSettings : IToolSettings<CircleFindSettings>
{
    /// <summary>原厂 FindMode（边缘类型）：1=最强 2=最后一条 3=第一条。</summary>
    public int FindMode { get; init; } = 1;
    /// <summary>原厂 EdgePolarity（边缘极性）：1=黑到白 2=白到黑 3=任意极性。</summary>
    public int EdgePolarity { get; init; } = 3;
    /// <summary>原厂 EdgeThresh（边缘阈值），范围 [0,255]。</summary>
    public int EdgeThresh { get; init; } = 15;
    /// <summary>原厂 EdgeWidth（滤波尺寸），范围 [1,50]。</summary>
    public int EdgeWidth { get; init; } = 2;
    /// <summary>原厂 RadNum（卡尺数量），范围 [3,1000]。</summary>
    public int RadNum { get; init; } = 30;
    /// <summary>原厂 RejectNum（剔除点数），范围 [0,997]。</summary>
    public int RejectNum { get; init; } = 0;
    /// <summary>原厂 CoarseDetectFlag（初定位）。</summary>
    public bool CoarseDetectFlag { get; init; }
    /// <summary>原厂 CCDSampleScale（下采样系数），范围 [1,8]。</summary>
    public int CcdSampleScale { get; init; } = 8;
    /// <summary>原厂 CCDCircleThresh（圆定位敏感度），范围 [1,1000]。</summary>
    public int CcdCircleThresh { get; init; } = 10;
    /// <summary>原厂 RejectDist（剔除距离），范围 [1,1000]。</summary>
    public int RejectDist { get; init; } = 5;
    /// <summary>原厂 ProLength（投影宽度），范围 [1,500]。</summary>
    public int ProLength { get; init; } = 5;
    /// <summary>原厂 InitType（初始拟合）：1=全局 2=局部。</summary>
    public int InitType { get; init; } = 2;
    /// <summary>原厂 FitFun（拟合方式）：1=最小二乘 2=Huber 3=Tukey。</summary>
    public int FitFun { get; init; } = 2;

    public bool RadiusLimitEnable { get; init; }
    public double RadiusLimitLow { get; init; }
    public double RadiusLimitHigh { get; init; } = 99999d;
    public bool CenterXLimitEnable { get; init; }
    public double CenterXLimitLow { get; init; }
    public double CenterXLimitHigh { get; init; } = 99999d;
    public bool CenterYLimitEnable { get; init; }
    public double CenterYLimitLow { get; init; }
    public double CenterYLimitHigh { get; init; } = 99999d;
    public bool FitErrorLimitEnable { get; init; }
    public double FitErrorLimitLow { get; init; }
    public double FitErrorLimitHigh { get; init; } = 100d;
    public bool FitPointsLimitEnable { get; init; }
    public int FitPointsLimitLow { get; init; } = 3;
    public int FitPointsLimitHigh { get; init; } = 200;

    public CircleFindSettings Freeze() => this;

    /// <summary>只校验设置本身：默认设置必须通过准入；越界直接报诊断，不静默夹取。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Range(string field, long value, long min, long max)
        {
            if (value < min || value > max)
                errors.Add(new("CIRCLEFIND_SETTINGS_INVALID", $"{field} 必须落在 {min} 到 {max} 之内。", field));
        }
        Range(CircleFindFields.FindMode, FindMode, 1, 3);
        Range(CircleFindFields.EdgePolarity, EdgePolarity, 1, 3);
        Range(CircleFindFields.EdgeThresh, EdgeThresh, 0, 255);
        Range(CircleFindFields.EdgeWidth, EdgeWidth, 1, 50);
        Range(CircleFindFields.RadNum, RadNum, 3, 1000);
        Range(CircleFindFields.RejectNum, RejectNum, 0, 997);
        Range(CircleFindFields.CcdSampleScale, CcdSampleScale, 1, 8);
        Range(CircleFindFields.CcdCircleThresh, CcdCircleThresh, 1, 1000);
        Range(CircleFindFields.RejectDist, RejectDist, 1, 1000);
        Range(CircleFindFields.ProLength, ProLength, 1, 500);
        Range(CircleFindFields.InitType, InitType, 1, 2);
        Range(CircleFindFields.FitFun, FitFun, 1, 3);
        Range(CircleFindFields.FitPointsLimitLow, FitPointsLimitLow, 3, 99999);
        Range(CircleFindFields.FitPointsLimitHigh, FitPointsLimitHigh, 3, 99999);
        void Pair(string lowField, string highField, double low, double high, double min, double max)
        {
            if (!double.IsFinite(low) || !double.IsFinite(high) || low < min || high > max)
                errors.Add(new("CIRCLEFIND_SETTINGS_INVALID", $"{lowField} 与 {highField} 必须落在 {min} 到 {max} 之内。", lowField));
            else if (low > high)
                errors.Add(new("CIRCLEFIND_SETTINGS_INVALID", $"{lowField} 不能大于 {highField}。", lowField));
        }
        if (RadiusLimitEnable) Pair(CircleFindFields.RadiusLimitLow, CircleFindFields.RadiusLimitHigh, RadiusLimitLow, RadiusLimitHigh, 0, 99999);
        if (CenterXLimitEnable) Pair(CircleFindFields.CenterXLimitLow, CircleFindFields.CenterXLimitHigh, CenterXLimitLow, CenterXLimitHigh, -99999, 99999);
        if (CenterYLimitEnable) Pair(CircleFindFields.CenterYLimitLow, CircleFindFields.CenterYLimitHigh, CenterYLimitLow, CenterYLimitHigh, -99999, 99999);
        if (FitErrorLimitEnable) Pair(CircleFindFields.FitErrorLimitLow, CircleFindFields.FitErrorLimitHigh, FitErrorLimitLow, FitErrorLimitHigh, 0, 100);
        if (FitPointsLimitEnable && FitPointsLimitLow > FitPointsLimitHigh)
            errors.Add(new("CIRCLEFIND_SETTINGS_INVALID", "拟合点数下限不能大于上限。", CircleFindFields.FitPointsLimitLow));
        return errors.AsReadOnly();
    }
}
