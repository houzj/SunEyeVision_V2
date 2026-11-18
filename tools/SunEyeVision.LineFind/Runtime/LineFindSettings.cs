using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.LineFind.Runtime;

/// <summary>
/// 直线查找的唯一持久化设置。字段名/默认值取自 <c>parameters.json</c>（原件 ParamRoot 24 项），
/// 范围/枚举取托管反编译 <c>LineFindParam.cs:13-84,513-542</c>。原生算法体（IMVSLineFindModu.dll）未反汇编，待补证。
/// </summary>
public sealed record LineFindSettings : IToolSettings<LineFindSettings>
{
    /// <summary>原厂 FindMode（边缘类型）：1=最强 2=第一条 3=最后一条 4=接近中线。</summary>
    public int FindMode { get; init; } = 1;
    /// <summary>原厂 EdgePolarity：1=黑到白 2=白到黑 3=任意。</summary>
    public int EdgePolarity { get; init; } = 3;
    /// <summary>原厂 EdgeStrength（边缘阈值），范围 [1,255]。</summary>
    public int EdgeStrength { get; init; } = 15;
    /// <summary>原厂 KernelSize（滤波尺寸），范围 [1,50]。</summary>
    public int KernelSize { get; init; } = 2;
    /// <summary>原厂 RayNum（卡尺数量），范围 [2,1000]。</summary>
    public int RayNum { get; init; } = 20;
    public bool RevertFindOrient { get; init; }
    public bool AngleNormalization { get; init; }
    public bool LineAngleEnable { get; init; }
    /// <summary>原厂 ExpectAngle，范围 [-180,180]。</summary>
    public double ExpectAngle { get; init; }
    /// <summary>原厂 RotateTolerance，范围 [0,180]。</summary>
    public double RotateTolerance { get; init; } = 180d;
    /// <summary>原厂 RejectNum（剔除点数），范围 [0,998]。</summary>
    public int RejectNum { get; init; }
    /// <summary>原厂 RejectDist（剔除距离），范围 [1,1000]。</summary>
    public int RejectDist { get; init; } = 5;
    /// <summary>原厂 RegionWidth（投影宽度），范围 [1,100]。</summary>
    public int RegionWidth { get; init; } = 5;
    /// <summary>原厂 InitType：1=全局ALS 2=局部LLS。</summary>
    public int InitType { get; init; } = 2;
    /// <summary>原厂 FitFun：1=最小二乘 2=Huber 3=Tukey。</summary>
    public int FitFun { get; init; } = 2;

    public bool FitPointsLimitEnable { get; init; }
    public int FitPointsLimitLow { get; init; } = 2;
    public int FitPointsLimitHigh { get; init; } = 200;
    public bool AngleLimitEnable { get; init; }
    public double AngleLimitLow { get; init; } = -180d;
    public double AngleLimitHigh { get; init; } = 180d;
    public bool ScoreLimitEnable { get; init; }
    public double ScoreLimitLow { get; init; }
    public double ScoreLimitHigh { get; init; } = 9999d;

    public LineFindSettings Freeze() => this;

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Int(string field, long value, long min, long max)
        {
            if (value < min || value > max)
                errors.Add(new("LINEFIND_SETTINGS_INVALID", $"{field} 必须落在 {min} 到 {max} 之内。", field));
        }
        Int(LineFindFields.FindMode, FindMode, 1, 4);
        Int(LineFindFields.EdgePolarity, EdgePolarity, 1, 3);
        Int(LineFindFields.EdgeStrength, EdgeStrength, 1, 255);
        Int(LineFindFields.KernelSize, KernelSize, 1, 50);
        Int(LineFindFields.RayNum, RayNum, 2, 1000);
        Int(LineFindFields.RejectNum, RejectNum, 0, 998);
        Int(LineFindFields.RejectDist, RejectDist, 1, 1000);
        Int(LineFindFields.RegionWidth, RegionWidth, 1, 100);
        Int(LineFindFields.InitType, InitType, 1, 2);
        Int(LineFindFields.FitFun, FitFun, 1, 3);
        Int(LineFindFields.FitPointsLimitLow, FitPointsLimitLow, 2, 99999);
        Int(LineFindFields.FitPointsLimitHigh, FitPointsLimitHigh, 2, 99999);
        void Number(string field, double value, double min, double max)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                errors.Add(new("LINEFIND_SETTINGS_INVALID", $"{field} 必须落在 {min} 到 {max} 之内。", field));
        }
        Number(LineFindFields.ExpectAngle, ExpectAngle, -180, 180);
        Number(LineFindFields.RotateTolerance, RotateTolerance, 0, 180);
        void Pair(string lowField, string highField, double low, double high, double min, double max)
        {
            if (!double.IsFinite(low) || !double.IsFinite(high) || low < min || high > max)
                errors.Add(new("LINEFIND_SETTINGS_INVALID", $"{lowField} 与 {highField} 必须落在 {min} 到 {max} 之内。", lowField));
            else if (low > high)
                errors.Add(new("LINEFIND_SETTINGS_INVALID", $"{lowField} 不能大于 {highField}。", lowField));
        }
        if (FitPointsLimitEnable && FitPointsLimitLow > FitPointsLimitHigh)
            errors.Add(new("LINEFIND_SETTINGS_INVALID", "拟合点数下限不能大于上限。", LineFindFields.FitPointsLimitLow));
        if (AngleLimitEnable) Pair(LineFindFields.AngleLimitLow, LineFindFields.AngleLimitHigh, AngleLimitLow, AngleLimitHigh, -180, 180);
        if (ScoreLimitEnable) Pair(LineFindFields.ScoreLimitLow, LineFindFields.ScoreLimitHigh, ScoreLimitLow, ScoreLimitHigh, 0, 9999);
        return errors.AsReadOnly();
    }
}
